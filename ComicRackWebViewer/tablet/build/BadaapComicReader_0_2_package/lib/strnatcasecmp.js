if(typeof(phpjs)==="undefined"){phpjs={}}phpjs.strnatcasecmp=function(k,j){var n=(k+"").toLowerCase();var m=(j+"").toLowerCase();var i=function(b){return b.charCodeAt(0)<=32};var l=function(q){var b=q.charCodeAt(0);return(b>=48&&b<=57)};var p=function(u,r){var w=0;var v=0;var t=0;var s;var q;for(var x=0;true;v++,t++){s=u.charAt(v);q=r.charAt(t);if(!l(s)&&!l(q)){return w}else{if(!l(s)){return -1}else{if(!l(q)){return 1}else{if(s<q){if(w===0){w=-1}}else{if(s>q){if(w===0){w=1}}else{if(s==="0"&&q==="0"){return w}}}}}}}};var f=0,d=0;var e=0,c=0;var h,g;var o;while(true){e=c=0;h=n.charAt(f);g=m.charAt(d);while(i(h)||h==="0"){if(h==="0"){e++}else{e=0}h=n.charAt(++f)}while(i(g)||g==="0"){if(g==="0"){c++}else{c=0}g=m.charAt(++d)}if(l(h)&&l(g)){if((o=p(n.substring(f),m.substring(d)))!==0){return o}}if(h==="0"&&g==="0"){return e-c}if(h<g){return -1}else{if(h>g){return +1}}++f;++d}};