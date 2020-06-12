using DokanNet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Newtonsoft.Json;

namespace Iress.VirtualFileSystem.Providers
{
  /// <summary>
  /// The kind of DTO we would be getting back from a web service which provides template content. Other fields
  /// might be present.
  /// </summary>
  public class FileItemInfo
  {
    public string Name { get; set; } // includes relative path
    public string Hash { get; set; }
    public long Length { get; set; }
    public string ContentUri { get; set; }
  }

  public class WebServiceFileSystemProvider : IReadOnlyFileSystemProvider
  {
    private HttpClient _webServiceClient = new HttpClient();

    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    private Dictionary<string, FileItemInfo> _cachedFileList;
    private Dictionary<string, Tuple<string, byte[]>> _fileDataCache;

    private SHA1 _sha1 = new SHA1CryptoServiceProvider();

    private readonly string controllerUriPath = "correspondence/v1/api/template/";

    public WebServiceFileSystemProvider(Uri baseUri)
    {
      _webServiceClient.BaseAddress = baseUri;

      _cachedFileList = new Dictionary<string, FileItemInfo>();
      _fileDataCache = new Dictionary<string, Tuple<string, byte[]>>();

      // get the initial file list
      RefreshCachedTemplateList();
    }

    private void RefreshCachedTemplateList()
    {
      try
      {
        var result = _webServiceClient.GetAsync(controllerUriPath + "list").Result; // base includes api/templates ?
        var fileInfos = JsonConvert.DeserializeObject<List<FileItemInfo>>(result.Content.ReadAsStringAsync().Result);
        _cachedFileList = fileInfos.ToDictionary(f => f.Name);
      }
      catch (Exception e)
      {
        log.Error($"Could not refresh list of template info objects: {e.Message}");
      }
    }

    private void RefreshCachedTemplateItemInfo(string relFilePath)
    {
      var result = _webServiceClient.GetAsync(controllerUriPath + "info?fileName=" + Uri.EscapeUriString(relFilePath)).Result; // base includes api/templates ?
      if (result.StatusCode == HttpStatusCode.NotFound)
        throw new FileNotFoundException($"File info for {relFilePath} could not be retrieved from web service");
      var fileInfo = JsonConvert.DeserializeObject<FileItemInfo>(result.Content.ReadAsStringAsync().Result);
      _cachedFileList[relFilePath] = fileInfo;
    }

    public Stream GetFileStream(string filePath)
    {
      var trimmedFilePath = filePath.TrimStart(new[] { '\\' });

      // do a GET on the FileItemInfo item uri to get latest hash, and compare with what we have cached.
      RefreshCachedTemplateItemInfo(trimmedFilePath);

      bool hasDataCached = _fileDataCache.ContainsKey(trimmedFilePath);
      if (!hasDataCached || _fileDataCache[trimmedFilePath].Item1.ToLower() != _cachedFileList[trimmedFilePath].Hash.ToLower())
      {
        // the hashes don't match or we don't haven't yet cached the file data, so do a full file get
        if (hasDataCached)
          log.Info($"File {filePath} updated on server; hash of cached data does not match");
        else 
          log.Info($"File {filePath} contents not yet cached");

        var fileItem = _cachedFileList[trimmedFilePath];
        var contentUri = fileItem.ContentUri.StartsWith(_webServiceClient.BaseAddress.ToString()) ? fileItem.ContentUri.Substring(_webServiceClient.BaseAddress.ToString().Length) : fileItem.ContentUri;
        var fileData = _webServiceClient.GetByteArrayAsync(contentUri).Result;
        var fileDataHash = BitConverter.ToString(_sha1.ComputeHash(fileData)).Replace("-", "");
        _fileDataCache[trimmedFilePath] = new Tuple<string, byte[]>(fileDataHash, fileData);
        log.Info($"File {filePath} with hash {fileDataHash} cached.");
      }

      return new MemoryStream(_fileDataCache[trimmedFilePath].Item2);
    }

    public List<FileInformation> ListItems(string parentDirectory)
    {
      // refresh cached template list
      RefreshCachedTemplateList();

      // get the items in the specified directory
      if (parentDirectory != "\\")
        parentDirectory += "\\";

      var dirItems = new List<FileInformation>();
      var itemNames = new HashSet<string>();
      foreach (var fileItem in _cachedFileList.Values)
      {
        var itemName = "\\" + fileItem.Name;
        if (itemName.StartsWith(parentDirectory))
        {
          itemName = itemName.Substring(parentDirectory.Length);
          int dirSepIdx = itemName.IndexOf('\\');
          if (dirSepIdx != -1)
          {
            // fileItem is in a subdir, so truncate to current level dir name
            itemName = itemName.Substring(0, dirSepIdx);
          }

          if (!itemNames.Contains(itemName))
          {
            itemNames.Add(itemName);
            dirItems.Add(new FileInformation()
            {
              FileName = itemName,
              Attributes = (dirSepIdx == -1 ? 0 : FileAttributes.Directory) | FileAttributes.ReadOnly | FileAttributes.NotContentIndexed,
              CreationTime = DateTime.Now,
              Length = dirSepIdx == -1 ? fileItem.Length : 0
            });
          }
        }
      }

      return dirItems;
    }

    public FileInformation QueryItem(string fileOrDirectoryPath)
    {
      var trimmedFilePath = fileOrDirectoryPath.TrimStart(new[] { '\\' });

      // TODO: use a filter query string to get a subset (or even single item) rather than pulling back the whole dir list each time a
      // TODO: file or directory item is queried
      RefreshCachedTemplateList();

      var fileListMatch = _cachedFileList.Values.FirstOrDefault(fi => fi.Name.StartsWith(trimmedFilePath));
      if (fileListMatch == null)
        throw new FileNotFoundException($"File {trimmedFilePath} requested doesn't exist on this FS");

      // if fileOrDirectoryPath is a dir, will potentially get more than one match. if fileOrDirectoryPath is a file
      // the match will be exact.
      return new FileInformation()
      {
        FileName = fileOrDirectoryPath,
        Attributes = (trimmedFilePath == fileListMatch.Name ? 0 : FileAttributes.Directory) | FileAttributes.NotContentIndexed | FileAttributes.ReadOnly,
        CreationTime = DateTime.Now,
        Length = (trimmedFilePath == fileListMatch.Name ? fileListMatch.Length : 0)
      };
    }
  }
}
