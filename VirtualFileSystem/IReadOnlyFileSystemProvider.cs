using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DokanNet;

namespace Iress.VirtualFileSystem
{
  public interface IReadOnlyFileSystemProvider
  {
    List<FileInformation> ListItems(string parentDirectory);
    FileInformation QueryItem(string fileOrDirectoryPath);
    Stream GetFileStream(string filePath);
  }
}
