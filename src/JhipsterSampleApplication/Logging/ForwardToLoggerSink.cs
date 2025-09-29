using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace JhipsterSampleApplication.Logging;

public sealed class ForwardToLoggerSink : ILogEventSink
{
    private readonly ILogger _logger;
    public ForwardToLoggerSink(ILogger logger) { _logger = logger; }
    public void Emit(LogEvent logEvent) { _logger.Write(logEvent); }
}

