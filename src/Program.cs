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
using System.IO;

public static class Program
{
    /// <summary>
    /// Synchronous main method of the app.
    /// </summary>
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure options from appsettings.json and command line
        builder.Services.Configure<OpcPlcConfiguration>(
            builder.Configuration.GetSection(OpcPlcConfiguration.SectionName));

        // Add MVC services for controllers
        builder.Services.AddControllers();

        // Register dependencies
        builder.Services.AddSingleton(args);
        builder.Services.AddTransient<TimeService>();
        builder.Services.AddHostedService<OpcPlcServer>();
        builder.Services.AddHostedService<OpcPlcServer>();
        builder.Services.AddSingleton<ILogger>(container => container.GetService<ILogger<object>>());

        // Register plugin nodes as services
        builder.Services.Scan(scan => scan
            .FromAssemblyOf<IPluginNodes>()
            .AddClasses(classes => classes.AssignableTo<IPluginNodes>())
            .AsImplementedInterfaces()
            .WithSingletonLifetime());

        // Register PlcSimulation as a service
        builder.Services.AddSingleton<PlcSimulation>();
        builder.Services.AddSingleton<PlcServer>();

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
}

