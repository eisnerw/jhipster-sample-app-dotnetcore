// Enable nullable reference types in this file to avoid CS8632 warnings
#nullable enable
using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog.Events;
using JhipsterSampleApplication.Services;
using Serilog;
using Microsoft.Extensions.Logging;

namespace JhipsterSampleApplication.Controllers;

[ApiController]
[Authorize(Roles = JhipsterSampleApplication.Crosscutting.Constants.RolesConstants.ADMIN)]
[Route("api/admin/logging")]
public class AdminLoggingController : ControllerBase
{
    private readonly LoggingControlService _svc;
    private readonly ILogger<AdminLoggingController> _logger;

    public AdminLoggingController(LoggingControlService svc, ILogger<AdminLoggingController> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    public record SetLevelRequest(string Level, int? Minutes);
    public record SetFileRequest(string Level, int? Minutes);

    [HttpGet("level")]
    public IActionResult GetLevels()
    {
        var baseDir = AppContext.BaseDirectory;
        var dir = Path.Combine(baseDir, "logs", "on-demand");
        string filePath = Path.Combine(dir, $"log-{DateTime.Now:yyyyMMdd}.log");
        string? latestPath = null;
        long? size = null;
        bool exists = false;

        try
        {
            if (Directory.Exists(dir))
            {
                var latest = new DirectoryInfo(dir)
                    .EnumerateFiles("log-*.log", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .FirstOrDefault();
                if (latest != null)
                {
                    latestPath = latest.FullName;
                    size = latest.Length;
                }
            }
            var fi = new FileInfo(filePath);
            exists = fi.Exists;
            if (exists) size = fi.Length;
        }
        catch { /* ignore */ }

        return Ok(new
        {
            globalLevel = _svc.CurrentGlobalLevel.ToString(),
            fileLevel = _svc.CurrentFileLevel.ToString(),
            filePath,
            fileExists = exists,
            fileSize = size,
            latestFilePath = latestPath
        });
    }

    [HttpPost("level")] // e.g., { "level":"Verbose", "minutes": 10 }
    public IActionResult SetGlobalLevel([FromBody] SetLevelRequest req)
    {
        if (!Enum.TryParse<LogEventLevel>(req.Level, true, out var lvl)) return BadRequest("invalid level");
        _svc.SetGlobalLevel(lvl, req.Minutes.HasValue ? TimeSpan.FromMinutes(req.Minutes.Value) : null);
        return NoContent();
    }

    [HttpPost("file")] // e.g., { "level": "Debug", "minutes": 15 }
    public IActionResult SetFileLevel([FromBody] SetFileRequest req)
    {
        if (!Enum.TryParse<LogEventLevel>(req.Level, true, out var lvl)) return BadRequest("invalid level");
        _svc.SetFileLevel(lvl, req.Minutes.HasValue ? TimeSpan.FromMinutes(req.Minutes.Value) : null);
        return NoContent();
    }

    [HttpPost("test")]
    public IActionResult WriteTest([FromQuery] string? message = null)
    {
        var msg = message ?? "logging-test";
        // Write via both Serilog static and Microsoft logger to validate both pipelines
        Serilog.Log.ForContext("test", true).Verbose("TEST VERBOSE {Message}", msg);
        Serilog.Log.Debug("TEST DEBUG {Message}", msg);
        Serilog.Log.Information("TEST INFO {Message}", msg);
        _logger.LogTrace("TEST VERBOSE {Message}", msg);
        _logger.LogDebug("TEST DEBUG {Message}", msg);
        _logger.LogInformation("TEST INFO {Message}", msg);
        return Ok(new { ok = true, message = msg });
    }
}
