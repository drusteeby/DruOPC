namespace OpcPlc;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpcPlc.Configuration;
using OpcPlc.PluginNodes.Models;
using System;

public static class Program
{
    /// <summary>
    /// Synchronous main method of the app.
    /// </summary>
    public static void Main(string[] args)
    {
        var app = CreateApplication(args);

        var config = app.Services.GetRequiredService<IOptions<OpcPlcConfiguration>>().Value;
        if (config.ShowHelp)
        {
            Console.WriteLine("OPC PLC Server - Configuration is managed via appsettings.json");
            Console.WriteLine("Please edit appsettings.json to configure the server");
            return;
        }

        app.Run();
    }

    /// <summary>
    /// Create the fully configured web application hosting the OPC UA server.
    /// Used by <see cref="Main"/> and by integration tests, which pass
    /// configuration overrides and service replacements via <paramref name="configureBuilder"/>.
    /// </summary>
    public static WebApplication CreateApplication(string[] args, Action<WebApplicationBuilder> configureBuilder = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        ConfigureContentRoot(builder);
        ConfigureWebServer(builder);

        builder.Services.AddOpcPlcServices(builder.Configuration);

        // Allow callers (e.g. tests) to override configuration and services.
        configureBuilder?.Invoke(builder);

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();
        app.MapControllers();

        return app;
    }

    /// <summary>
    /// Register all OPC PLC services on the given service collection.
    /// </summary>
    public static IServiceCollection AddOpcPlcServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpcPlcConfiguration>()
            .Bind(configuration.GetSection(OpcPlcConfiguration.SectionName))
            .Validate(c => c.FastNodes.NodeRate > 0, "FastNodes:NodeRate must be a positive number of seconds")
            .Validate(c => c.SlowNodes.NodeRate > 0, "SlowNodes:NodeRate must be a positive number of seconds")
            .Validate(c => c.VeryFastByteStringNodes.NodeRate > 0, "VeryFastByteStringNodes:NodeRate must be a positive number of ms")
            .Validate(c => c.Simulation.SimulationCycleLength > 0, "Simulation:SimulationCycleLength must be a positive number of ms")
            .Validate(c => c.Simulation.EventInstanceRate > 0, "Simulation:EventInstanceRate must be a positive number of ms")
            .Validate(c => c.TagWriter.StepDelayMs > 0 && c.TagWriter.WriteIntervalMs > 0, "TagWriter delays must be positive")
            .ValidateOnStart();

        services.AddControllers();

        services.AddTransient<TimeService>();
        services.AddSingleton<IpAddressProvider>();
        services.AddSingleton<PlcSimulation>();

        // Non-generic ILogger for components that log under a shared category.
        services.AddSingleton<ILogger>(sp => sp.GetRequiredService<ILoggerFactory>().CreateLogger("OpcPlc"));

        // The OPC UA server host is exposed both as a singleton (so other
        // components can await readiness and reach the running PlcServer)
        // and as the hosted service that runs it.
        services.AddSingleton<OpcPlcServer>();
        services.AddHostedService(sp => sp.GetRequiredService<OpcPlcServer>());
        services.AddHostedService<OpcTagWriterService>();

        // Register plugin nodes.
        services.Scan(scan => scan
            .FromAssemblyOf<IPluginNodes>()
            .AddClasses(classes => classes.AssignableTo<IPluginNodes>())
            .AsImplementedInterfaces()
            .WithSingletonLifetime());

        return services;
    }

    private static void ConfigureContentRoot(WebApplicationBuilder builder)
    {
        var snapLocation = Environment.GetEnvironmentVariable("SNAP");
        if (!string.IsNullOrWhiteSpace(snapLocation))
        {
            // The application is running as a snap
            builder.Environment.ContentRootPath = snapLocation;
        }
    }

    private static void ConfigureWebServer(WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel((context, serverOptions) =>
        {
            var config = context.Configuration
                .GetSection(OpcPlcConfiguration.SectionName)
                .Get<OpcPlcConfiguration>() ?? new OpcPlcConfiguration();

            // Always bind the configured web server port so the pn.json
            // endpoint is served where the logs say it is.
            serverOptions.ListenAnyIP((int)config.WebServerPort);
        });
    }
}
