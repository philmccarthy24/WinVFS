using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using DokanTest;
using Microsoft.Win32;
using System.Configuration;
using System.Linq;
using System.Reflection;
using Iress.VirtualFileSystem;
using Iress.VirtualFileSystem.Providers;
using log4net;
using VirtualFileSystem;

namespace DokanTest
{


  internal class Program
  {
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    private static void Main()
    {
      // to stack file systems, the next item is another FileSystemProviderStack
      var hybridFileSystem = new FileSystemProviderStack(
        new WebServiceFileSystemProvider(new Uri(ConfigurationManager.AppSettings["httpclienturi"])),
        new FileSystemProviderStack(
          new DirectoryProxyFileSystemProvider(ConfigurationManager.AppSettings["dirtoproxy"]),
            null));

      try
      {
        using (var vfsManager = new VFSManager(ConfigurationManager.AppSettings["mountpoint"], hybridFileSystem))
        {
          Console.WriteLine("Mounted, press any key to unmount and exit...");
          Console.ReadKey(false);
        }
      }
      catch (Exception e)
      {
        log.Error(e);
      }
    }
  }
}