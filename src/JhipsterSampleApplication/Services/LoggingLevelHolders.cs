using Serilog.Core;

namespace JhipsterSampleApplication.Services;

public sealed class GlobalLogLevel
{
    public LoggingLevelSwitch Switch { get; }
    public GlobalLogLevel(LoggingLevelSwitch @switch) => Switch = @switch;
}

public sealed class FileLogLevel
{
    public LoggingLevelSwitch Switch { get; }
    public FileLogLevel(LoggingLevelSwitch @switch) => Switch = @switch;
}

