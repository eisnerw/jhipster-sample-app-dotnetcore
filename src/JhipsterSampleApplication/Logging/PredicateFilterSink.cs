using System;
using Serilog.Core;
using Serilog.Events;

namespace JhipsterSampleApplication.Logging;

public sealed class PredicateFilterSink : ILogEventSink
{
    private readonly ILogEventSink _inner;
    private readonly Func<LogEvent, bool> _predicate;

    public PredicateFilterSink(ILogEventSink inner, Func<LogEvent, bool> predicate)
    {
        _inner = inner;
        _predicate = predicate;
    }

    public void Emit(LogEvent logEvent)
    {
        if (_predicate(logEvent))
        {
            _inner.Emit(logEvent);
        }
    }
}

