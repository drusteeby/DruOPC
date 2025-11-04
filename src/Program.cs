namespace OpcPlc;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpcPlc.Configuration;
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
        builder.Services.AddHostedService<OpcPlcServer>();

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

