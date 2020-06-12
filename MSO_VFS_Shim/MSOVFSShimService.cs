using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using Iress.VirtualFileSystem.Providers;
using log4net;
using VirtualFileSystem;

namespace MSO_VFS_Shim
{
  public partial class MSOVFSShimService : ServiceBase
  {
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    private VFSManager _vfsManager;

    public MSOVFSShimService()
    {
      InitializeComponent();
    }

    protected override void OnStart(string[] args)
    {
      if (_vfsManager == null)
      {
        var hybridFileSystem = new FileSystemProviderStack(
          new WebServiceFileSystemProvider(new Uri(ConfigurationManager.AppSettings["httpclienturi"])),
          new FileSystemProviderStack(
            new DirectoryProxyFileSystemProvider(ConfigurationManager.AppSettings["dirtoproxy"]),
            null));

        try
        {
          _vfsManager = new VFSManager(ConfigurationManager.AppSettings["mountpoint"], hybridFileSystem);
        }
        catch (Exception e)
        {
          log.Error(e);
          _vfsManager = null;
        }
      }
    }

    protected override void OnStop()
    {
      if (_vfsManager != null)
      {
        _vfsManager.Dispose();
        _vfsManager = null;
      }
    }
  }
}
