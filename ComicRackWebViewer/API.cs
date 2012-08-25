﻿using System;
using System.Windows;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using cYo.Projects.ComicRack.Engine;
using cYo.Projects.ComicRack.Engine.IO.Provider;
using cYo.Projects.ComicRack.Viewer;
using Nancy;
using Nancy.OData;

namespace ComicRackWebViewer
{
    public static class API
    {
        public static BooksList GetIssuesOfList(string name, NancyContext context)
        {
            var list = Program.Database.ComicLists.FirstOrDefault(x => x.Name == name);
            if (list == null)
            {
                return new BooksList
                {
                    Comics = Enumerable.Empty<Comic>(),
                    Name = name
                };
            }
            return new BooksList
            {
                Comics = context.ApplyODataUriFilter(list.GetBooks().Select(x => x.ToComic())).Cast<Comic>(),
                Name = name
            };
        }
        
        public static BooksList GetIssuesOfListFromId(Guid id, NancyContext context)
        {
            var list = Program.Database.ComicLists.FindItem(id);
            if (list == null)
            {
                return new BooksList
                {
                    Comics = Enumerable.Empty<Comic>(),
                    Id = id
                };
            }
            return new BooksList
            {
                Comics = list.GetBooks().Select(x => x.ToComic()),
                Id = id
            };
        }

        public static IEnumerable<Series> GetSeries()
        {
            return Plugin.Application.GetLibraryBooks().AsSeries();
        }

        public static BooksList GetSeries(Guid id, NancyContext context)
        {
            var books = Plugin.Application.GetLibraryBooks();
            var book = books.Where(x => x.Id == id).First();
            var series = books.Where(x => x.Series == book.Series)
                .Where(x => x.Volume == book.Volume)
                .Select(x => x.ToComic())
                .OrderBy(x => x.Number).ToList();
            return new BooksList
            {
                Comics = context.ApplyODataUriFilter(series).Cast<Comic>(),
                Name = book.Series
            };
        }
        
        public static IEnumerable<Comic> GetComicsFromSeries(Guid id)
        {
            var books = Plugin.Application.GetLibraryBooks();
            var book = books.Where(x => x.Id == id).First();
            var series = books.Where(x => x.Series == book.Series)
                .Where(x => x.Volume == book.Volume)
                .Select(x => x.ToComic())
                .OrderBy(x => x.Number).ToList();
            
            return series;
        }

        public static Response GetThumbnailImage(Guid id, int page, IResponseFormatter response)
        {
            var bitmap = Image.FromStream(new MemoryStream(GetPageImageBytes(id, page)), false, false);
            double ratio = 200D / (double)bitmap.Height;
            int width = (int)(bitmap.Width * ratio);
            var callback = new Image.GetThumbnailImageAbort(() => true);
            var thumbnail = bitmap.GetThumbnailImage(width, 200, callback, IntPtr.Zero);
            MemoryStream stream = GetBytesFromImage(thumbnail);
            return response.FromStream(stream, MimeTypes.GetMimeType(".jpg"));
        }

        public static MemoryStream GetBytesFromImage(Image image)
        {
            var bitmap = new Bitmap(image);
            MemoryStream stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Jpeg);
            stream.Position = 0;
            return stream;
        }

        private static byte[] GetPageImageBytes(Guid id, int page)
        {
            try
            {
              var comic = GetComics().First(x => x.Id == id);
              var index = comic.TranslatePageToImageIndex(page);
              var provider = GetProvider(comic);
              if (provider == null)
              {
                  return null;
              }
              return provider.GetByteImage(index); // ComicRack returns the page converted to a jpeg image.....
            }
            catch (Exception e)
            {
                //MessageBox.Show(e.ToString());
                return null;
            }
        }

        public static Response GetPageImage(Guid id, int page, IResponseFormatter response)
        {
            var bytes = GetPageImageBytes(id, page);
            if (bytes == null)
            {
                return response.AsRedirect("/original/Views/spacer.png");
            }
            return response.FromStream(new MemoryStream(bytes), MimeTypes.GetMimeType(".jpg"));
        }

        
        public static Image Resize(Image img, int width, int height)
        {
            //create a new Bitmap the size of the new image
            Bitmap bmp = new Bitmap(width, height);
            //create a new graphic from the Bitmap
            Graphics graphic = Graphics.FromImage((Image)bmp);
            graphic.InterpolationMode = InterpolationMode.HighQualityBicubic;
            //draw the newly resized image
            graphic.DrawImage(img, 0, 0, width, height);
            //dispose and free up the resources
            graphic.Dispose();
            //return the image
            return (Image)bmp;
        }
        
        public static Response GetPageImage(Guid id, int page, int width, int height, IResponseFormatter response)
        {
            string filename = string.Format("{0}-p{1}-w{2}-h{3}.jpg", id, page, width, height);
            try
            {
              MemoryStream stream = BCRSettingsStore.Instance.LoadFromCache(filename, !(width == -1 && height == -1));
              if (stream == null)
                return response.FromStream(stream, MimeTypes.GetMimeType(".jpg"));
            }
            catch (Exception e)
            {
              // Image is not in the cache.
            }
            
            var bytes = GetPageImageBytes(id, page);
            if (bytes == null)
            {
              return HttpStatusCode.NotFound;
            }
            
            if (width == -1 && height == -1)
            {
              // Return original image.
              MemoryStream mem = new MemoryStream(bytes);
              BCRSettingsStore.Instance.SaveToCache(filename, mem, false);
              
              return response.FromStream(mem, MimeTypes.GetMimeType(".jpg"));
            }
            else
            {
              var bitmap = Image.FromStream(new MemoryStream(bytes), false, false);
              if (width == -1)
              {
                double ratio = height / (double)bitmap.Height;
                width = (int)(bitmap.Width * ratio);
              }
              else
              if (height == -1)
              {
                double ratio = width / (double)bitmap.Width;
                height = (int)(bitmap.Height * ratio);
              }
                  
              // Use high quality resize.
              var thumbnail = Resize(bitmap, width, height);
              bitmap.Dispose();
              MemoryStream stream = GetBytesFromImage(thumbnail);
              thumbnail.Dispose();
              
              BCRSettingsStore.Instance.SaveToCache(filename, stream, true);
                            
              return response.FromStream(stream, MimeTypes.GetMimeType(".jpg"));
            }
        }
        
        private static ImageProvider GetProvider(ComicBook comic)
        {
            var provider = comic.CreateImageProvider();
            if (provider == null)
            {
                return null;
            }
            if (provider.Status != ImageProviderStatus.Completed)
            {
                provider.Open(false);
            }
            return provider;
        }

        public static Comic GetComic(Guid id)
        {
          try
          {
            var comic = GetComics().First(x => x.Id == id);
            return comic.ToComic();
          }
          catch(Exception e)
          {
            //MessageBox.Show(e.ToString());
            return null;
          }
        }
        
        public static ComicBook GetComicBook(Guid id)
        {
          try
          {
            var comic = GetComics().First(x => x.Id == id);
            return comic;
          }
          catch(Exception e)
          {
            //MessageBox.Show(e.ToString());
            return null;
          }
        }

        public static IQueryable<ComicBook> GetComics()
        {
            return Plugin.Application.GetLibraryBooks().AsQueryable();
        }

        public static Response GetIcon(string key, IResponseFormatter response)
        {
            var image = ComicBook.PublisherIcons.GetImage(key);
            if (image == null)
            {
                return response.AsRedirect("/original/Views/spacer.png");
            }
            return response.FromStream(API.GetBytesFromImage(image), MimeTypes.GetMimeType(".jpg"));
        }

        public static IEnumerable<Publisher> GetPublishers()
        {
            return Plugin.Application.GetLibraryBooks().GroupBy(x => x.Publisher).Select(x =>
            {
                return x.GroupBy(y => y.Imprint).Select(y => new Publisher { Name = x.Key, Imprint = y.Key });
            }).SelectMany(x => x).OrderBy(x => x.Imprint).OrderBy(x => x.Name);
        }

        public static IEnumerable<Series> GetSeries(string publisher, string imprint)
        {
            IEnumerable<ComicBook> comics;
            if (string.Compare(publisher, "no publisher", true) == 0)
            {
                comics = Plugin.Application.GetLibraryBooks().Where(x => string.IsNullOrEmpty(x.Publisher));
            }
            else
            {
                comics = Plugin.Application.GetLibraryBooks().Where(x => string.Compare(publisher, x.Publisher, true) == 0);
                if (string.IsNullOrEmpty(imprint))
                {
                    comics = comics.Where(x => string.IsNullOrEmpty(x.Imprint));
                }
                comics = comics.Where(x => string.Compare(imprint, x.Imprint, true) == 0);
            }
            return comics.AsSeries();
        }
    }
}