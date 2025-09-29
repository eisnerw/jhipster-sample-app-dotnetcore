using Serilog.Core;
using Serilog.Events;

namespace JhipsterSampleApplication.Logging;

public sealed class LevelThresholdSink : ILogEventSink
{
    private readonly ILogEventSink _inner;
    private readonly System.Func<LogEventLevel> _getThreshold;

    public LevelThresholdSink(ILogEventSink inner, System.Func<LogEventLevel> getThreshold)
    {
        _inner = inner;
        _getThreshold = getThreshold;
    }

    public void Emit(LogEvent logEvent)
    {
        var threshold = _getThreshold();
        if (logEvent.Level >= threshold)
        {
            _inner.Emit(logEvent);
        }
    }
}

