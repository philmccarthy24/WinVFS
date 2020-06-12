using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DokanNet;
using Iress.VirtualFileSystem;
using log4net;

namespace VirtualFileSystem
{
  public class VFSManager : IDisposable
  {
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    private readonly bool _cleanupMountDirOnExit;

    private Task _dokanTask;

    private readonly string _mountPoint;

    public VFSManager(string mountPoint, IReadOnlyFileSystemProvider vfsProvider)
    {
      _mountPoint = mountPoint;

      if (Directory.Exists(mountPoint))
      {
        _cleanupMountDirOnExit = false;

        if (Directory.EnumerateFileSystemEntries(mountPoint).Count() > 0)
        {
          log.Error($"Mountpoint directory {mountPoint} contains existing files");
          throw new InvalidOperationException($"Mount point {mountPoint} must not contain existing files.");
        }
          
      }
      else
      {
        Directory.CreateDirectory(mountPoint);
        _cleanupMountDirOnExit = true;
        log.Info($"Mountpoint directory {mountPoint} created.");
      }

      log.Info($"Mounting virtual filesystem...");
      _dokanTask = Task.Run(() =>
      {
        var fsFacade = new VirtualFileSystemFacade();
        fsFacade.VirtualFileSystemProvider = vfsProvider;
        fsFacade.Mount(mountPoint); // to allow mount to be viewed over the network, we need to use NetworkDrive option here, and also set the option using dokanctl /i n. This shouldn't be required.
      });

    }

    public void UnmountVFS()
    {
      if (_dokanTask != null)
      {
        try
        {
          log.Info($"Unmounting virtual filesystem at {_mountPoint}...");
          Dokan.RemoveMountPoint(_mountPoint);
          if (_dokanTask.Wait(TimeSpan.FromSeconds(10)))
          {
            log.Info($"Unmount of {_mountPoint} was successful.");
          }

          if (_cleanupMountDirOnExit)
          {
            Directory.Delete(_mountPoint);
            log.Info($"Mountpoint directory {_mountPoint} removed.");
          }
        }
        catch (Exception e)
        {
          log.Error("Unmount failed: " + e.Message);
        }
        finally
        {
          _dokanTask = null;
        }
      }
    }

    public void Dispose()
    {
      UnmountVFS();
    }
  }
}
