﻿/*
 * Created by SharpDevelop.
 * User: jeroen
 * Date: 03/14/2013
 * Time: 21:44
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using BCR;
using System.Data.SQLite;
using cYo.Projects.ComicRack.Viewer;
using cYo.Projects.ComicRack.Engine;
using cYo.Projects.ComicRack.Engine.Database;


namespace ComicRackWebViewer
{
  
  
  
  /// <summary>
  /// MainForm provides the user interface of this plugin.
  /// </summary>
  public partial class MainForm : Form
  {
    
    private static ManualResetEvent mre = new ManualResetEvent(false);
    private static BCR.WebHost host;
    private int? actualPort;
    private bool allowExternal;
    private Guid libraryGuid;
    private bool cacheSizesInitialized = false;
    
    public MainForm()
    {
      //
      // The InitializeComponent() call is required for Windows Forms designer support.
      //
      InitializeComponent();
      
      string path = BCRInstaller.Instance.installFolder + "about.html";
      
      
      webBrowserAbout.Url = new Uri("file://" + path);
      
      textBoxPort.Text = Database.Instance.globalSettings.webserver_port.ToString();
      actualPort = Database.Instance.globalSettings.webserver_port;
      allowExternal = true; //ImageCache.Instance.webserver_allow_external;

      string s = "cYo.Projects.ComicRack.Engine.Database.ComicLibraryListItem";
      ComicListItem item = Program.Database.ComicLists.GetItems<ComicListItem>(false).FirstOrDefault((ComicListItem cli) => cli.GetType().ToString() == s);
      if (item != null)
      {
        libraryGuid = item.Id;
      }
      
      FillComboHomeList();
      FillComboUsers();
                  
      SetEnabledState();
    }
    
    private void SetEnabledState()
    {
      if (buttonStart == null)
      {
        return;
      }
      buttonStart.Enabled = actualPort.HasValue;
      textBoxPort.Enabled = host == null;

      if (host == null)
      {
        buttonStart.Text = "Start";
        labelStatus.Text = "The web server is not running.";
        
      }
      else
      {
        buttonStart.Text = "Stop";
        labelStatus.Text = "The web server is running.";
        /*
        int port = actualPort.Value;
        List<Uri> uris = GetUriParams(port).ToList();
        foreach (var uri in uris)
        {
          Status.Text += uri.ToString() + " ";
        }
        
        if (allowExternal)
        {
          Status.Text += string.Format("http://+:{0}/", port);
        }
        */
      }
      System.Windows.Input.Mouse.SetCursor(null);
    }

    public void StartService()
    {
      textBoxPort.Enabled = false;
      System.Windows.Input.Mouse.SetCursor(System.Windows.Input.Cursors.Wait);
      Task.Factory.StartNew(() => LoadService());
      labelStatus.Text = "Starting";
    }

    public void LoadService()
    {
      if (host != null)
      {
        StopService();
      }

      int port = actualPort.Value;
      
      
      host = new WebHost(new Bootstrapper(), allowExternal, port, GetUriParams(port));

      try
      {
        host.Start();
        this.BeginInvoke(new Action(SetEnabledState));
        mre.Reset();
        mre.WaitOne();

        host.Stop();
      }
      catch (Exception)
      {
        System.Windows.Forms.MessageBox.Show("Error in url binding");
        StopService();
        throw;
      }
      finally
      {
        host = null;
        this.BeginInvoke(new Action(SetEnabledState));
      }
    }


    private static IEnumerable<string> GetLocalIPs()
    {
      return Dns.GetHostAddresses(Dns.GetHostName()).Where(x => x.AddressFamily == AddressFamily.InterNetwork).Select(x => x.ToString());
    }

    private Uri[] GetUriParams(int port)
    {
        var uriParams = new List<Uri>();
        
        // No need to enumerate addresses, as the httplistener will respond to all requests regardless of host name.
        if (allowExternal)
          return uriParams.ToArray();
        
        // Enumerate all local addresses.
        string hostName = Dns.GetHostName();

        // Host name URI
        string hostNameUri = string.Format("http://{0}:{1}", Dns.GetHostName(), port);
        uriParams.Add(new Uri(hostNameUri));

        // Host address URI(s)
        var hostEntry = Dns.GetHostEntry(hostName);
        foreach (var ipAddress in hostEntry.AddressList)
        {
            if (ipAddress.AddressFamily == AddressFamily.InterNetwork)  // IPv4 addresses only
            {
                var addrBytes = ipAddress.GetAddressBytes();
                string hostAddressUri = string.Format("http://{0}.{1}.{2}.{3}:{4}", addrBytes[0], addrBytes[1], addrBytes[2], addrBytes[3], port);
                uriParams.Add(new Uri(hostAddressUri));
            }
        }

        // Localhost URI
        uriParams.Add(new Uri(string.Format("http://localhost:{0}", port)));
        
        return uriParams.ToArray();
    }
    
    public void StopService()
    {
      mre.Set();
    }
    
    private bool IsCurrentlyRunningAsAdmin()
    {
        bool isAdmin = false;
        WindowsIdentity currentIdentity = WindowsIdentity.GetCurrent();
        if (currentIdentity != null)
        {
            WindowsPrincipal pricipal = new WindowsPrincipal(currentIdentity);
            isAdmin = pricipal.IsInRole(WindowsBuiltInRole.Administrator);
            pricipal = null;
        }
        return isAdmin;
    }
    

    ///////////////////////////////////////////////////////////////////////
    /// WebServer TabPage
    void ButtonStartClick(object sender, System.EventArgs e)
    {
      if (host != null)
      {
        StopService();
      }
      else
      {
        if (IsCurrentlyRunningAsAdmin())
        {
          Database.Instance.globalSettings.webserver_port = actualPort.HasValue ? actualPort.Value : 8080;
          Database.Instance.globalSettings.Save();
          
          StartService();
        }
        else
        {
          System.Windows.Forms.MessageBox.Show("Sorry!, you must be running ComicRack with administrator privileges.");
        }
      }
    }
    
    void TextBoxPortTextChanged(object sender, EventArgs e)
    {
      if (string.IsNullOrEmpty(textBoxPort.Text))
      {
          return;
      }
      int x;
      if (int.TryParse(textBoxPort.Text, out x))
      {
          actualPort = x;
      }
      else
      {
          actualPort = null;
      }
      SetEnabledState();
    }


    ///////////////////////////////////////////////////////////////////////
    /// Users TabPage
    void FillComboUsers()
    {
      comboBoxUsers.Items.Clear();
      using (SQLiteDataReader reader = Database.Instance.ExecuteReader("SELECT id, username FROM user;"))
      {
        while (reader.Read())
        {
          comboBoxUsers.Items.Add(new ComboUserItem(reader.GetString(1), reader.GetInt32(0)));
        }
      }
      
      if (comboBoxUsers.Items.Count > 0)
        comboBoxUsers.SelectedIndex = 0;
    }
    
    void FillComboHomeList()
    {
      comboTreeHomeList.Nodes.Clear();
      
      var nodes = Program.Database.ComicLists.Select(c => c.ToComboTreeNode());
      comboTreeHomeList.Nodes.AddRange(nodes);
      if (comboTreeHomeList.Nodes.Count > 0)
      {
        comboTreeHomeList.SelectedNode = comboTreeHomeList.Nodes[0];
      }
      
      //Guid id = new Guid(string);
      //var list = Program.Database.ComicLists.FindItem(id);
     
    }
    
    void ButtonAddUserClick(object sender, EventArgs e)
    {
      InputBoxValidation validation = delegate(string val) {
        if (val.Length < 4)
          return "The username must contain at least 4 characters.";
        
        if (UserDatabase.GetUserId(val) != -1)
          return "The username already exists.";
        
        return "";
      };
      
      string name = "";
      var result = InputBox.Show("Add User", "Enter username (min. 4 characters):", ref name, validation);
      if (result == System.Windows.Forms.DialogResult.OK)
      {
        UserDatabase.AddUser(name, "1234567890");
        FillComboUsers();
        comboBoxUsers.SelectedIndex = comboBoxUsers.FindString(name);
        System.Windows.Forms.MessageBox.Show("User added.\nDon't forget to set a password and choose a home list.", "Add User", MessageBoxButtons.OK, MessageBoxIcon.Information);
        ButtonChangePasswordClick(null, null);
      }
      
      /*
      ComicListItemFolder userFolder = new ComicListItemFolder(name);
      ComicIdListItem readingList = new ComicIdListItem("Reading");
      userFolder.Items.Add(readingList);
      ComicIdListItem favoritesList = new ComicIdListItem("Favorites");
      userFolder.Items.Add(favoritesList);
            
      ((ComicLibrary)Program.Database).ComicLists.Add(userFolder);
      */
    }
    
    
    void ButtonRemoveUserClick(object sender, System.EventArgs e)
    {
      ComboUserItem item = (ComboUserItem)comboBoxUsers.SelectedItem;
      if (item == null)
      {
        return;
      }
      
      if (DialogResult.Yes == System.Windows.Forms.MessageBox.Show("Are you sure you want to remove user '"+item.Text+"'?", "Remove user", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2))
      {
        // remove user
        UserDatabase.RemoveUser(item.UserId);
        System.Windows.Forms.MessageBox.Show("User removed.", "Remove User", MessageBoxButtons.OK, MessageBoxIcon.Information);
        FillComboUsers();
      }
    }
    
    void ButtonChangePasswordClick(object sender, EventArgs e)
    {
      ComboUserItem item = (ComboUserItem)comboBoxUsers.SelectedItem;
      if (item == null)
      {
        return;
      }
      
      // Password Strength: http://xkcd.com/936/
      InputBoxValidation validation = delegate(string val) {
        if (val.Length < 8)
          return "Password must contain at least 8 characters.";
        
        return "";
      };

      string password = "";
      if (DialogResult.OK == InputBox.Show("Change Password", "Enter a new password (min. 8 characters):", ref password, validation))
      {
        // change password
        UserDatabase.SetPassword(item.UserId, password);
      }
    }
    
    void ComboTreeHomeListSelectedNodeChanged(object sender, EventArgs e)
    {
      ComboUserItem item = (ComboUserItem)comboBoxUsers.SelectedItem;
      if (item == null)
      {
        return;
      }
      
      if (comboTreeHomeList.SelectedNode != null)
      {
        string listId = comboTreeHomeList.SelectedNode.Tag.ToString();
        Database.Instance.ExecuteNonQuery("UPDATE user_settings SET home_list_id='" + listId + "' WHERE user_id=" + item.UserId + ";");
      }
    }
    
    
    void ComboBoxUsersSelectedIndexChanged(object sender, EventArgs e)
    {
      // Apply values to previously selected user.
      
      // Show values for current user.
      ComboUserItem item = (ComboUserItem)comboBoxUsers.SelectedItem;
      if (item == null)
      {
        return;
      }
      
      using (SQLiteDataReader reader = Database.Instance.ExecuteReader("SELECT id, username, fullname, home_list_id FROM user JOIN user_settings ON user.id=user_settings.user_id WHERE user.id = " + item.UserId + " LIMIT 1;"))
      {
        if (reader.Read())
        {
          textBoxUsername.Text = reader.GetString(1);
          if (!reader.IsDBNull(2))
            textBoxFullName.Text = reader.GetString(2);
          else
            textBoxFullName.Text = "";

          Guid listId;
          
          
          if (!reader.IsDBNull(3))
          {
            try
            {
              listId = new Guid(reader.GetString(3));
              // Check if list still exists, if not, select Library and update database
              var list = Program.Database.ComicLists.FindItem(listId);
              if (list == null)
              {
                listId = libraryGuid;
              }
            }
            catch (Exception ex)
            {
              // invalid Guid format or reader.GetString(3) failed, just ignore and use main library list...
              Console.WriteLine(ex.ToString());
              listId = libraryGuid;
            }
          }
          else
          {
            listId = libraryGuid;
          }
          
          comboTreeHomeList.SelectedNode = comboTreeHomeList.Nodes.FirstOrDefault((ComboTreeNode ctn) => ctn.Tag.Equals(listId));
        }
      }  
    }
    
    
    void ButtonClearPageCacheClick(object sender, EventArgs e)
    {
     var cursor = this.Cursor; 
     this.Cursor = Cursors.WaitCursor;
          
     ImageCache.Instance.ClearPageCache();
     
     UpdateCacheSizes();
     
     this.Cursor = cursor;
    }
    

    
    void TextBoxFullNameValidated(object sender, EventArgs e)
    {
      ComboUserItem item = (ComboUserItem)comboBoxUsers.SelectedItem;
      if (item == null)
      {
        return;
      }
      
      UserDatabase.SetFullName(item.UserId, ((System.Windows.Forms.TextBox)sender).Text);
    }
    
    public void UpdateCacheSizes()
    {
      labelCacheSize.Text = String.Format("Used cache size: Pages {0} MB / Thumbnails {1} MB", (int)(ImageCache.Instance.GetPageCacheSize()/(1024*1024)), (int)(ImageCache.Instance.GetThumbnailsCacheSize()/(1024*1024)));
    }
    
    void ButtonClearThumbnailsCacheClick(object sender, EventArgs e)
    {
      var cursor = this.Cursor; 
      this.Cursor = Cursors.WaitCursor;
        
      ImageCache.Instance.ClearThumbnailsCache();
      
      UpdateCacheSizes();
      
      this.Cursor = cursor;
    }

    
    void TabControlSelectedIndexChanged(object sender, EventArgs e)
    {
      if (tabControl.SelectedIndex == 2 && !cacheSizesInitialized)
      {
        cacheSizesInitialized = true;
        labelCacheSize.Text = "Calculating cache size....";
        var cursor = this.Cursor; 
        this.Cursor = Cursors.WaitCursor;
            
        UpdateCacheSizes();
       
        this.Cursor = cursor;
      }
    }
  }
  
  // System.Resources.MissingManifestResourceException: Could not find any resources appropriate for the specified culture or the neutral culture.
  // http://stackoverflow.com/questions/2058441/could-not-find-any-resources-appropriate-for-the-specified-culture-or-the-neutra
  // To resolve this problem, move all of the other class definitions so that they appear after the form's class definition.
  
  public class ComboUserItem
  {
      public string Text { get; set; }
      public int UserId { get; set; }
  
      public ComboUserItem(string text, int userid)
      {
        Text = text;
        UserId = userid;
      }
      
      public override string ToString()
      {
        return Text;
      }
  }
  
  public static class ComboTreeNodeExtensions
  {
    public static ComboTreeNode ToComboTreeNode(this ComicListItem x)
    {
      ComboTreeNode node = new ComboTreeNode(x.Name);
      node.Tag = x.Id;
          
      ComicListItemFolder folderList = x as ComicListItemFolder;
      if (folderList != null)
      {
        node.Nodes.AddRange(folderList.Items.Select(c => c.ToComboTreeNode()));
      }
      
      return node;
    }
  }
  
  
}
