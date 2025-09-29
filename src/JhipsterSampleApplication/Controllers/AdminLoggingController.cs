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
using Microsoft.Extensions.Configuration;

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
        var cfg = (IConfiguration?)HttpContext.RequestServices.GetService(typeof(IConfiguration));
        var logsRoot = cfg?.GetValue<string>("Logging:Directory");
        if (string.IsNullOrWhiteSpace(logsRoot)) logsRoot = Path.Combine(AppContext.BaseDirectory, "logs");
        var dir = logsRoot!;
        string filePath = Path.Combine(dir, $"on-demand-{DateTime.Now:yyyyMMdd}.log");
        string? latestPath = null;
        long? size = null;
        bool exists = false;

        try
        {
            if (Directory.Exists(dir))
            {
                var latest = new DirectoryInfo(dir)
                    .EnumerateFiles("on-demand-*.log", SearchOption.TopDirectoryOnly)
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
            latestFilePath = latestPath,
            logsRoot
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
        Serilog.Log.ForContext<AdminLoggingController>().ForContext("test", true).Verbose("TEST VERBOSE {Message}", msg);
        Serilog.Log.ForContext<AdminLoggingController>().Debug("TEST DEBUG {Message}", msg);
        Serilog.Log.ForContext<AdminLoggingController>().Information("TEST INFO {Message}", msg);
        _logger.LogTrace("TEST VERBOSE {Message}", msg);
        _logger.LogDebug("TEST DEBUG {Message}", msg);
        _logger.LogInformation("TEST INFO {Message}", msg);
        return Ok(new { ok = true, message = msg });
    }

    [HttpPost("truncate")] // /api/admin/logging/truncate  (defaults to on-demand)
    public IActionResult Truncate([FromQuery] string? which = null)
    {
        // Default to truncating on-demand logs only
        var target = string.IsNullOrWhiteSpace(which) ? "on-demand" : which.Trim();
        if (!string.Equals(target, "on-demand", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { ok = false, message = "Only on-demand truncation is supported via API. Use OS tools for error logs.", target });

        var cfg = (IConfiguration?)HttpContext.RequestServices.GetService(typeof(IConfiguration));
        var logsRoot = cfg?.GetValue<string>("Logging:Directory");
        if (string.IsNullOrWhiteSpace(logsRoot)) logsRoot = Path.Combine(AppContext.BaseDirectory, "logs");

        var dir = logsRoot!;
        if (!Directory.Exists(dir)) return NotFound(new { ok = false, message = "log directory not found", dir });
        var today = $"on-demand-{DateTime.Now:yyyyMMdd}.log";
        var path = Path.Combine(dir, today);
        if (!System.IO.File.Exists(path))
        {
            var latest = new DirectoryInfo(dir).EnumerateFiles("on-demand-*.log").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
            if (latest == null) return NotFound(new { ok = false, message = "no on-demand log file found to truncate", dir });
            path = latest.FullName;
        }
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        fs.SetLength(0);
        return Ok(new { ok = true, truncated = path });
    }

    [HttpPost("generate-error")] // /api/admin/logging/generate-error?mode=log|throw
    public IActionResult GenerateError([FromQuery] string? mode = null)
    {
        var m = string.IsNullOrWhiteSpace(mode) ? "log" : mode.ToLowerInvariant();
        // Add some verbose context first
        Serilog.Log.Verbose("ERROR TEST: preparing context A");
        _logger.LogTrace("ERROR TEST: preparing context B");
        _logger.LogDebug("ERROR TEST: preparing context C");

        if (m == "throw")
        {
            // This will flow through the middleware pipeline and be logged as an unhandled error
            throw new InvalidOperationException("Intentional test exception from /api/admin/logging/generate-error?mode=throw");
        }
        else
        {
            var ex = new ApplicationException("Intentional test error (logged)");
            Serilog.Log.Error(ex, "ERROR TEST: logged error in generate-error endpoint");
            _logger.LogError(ex, "ERROR TEST (MEL): logged error in generate-error endpoint");
            return Problem(detail: "Logged an intentional test error.", statusCode: 500, title: "Test Error Logged");
        }
    }
}
