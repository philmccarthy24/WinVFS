using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace StubWebApi.Controllers
{
  [ApiController]
  [Route("correspondence/v1/api/[controller]")]
  public class TemplateController : ControllerBase
  {
    private List<FileItemInfo> _fileItems = new List<FileItemInfo>();

    private readonly ILogger<TemplateController> _logger;

    public TemplateController(ILogger<TemplateController> logger)
    {
      _logger = logger;

      _fileItems.Add(new FileItemInfo()
      {
        Name = @"pdf\MSO Integration Architecture.pdf",
        Content = System.IO.File.ReadAllBytes(@"assets\MSO Integration Architecture.pdf"),
        ContentUri = Uri.EscapeUriString("http://localhost:64634/correspondence/v1/api/template/data?fileName=pdf\\MSO Integration Architecture.pdf")
      });
      _fileItems.Add(new FileItemInfo()
      {
        Name = @"pdf\Some other Architecture.pdf",
        Content = System.IO.File.ReadAllBytes(@"assets\MSO Integration Architecture.pdf"),
        ContentUri = Uri.EscapeUriString("http://localhost:64634/correspondence/v1/api/template/data?fileName=pdf\\MSO Integration Architecture.pdf")
      });
    }

    [HttpGet]
    public IEnumerable<FileItemInfo> GetTemplateInfoList()
    {
      return _fileItems;
    }

    // GET api/<controller>/{url encoded filename}
    [HttpGet("data")]
    public IActionResult GetTemplateData([FromQuery] string fileName)
    {
      var found = _fileItems.FirstOrDefault(f => f.Name == fileName);
      if (found == null)
        return NotFound();

      var stream = new MemoryStream(found.Content);
      return File(stream, "application/octet-stream");
    }

    // GET api/<controller>
    [HttpGet("info")]
    public ActionResult<FileItemInfo> GetTemplateInfo([FromQuery] string fileName)
    {
      var fileItemInfo = _fileItems.FirstOrDefault(f => f.Name == fileName);

      if (fileItemInfo == null)
        return NotFound();

      return Ok(fileItemInfo);
    }
  }
}
