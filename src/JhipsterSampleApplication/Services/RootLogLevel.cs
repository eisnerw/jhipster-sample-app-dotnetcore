using Serilog.Core;

namespace JhipsterSampleApplication.Services;

public sealed class RootLogLevel
{
    public LoggingLevelSwitch Switch { get; }
    public RootLogLevel(LoggingLevelSwitch @switch) => Switch = @switch;
}

