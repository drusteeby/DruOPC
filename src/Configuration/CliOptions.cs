namespace OpcPlc.Configuration;

using Mono.Options;
using OpcPlc.Configuration.OptionGroups;
using OpcPlc.PluginNodes.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

/// <summary>
/// Orchestrates command-line option parsing using modular option groups.
/// </summary>
public static class CliOptions
{
    private static Mono.Options.OptionSet _options;

    /// <summary>
    /// Initializes configuration from command-line arguments.
    /// </summary>
    public static (PlcSimulation PlcSimulationInstance, List<string> ExtraArgs) InitConfiguration(
        string[] args,
        OpcPlcConfiguration config,
        ImmutableList<IPluginNodes> pluginNodes)
    {
        var plcSimulation = new PlcSimulation(pluginNodes);

        _options = BuildOptionSet(config, plcSimulation, pluginNodes);

        // Parse the command line.
        List<string> extraArgs = _options.Parse(args);

        return (plcSimulation, extraArgs);
    }

    /// <summary>
    /// Builds the option set with all registered option groups.
    /// </summary>
    private static OptionSet BuildOptionSet(
        OpcPlcConfiguration config,
        PlcSimulation plcSimulation,
        ImmutableList<IPluginNodes> pluginNodes)
    {
        var options = new OptionSet();

        // Create and register all option groups
        var optionGroups = new List<IOptionGroup>
        {
            new LoggingOptions(config),
            new SimulationOptions(plcSimulation),
            new OpcUaServerOptions(config),
            new CertificateStoreOptions(config),
            new AuthenticationOptions(config),
            new OtlpOptions(config),
            new MiscellaneousOptions(config)
        };

        // Register options from each group
        foreach (var group in optionGroups)
        {
            group.RegisterOptions(options);
        }

        // Add options from plugin nodes
        foreach (var plugin in pluginNodes)
        {
            plugin.AddOptions(options);
        }

        return options;
    }

    /// <summary>
    /// Get usage help message.
    /// </summary>
    public static string GetUsageHelp(string programName)
    {
        var sb = new StringBuilder();

        sb.AppendLine();
        sb.AppendLine($"{programName} v{FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion}");
        sb.AppendLine($"Informational version: v{(Attribute.GetCustomAttribute(Assembly.GetEntryAssembly(), typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute)?.InformationalVersion}");
        sb.AppendLine();
        sb.AppendLine($"Usage: dotnet {Assembly.GetEntryAssembly().GetName().Name}.dll [<options>]");
        sb.AppendLine();
        sb.AppendLine("OPC UA PLC for different data simulation scenarios.");
        sb.AppendLine("To exit the application, press CTRL-C while it's running.");
        sb.AppendLine();
        sb.AppendLine("Use the following format to specify a list of strings:");
        sb.AppendLine("\"<string 1>,<string 2>,...,<string n>\"");
        sb.AppendLine("or if one string contains commas:");
        sb.AppendLine("\"\"<string 1>\",\"<string 2>\",...,\"<string n>\"\"");
        sb.AppendLine();

        // Append the options.
        sb.AppendLine("Options:");
        using var stringWriter = new StringWriter(sb);
        _options.WriteOptionDescriptions(stringWriter);

        return sb.ToString();
    }
}

