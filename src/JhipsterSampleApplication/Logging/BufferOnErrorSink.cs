// Enable nullable reference types for this file
#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Serilog.Core;
using Serilog.Events;

namespace JhipsterSampleApplication.Logging;

/// <summary>
/// Buffers low-level events (Verbose/Debug; optionally Information) per trace.id and
/// replays them to the forward sink if an Error/Fatal occurs for the same trace.id.
/// </summary>
public sealed class BufferOnErrorSink : ILogEventSink, IDisposable
{
    private readonly ILogEventSink _forward;
    private readonly int _capacity;
    private readonly TimeSpan _ttl;
    private readonly bool _bufferInformation;
    private readonly Func<bool>? _isBufferingEnabled;
    private readonly ConcurrentDictionary<string, Ring> _buffers = new();
    private readonly System.Threading.Timer _sweeper;

    public BufferOnErrorSink(
        ILogEventSink forward,
        int capacity = 200,
        TimeSpan? ttl = null,
        bool bufferInformation = false,
        Func<bool>? isBufferingEnabled = null)
    {
        _forward = forward;
        _capacity = Math.Max(10, capacity);
        _ttl = ttl ?? TimeSpan.FromSeconds(60);
        _bufferInformation = bufferInformation;
        _isBufferingEnabled = isBufferingEnabled;
        _sweeper = new System.Threading.Timer(_ => Sweep(), null, _ttl, _ttl);
    }

    public void Emit(LogEvent logEvent)
    {
        // Skip buffering when disabled (e.g., global level set to Debug/Verbose)
        if (_isBufferingEnabled != null && !_isBufferingEnabled())
        {
            _forward.Emit(logEvent);
            return;
        }

        string? traceId = null;
        if (logEvent.Properties.TryGetValue("trace.id", out var val))
        {
            // Property values are rendered with quotes by default; strip them.
            var s = val.ToString();
            if (!string.IsNullOrEmpty(s)) traceId = s.Trim('"');
        }

        if (string.IsNullOrEmpty(traceId))
        {
            _forward.Emit(logEvent);
            return;
        }

        var level = logEvent.Level;

        if (level >= LogEventLevel.Warning)
        {
            // On first Error/Fatal, flush buffer for this trace
            if (level >= LogEventLevel.Error && _buffers.TryRemove(traceId, out var ring))
            {
                foreach (var buffered in ring.EventsInOrder())
                {
                    var props = new List<LogEventProperty>(buffered.Properties.Count + 1);
                    foreach (var p in buffered.Properties)
                        props.Add(new LogEventProperty(p.Key, p.Value));
                    props.Add(new LogEventProperty("replayed", new ScalarValue(true)));
                    var replayed = new LogEvent(buffered.Timestamp, buffered.Level, buffered.Exception, buffered.MessageTemplate, props);
                    _forward.Emit(replayed);
                }
            }
            _forward.Emit(logEvent);
            return;
        }

        // Buffer Verbose/Debug (and optionally Information)
        if (level == LogEventLevel.Information && !_bufferInformation)
        {
            // Forward live to keep high-level narratives without buffering
            _forward.Emit(logEvent);
            return;
        }

        var buf = _buffers.GetOrAdd(traceId, _ => new Ring(_capacity));
        buf.Add(logEvent);
    }

    private void Sweep()
    {
        var cutoff = DateTimeOffset.UtcNow - _ttl;
        foreach (var kv in _buffers)
        {
            if (kv.Value.LastSeen < cutoff)
            {
                _buffers.TryRemove(kv.Key, out _);
            }
        }
    }

    public void Dispose() => _sweeper.Dispose();

    private sealed class Ring
    {
        private readonly LogEvent[] _arr;
        private int _idx;
        private int _count;
        public DateTimeOffset LastSeen { get; private set; } = DateTimeOffset.UtcNow;

        public Ring(int capacity) => _arr = new LogEvent[capacity];

        public void Add(LogEvent e)
        {
            _arr[_idx] = e;
            _idx = (_idx + 1) % _arr.Length;
            _count = Math.Min(_count + 1, _arr.Length);
            LastSeen = DateTimeOffset.UtcNow;
        }

        public IEnumerable<LogEvent> EventsInOrder()
        {
            for (int i = _count; i > 0; i--)
                yield return _arr[(_idx - i + _arr.Length) % _arr.Length];
        }
    }
}
