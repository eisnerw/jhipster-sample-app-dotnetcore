using System;
using Serilog.Core;
using Serilog.Events;

namespace JhipsterSampleApplication.Logging;

/// <summary>
/// Forwards events to an inner sink, demoting matching events to a lower level (e.g., Information -> Verbose).
/// Use to treat framework Information logs as Verbose so they are buffered/replayed only on error.
/// </summary>
public sealed class LevelDemotionSink : ILogEventSink
{
    private readonly ILogEventSink _inner;
    private readonly Predicate<LogEvent> _shouldDemote;
    private readonly LogEventLevel _targetLevel;

    public LevelDemotionSink(ILogEventSink inner, Predicate<LogEvent> shouldDemote, LogEventLevel targetLevel)
    {
        _inner = inner;
        _shouldDemote = shouldDemote;
        _targetLevel = targetLevel;
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent == null) return;
        if (_shouldDemote(logEvent) && logEvent.Level > _targetLevel)
        {
            var props = new System.Collections.Generic.List<LogEventProperty>(logEvent.Properties.Count + 3);
            foreach (var kv in logEvent.Properties)
                props.Add(new LogEventProperty(kv.Key, kv.Value));
            props.Add(new LogEventProperty("demoted", new ScalarValue(true)));
            props.Add(new LogEventProperty("demoted.from", new ScalarValue(logEvent.Level.ToString())));
            // Ensure any precomputed textual level reflects the new level for tailers that prefer it
            props.RemoveAll(p => p.Name == "level_text");
            props.Add(new LogEventProperty("level_text", new ScalarValue(_targetLevel.ToString())));
            var demoted = new LogEvent(logEvent.Timestamp, _targetLevel, logEvent.Exception, logEvent.MessageTemplate, props);
            _inner.Emit(demoted);
        }
        else
        {
            _inner.Emit(logEvent);
        }
    }
}
