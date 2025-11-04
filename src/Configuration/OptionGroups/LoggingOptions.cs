namespace OpcPlc.Configuration.OptionGroups;

using Mono.Options;
using OpcPlc.Configuration.Validators;
using System;
using System.Collections.Generic;

/// <summary>
/// Options for logging configuration.
/// </summary>
public class LoggingOptions : IOptionGroup
{
    private readonly OpcPlcConfiguration _config;

    public LoggingOptions(OpcPlcConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public void RegisterOptions(OptionSet options)
    {
        var logLevelValidator = new EnumValidator(
            new[] { "critical", "error", "warn", "info", "debug", "trace" },
            caseInsensitive: true);

        var positiveIntValidator = new PositiveNumberValidator<int>(0);

        options.Add(
            "lf|logfile=",
            $"the filename of the logfile to use.\nDefault: './{_config.LogFileName}'",
            (string s) => _config.LogFileName = s);

        options.Add(
            "lt|logflushtimespan=",
            $"the timespan in seconds when the logfile should be flushed.\nDefault: {_config.LogFileFlushTimeSpanSec.TotalSeconds} sec",
            (int i) =>
            {
                positiveIntValidator.Validate(i, "logflushtimespan");
                _config.LogFileFlushTimeSpanSec = TimeSpan.FromSeconds(i);
            });

        options.Add(
            "ll|loglevel=",
            "the loglevel to use (allowed: critical, error, warn, info, debug, trace).\nDefault: info",
            (string s) =>
            {
                var lowerValue = s.ToLowerInvariant();
                logLevelValidator.Validate(lowerValue, "loglevel");
                _config.LogLevelCli = lowerValue;
            });
    }
}
