// Enable nullable reference types for this file
#nullable enable

using System;
using System.Threading;
using Serilog.Core;
using Serilog.Events;

namespace JhipsterSampleApplication.Services;

public class LoggingControlService
{
    private readonly LoggingLevelSwitch _globalSwitch;
    private readonly LoggingLevelSwitch _fileSwitch;
    private readonly LoggingLevelSwitch _rootSwitch;
    private Timer? _revertTimer;
    private LogEventLevel _previousLevel;
    private Timer? _fileTimer;
    private LogEventLevel _prevFileLevel;

    public LoggingControlService(GlobalLogLevel global, FileLogLevel file, RootLogLevel root)
    {
        _globalSwitch = global.Switch;
        _fileSwitch = file.Switch;
        _rootSwitch = root.Switch;
        SyncRoot();
    }

    public LogEventLevel CurrentGlobalLevel => _globalSwitch.MinimumLevel;
    public LogEventLevel CurrentFileLevel => _fileSwitch.MinimumLevel;

    public void SetGlobalLevel(LogEventLevel level, TimeSpan? duration = null)
    {
        CancelGlobalRevert();
        _previousLevel = _globalSwitch.MinimumLevel;
        _globalSwitch.MinimumLevel = level;
        SyncRoot();
        if (duration.HasValue && duration.Value > TimeSpan.Zero)
        {
            _revertTimer = new Timer(_ =>
            {
                _globalSwitch.MinimumLevel = _previousLevel;
                SyncRoot();
                CancelGlobalRevert();
            }, null, duration.Value, Timeout.InfiniteTimeSpan);
        }
    }

    public void SetFileLevel(LogEventLevel level, TimeSpan? duration = null)
    {
        CancelFileRevert();
        _prevFileLevel = _fileSwitch.MinimumLevel;
        _fileSwitch.MinimumLevel = level;
        SyncRoot();
        if (duration.HasValue && duration.Value > TimeSpan.Zero)
        {
            _fileTimer = new Timer(_ =>
            {
                _fileSwitch.MinimumLevel = _prevFileLevel;
                SyncRoot();
                CancelFileRevert();
            }, null, duration.Value, Timeout.InfiniteTimeSpan);
        }
    }

    private void CancelGlobalRevert()
    {
        _revertTimer?.Dispose();
        _revertTimer = null;
    }

    private void CancelFileRevert()
    {
        _fileTimer?.Dispose();
        _fileTimer = null;
    }

    private void SyncRoot()
    {
        var min = _globalSwitch.MinimumLevel <= _fileSwitch.MinimumLevel ? _globalSwitch.MinimumLevel : _fileSwitch.MinimumLevel;
        _rootSwitch.MinimumLevel = min;
    }
}
