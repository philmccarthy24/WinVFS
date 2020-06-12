using DokanNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace Iress.VirtualFileSystem.Providers
{
  /// <summary>
  /// This class proxies file system calls to another directory on the local disk
  /// </summary>
  public class DirectoryProxyFileSystemProvider : IReadOnlyFileSystemProvider
  {
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    private readonly string _directoryToProxy;

    public DirectoryProxyFileSystemProvider(string localPathToProxy)
    {
      _directoryToProxy = localPathToProxy;
    }

    public Stream GetFileStream(string filePath)
    {
      var trimmedFilePath = filePath.TrimStart(new[] { '\\' });
      var fileToRead = Path.Combine(_directoryToProxy, trimmedFilePath);
      if (!File.Exists(fileToRead))
        throw new FileNotFoundException($"File requested {trimmedFilePath} doesn't exist on this FS");

      return new FileStream(fileToRead, FileMode.Open, System.IO.FileAccess.Read);
    }

    public List<FileInformation> ListItems(string parentDirectory)
    {
      var trimmedFilePath = parentDirectory.TrimStart(new[] { '\\' });
      var dirToList = Path.Combine(_directoryToProxy, trimmedFilePath);
      var proxiedItems = Directory.EnumerateFileSystemEntries(dirToList).Select(i =>
      {
        var fileInfo = new FileInfo(i);
        return new FileInformation()
        {
          FileName = fileInfo.Name,
          Attributes = (fileInfo.Attributes.HasFlag(FileAttributes.Directory)
            ? FileAttributes.Directory
            : 0) | FileAttributes.NotContentIndexed | FileAttributes.ReadOnly,
          CreationTime = fileInfo.CreationTime,
          LastAccessTime = fileInfo.LastAccessTime,
          LastWriteTime = fileInfo.LastWriteTime,
          Length = fileInfo.Attributes.HasFlag(FileAttributes.Directory) ? 0 : fileInfo.Length
        };
      });

      return proxiedItems.ToList();
    }

    public FileInformation QueryItem(string fileOrDirectoryPath)
    {
      var trimmedFilePath = fileOrDirectoryPath.TrimStart(new[] {'\\'});
      var fileOrDirToQuery = Path.Combine(_directoryToProxy, trimmedFilePath);

      if (File.Exists(fileOrDirToQuery) || Directory.Exists(fileOrDirToQuery))
      {
        var fileInfo = new FileInfo(fileOrDirToQuery);
        return new FileInformation()
        {
          FileName = fileOrDirectoryPath,
          Attributes = (fileInfo.Attributes.HasFlag(FileAttributes.Directory)
            ? FileAttributes.Directory
            : 0) | FileAttributes.NotContentIndexed | FileAttributes.ReadOnly,
          CreationTime = fileInfo.CreationTime,
          LastAccessTime = fileInfo.LastAccessTime,
          LastWriteTime = fileInfo.LastWriteTime,
          Length = fileInfo.Attributes.HasFlag(FileAttributes.Directory) ? 0 : fileInfo.Length
        };
      }
      else throw new FileNotFoundException($"File {trimmedFilePath} requested doesn't exist on this FS");
    }
  }
}
