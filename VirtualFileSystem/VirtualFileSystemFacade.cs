using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using DokanNet;
using log4net;
using Microsoft.Win32;
using FileAccess = DokanNet.FileAccess;

namespace Iress.VirtualFileSystem
{
  public class VirtualFileSystemFacade : IDokanOperations
  {
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    // the decorator pattern can be used to mount many providers in the same dir / give a hybrid view
    public IReadOnlyFileSystemProvider VirtualFileSystemProvider;

    public VirtualFileSystemFacade()
    {
    }

    /**
     * From the Dokan docs:
     * DOKAN_OPERATIONS.Cleanup is invoked when the function CloseHandle in the Windows API is executed.
     * If the file system application stored a file handle in the Context variable when the function DOKAN_OPERATIONS.ZwCreateFile
     * is invoked, this should be closed in the Cleanup function, not in CloseFile function. If the user application calls CloseHandle
     * and subsequently opens the same file, the CloseFile function of the file system application may not be invoked before the
     * CreateFile API is called and therefore may cause a sharing violation error since the HANDLE has not been closed.
     */
    public void Cleanup(string filename, IDokanFileInfo info)
    {
      // Not sure what the best thing to do is here.
      // VirtualFileSystemProvider.GetFileStream is called ~12 times. I don't know if there are genuinely repeated CreateFile calls
      // when you try to open up an excel (or any) file, or if it's because the http client takes too long
      // so repeat requests are sent? Ie is it a property of excel (might be worth testing on other file types) or because the http client is laggy?
      // some kind of cache mechanism required?
      // I think we are filtering out all file attribute read type operations - these are genuine file data reads (?)
      var fileStream = info.Context as Stream;
      if (fileStream != null)
      {
        lock (fileStream)
        {
          fileStream.Dispose();
          info.Context = null;
        }
      }
    }

    public void CloseFile(string filename, IDokanFileInfo info)
    {
    }

    public NtStatus CreateFile(
      string filename,
      FileAccess access,
      FileShare share,
      FileMode mode,
      FileOptions options,
      FileAttributes attributes,
      IDokanFileInfo info)
    {
      // don't allow user to create new files or directories - this fake volume is read only
      if (mode != FileMode.Open && mode != FileMode.OpenOrCreate)
        return DokanResult.AccessDenied;

      // check if the file/directory exists
      try
      {
        var fi = VirtualFileSystemProvider.QueryItem(filename);
        if (info.IsDirectory && !fi.Attributes.HasFlag(FileAttributes.Directory))
        {
          return DokanResult.NotADirectory;
        }
        if (!info.IsDirectory && access.HasFlag(FileAccess.GenericRead))
        {
          // set the memory stream on the context
          info.Context = VirtualFileSystemProvider.GetFileStream(filename);
        }
      }
      catch (FileNotFoundException)
      {
        return info.IsDirectory ? DokanResult.PathNotFound : DokanResult.FileNotFound;
      }
      catch (Exception e)
      {
        log.Error(e.Message);
        return DokanResult.Error;
      }

      return DokanResult.Success;
    }

    public NtStatus DeleteDirectory(string filename, IDokanFileInfo info)
    {
      return DokanResult.Error;
    }

    public NtStatus DeleteFile(string filename, IDokanFileInfo info)
    {
      return DokanResult.Error;
    }

    public NtStatus FlushFileBuffers(
      string filename,
      IDokanFileInfo info)
    {
      return DokanResult.Error;
    }

    public NtStatus FindFiles(
      string filename,
      out IList<FileInformation> files,
      IDokanFileInfo info)
    {
      files = new List<FileInformation>();
      
      List<FileInformation> fileSystemQueryResult;
      try
      {
        fileSystemQueryResult = VirtualFileSystemProvider.ListItems(filename);
        if (fileSystemQueryResult == null)
          throw new NullReferenceException("FS query result cannot be null");
      }
      catch (Exception e)
      {
        log.Error(e.Message);
        return DokanResult.Error;
      }
      files = fileSystemQueryResult;

      return DokanResult.Success;
    }

    public NtStatus GetFileInformation(
      string filename,
      out FileInformation fileinfo,
      IDokanFileInfo info)
    {
      fileinfo = new FileInformation { FileName = filename };
      if (filename == "\\")
      {
        fileinfo.Attributes = FileAttributes.Directory;
        fileinfo.LastAccessTime = DateTime.Now;
        fileinfo.LastWriteTime = null;
        fileinfo.CreationTime = null;
      }
      else
      {
        try
        {
          fileinfo = VirtualFileSystemProvider.QueryItem(filename);
        }
        catch (FileNotFoundException)
        {
          return DokanResult.FileNotFound;
        }
        catch (Exception e)
        {
          log.Error(e.Message);
          return DokanResult.Error;
        }
      }

      return DokanResult.Success;
    }

    public NtStatus LockFile(
      string filename,
      long offset,
      long length,
      IDokanFileInfo info)
    {
      return DokanResult.Success;
    }

    public NtStatus MoveFile(
      string filename,
      string newname,
      bool replace,
      IDokanFileInfo info)
    {
      return DokanResult.Error;
    }

    public NtStatus ReadFile(
      string filename,
      byte[] buffer,
      out int readBytes,
      long offset,
      IDokanFileInfo info)
    {
      if(info.Context == null) // memory mapped read
      {
        using (var stream = VirtualFileSystemProvider.GetFileStream(filename))
        {
          stream.Position = offset;
          readBytes = stream.Read(buffer, 0, buffer.Length);
        }
      }
      else // normal read
      {
        var stream = info.Context as Stream;
        lock (stream) //Protect from overlapped read
        {
          stream.Position = offset;
          readBytes = stream.Read(buffer, 0, buffer.Length);
        }
      }

      return DokanResult.Success;
    }

    public NtStatus SetEndOfFile(string filename, long length, IDokanFileInfo info)
    {
      return DokanResult.Error;
    }

    public NtStatus SetAllocationSize(string filename, long length, IDokanFileInfo info)
    {
      return DokanResult.Error;
    }

    public NtStatus SetFileAttributes(
      string filename,
      FileAttributes attr,
      IDokanFileInfo info)
    {
      return DokanResult.Error;
    }

    public NtStatus SetFileTime(
      string filename,
      DateTime? ctime,
      DateTime? atime,
      DateTime? mtime,
      IDokanFileInfo info)
    {
      return DokanResult.Error;
    }

    public NtStatus UnlockFile(string filename, long offset, long length, IDokanFileInfo info)
    {
      return DokanResult.Success;
    }

    public NtStatus Mounted(IDokanFileInfo info)
    {
      return DokanResult.Success;
    }

    public NtStatus Unmounted(IDokanFileInfo info)
    {
      return DokanResult.Success;
    }

    public NtStatus GetDiskFreeSpace(
      out long freeBytesAvailable,
      out long totalBytes,
      out long totalFreeBytes,
      IDokanFileInfo info)
    {
      freeBytesAvailable = 512 * 1024 * 1024;
      totalBytes = 1024 * 1024 * 1024;
      totalFreeBytes = 512 * 1024 * 1024;
      return DokanResult.Success;
    }

    public NtStatus WriteFile(
      string filename,
      byte[] buffer,
      out int writtenBytes,
      long offset,
      IDokanFileInfo info)
    {
      writtenBytes = 0;
      return DokanResult.Error;
    }

    public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features,
      out string fileSystemName, out uint maximumComponentLength, IDokanFileInfo info)
    {
      volumeLabel = "myMSOVolume";
      features = FileSystemFeatures.ReadOnlyVolume;
      fileSystemName = "HackFS";
      maximumComponentLength = 256;
      return DokanResult.Success;
    }

    public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security, AccessControlSections sections,
      IDokanFileInfo info)
    {
      security = null;
      return DokanResult.Error;
    }

    public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections,
      IDokanFileInfo info)
    {
      return DokanResult.Error;
    }

    public NtStatus EnumerateNamedStreams(string fileName, IntPtr enumContext, out string streamName,
      out long streamSize, IDokanFileInfo info)
    {
      streamName = string.Empty;
      streamSize = 0;
      return DokanResult.NotImplemented;
    }

    public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, IDokanFileInfo info)
    {
      streams = new FileInformation[0];
      return DokanResult.NotImplemented;
    }

    public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files,
      IDokanFileInfo info)
    {
      files = new FileInformation[0];
      return DokanResult.NotImplemented;
    }

  }
}
