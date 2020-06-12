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
  public class FileInformationFilenameEqualityComparer : IEqualityComparer<FileInformation>
  {
    public bool Equals(FileInformation x, FileInformation y)
    {
      return x.FileName.ToUpper() == y.FileName.ToUpper();
    }

    public int GetHashCode(FileInformation obj)
    {
      return obj.FileName.ToUpper().GetHashCode();
    }
  }

  /// <summary>
  /// Allows multiple providers to be stacked / unified, giving a hybrid file system view
  /// </summary>
  public class FileSystemProviderStack : IReadOnlyFileSystemProvider
  {
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    private IReadOnlyFileSystemProvider _handler;
    private IReadOnlyFileSystemProvider _next;

    public FileSystemProviderStack(IReadOnlyFileSystemProvider handler, IReadOnlyFileSystemProvider next)
    {
      _handler = handler;
      _next = next;
    }

    public Stream GetFileStream(string filePath)
    {
      try
      {
        return _handler.GetFileStream(filePath);
      }
      catch (Exception)
      {
        // not handled by current handler, so try next level down
        if (_next == null)
          throw;
        return _next.GetFileStream(filePath);
      }
    }

    public List<FileInformation> ListItems(string parentDirectory)
    {
      List<FileInformation> dirItems = new List<FileInformation>();
      try
      {
        dirItems.AddRange(_handler.ListItems(parentDirectory));
      }
      catch (Exception e)
      {
        log.Warn($"File sys provider {_handler.GetType().Name} could not handle ListItems for path {parentDirectory}.");
      }

      if (_next != null)
      {
        dirItems.AddRange(_next.ListItems(parentDirectory));
      }

      var fiEqualityComparer = new FileInformationFilenameEqualityComparer();
      return dirItems.Distinct(fiEqualityComparer).ToList();
    }

    public FileInformation QueryItem(string fileOrDirectoryPath)
    {
      try
      {
        return _handler.QueryItem(fileOrDirectoryPath);
      }
      catch (Exception)
      {
        // not handled by current handler, so try next level down
        if (_next == null)
          throw;
        return _next.QueryItem(fileOrDirectoryPath);
      }
    }
  }
}
