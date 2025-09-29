using Serilog.Core;
using Serilog.Events;

namespace JhipsterSampleApplication.Logging;

public sealed class LevelStampingEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (logEvent == null) return;
        var lvl = propertyFactory.CreateProperty("level_text", logEvent.Level.ToString());
        logEvent.AddOrUpdateProperty(lvl);

        if (!logEvent.Properties.ContainsKey("SourceContext") && logEvent.Properties.TryGetValue("log.logger", out var ll))
        {
            // Mirror log.logger to SourceContext if missing, helps tailers
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("SourceContext", ll));
        }
    }
}

