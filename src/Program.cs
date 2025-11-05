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

        // Configure options from appsettings.json and command line
        builder.Services.Configure<OpcPlcConfiguration>(
            builder.Configuration.GetSection(OpcPlcConfiguration.SectionName));

        // Configure web server URLs conditionally
        ConfigureWebServer(builder);

        // Add MVC services for controllers
        builder.Services.AddControllers();

        // Register dependencies
        builder.Services.AddSingleton(args);
        builder.Services.AddTransient<TimeService>();
        builder.Services.AddHostedService<OpcPlcServer>();
        builder.Services.AddSingleton<ILogger>(container => container.GetService<ILogger<object>>());
        builder.Services.AddSingleton<IpAddressProvider>();
        builder.Services.AddSingleton<PlcSimulation>();
        builder.Services.AddSingleton<PlcServer>();

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
            var config = context.Configuration.GetSection(OpcPlcConfiguration.SectionName);
            
            var showPublisherConfigJsonIp = config.GetValue<bool>("ShowPublisherConfigJsonIp");
            var showPublisherConfigJsonPh = config.GetValue<bool>("ShowPublisherConfigJsonPh");
            var webServerPort = config.GetValue<uint?>("WebServerPort") ?? 8080;
            
            if (showPublisherConfigJsonIp || showPublisherConfigJsonPh)
            {
                serverOptions.ListenAnyIP((int)webServerPort);
            }
        });
    } 
}

