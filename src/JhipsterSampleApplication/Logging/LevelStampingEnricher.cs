using Serilog.Core;
using Serilog.Events;
using System.IO;

namespace JhipsterSampleApplication.Logging;

public sealed class LevelStampingEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (logEvent == null) return;
        var lvl = propertyFactory.CreateProperty("level_text", logEvent.Level.ToString());
        logEvent.AddOrUpdateProperty(lvl);

        // Ensure SourceContext is populated for attribution (module)
        if (!logEvent.Properties.ContainsKey("SourceContext") && logEvent.Properties.TryGetValue("log.logger", out var ll))
        {
            // Mirror log.logger to SourceContext if missing, helps tailers
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("SourceContext", ll));
        }

        if (!logEvent.Properties.ContainsKey("SourceContext") && logEvent.Properties.TryGetValue("CallerFilePath", out var fp))
        {
            var s = fp.ToString().Trim('"');
            try
            {
                var cls = Path.GetFileNameWithoutExtension(s);
                if (!string.IsNullOrWhiteSpace(cls))
                    logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("SourceContext", cls));
            }
            catch { /* ignore */ }
        }

        // Also surface class/method under labels for ES mapping/tailer
        if (logEvent.Properties.TryGetValue("CallerMemberName", out var cm))
        {
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("labels.CallerMemberName", cm));
        }
        if (logEvent.Properties.TryGetValue("SourceContext", out var sc2))
        {
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("labels.SourceContext", sc2));
        }
    }
}
