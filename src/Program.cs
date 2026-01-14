namespace OpcPlc;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpcPlc.Configuration;
using OpcPlc.PluginNodes.Models;
using Scrutor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

public static class Program
{
    /// <summary>
    /// Synchronous main method of the app.
    /// </summary>
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure content root for snap environment
        ConfigureContentRoot(builder);

        // Configure options from appsettings.json
        builder.Services.Configure<OpcPlcConfiguration>(
            builder.Configuration.GetSection(OpcPlcConfiguration.SectionName));

        // Validate configuration
        var config = builder.Configuration.GetSection(OpcPlcConfiguration.SectionName).Get<OpcPlcConfiguration>();
        if (config == null)
        {
            Console.WriteLine("Error: Could not load configuration from appsettings.json");
            Environment.Exit(1);
            return;
        }

        // Show usage if requested
        if (config.ShowHelp)
        {
            Console.WriteLine("OPC PLC Server - Configuration is now managed via appsettings.json");
            Console.WriteLine("Please edit appsettings.json to configure the server");
            Environment.Exit(0);
            return;
        }

        // Configure web server URLs conditionally
        ConfigureWebServer(builder);

        // Add MVC services for controllers
        builder.Services.AddControllers();

        // Register dependencies
        builder.Services.AddSingleton(args);
        builder.Services.AddTransient<TimeService>();
        builder.Services.AddHostedService<OpcPlcServer>();
        builder.Services.AddHostedService<OpcTagWriterService>(); // Register the OPC Tag Writer Service
        builder.Services.AddSingleton<ILogger>(container => container.GetService<ILogger<object>>());
        builder.Services.AddSingleton<IpAddressProvider>();
        builder.Services.AddSingleton<PlcSimulation>();
        
        // Register a factory to get PlcServer from OpcPlcServer
        builder.Services.AddSingleton<Func<PlcServer>>(sp =>
        {
            return () =>
            {
                var opcPlcServer = sp.GetServices<IHostedService>()
                    .OfType<OpcPlcServer>()
                    .FirstOrDefault();
                return opcPlcServer?.PlcServer;
            };
        });

        // Register plugin nodes as services
        builder.Services.Scan(scan => scan
            .FromAssemblyOf<IPluginNodes>()
            .AddClasses(classes => classes.AssignableTo<IPluginNodes>())
            .AsImplementedInterfaces()
            .WithSingletonLifetime());

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // Use routing and map controllers
        app.UseRouting();
        app.MapControllers();

        app.Run();
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
        // Configure URLs after binding by using a lambda that reads from IOptions
        builder.WebHost.ConfigureKestrel((context, serverOptions) =>
        {
            var config = context.Configuration.Get<OpcPlcConfiguration>();          

            
            if (config.ShowPublisherConfigJsonIp || config.ShowPublisherConfigJsonPh)
            {
                serverOptions.ListenAnyIP((int)config.WebServerPort);
            }  
        });
    }

    
}

