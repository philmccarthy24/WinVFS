using System;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace StubWebApi
{
  public class FileItemInfo
  {
    public string Name { get; set; } // includes relative path

    public string Hash
    {
      get
      {
        if (Content == null)
          return null;
        SHA1 sha = new SHA1CryptoServiceProvider();
        return BitConverter.ToString(sha.ComputeHash(Content)).Replace("-", "");
      }
    }

    public long Length => Content.Length;

    public string ContentUri { get; set; } // base-relative uri

    [JsonIgnore]
    public byte[] Content { get; set; }
  }
}
