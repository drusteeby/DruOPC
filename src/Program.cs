namespace OpcPlc;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

public static class Program
{
    public static OpcPlcServer OpcPlcServer { get; private set; }

    /// <summary>
    /// Synchronous main method of the app.
    /// </summary>
    public static void Main(string[] args)
    {
        OpcPlcServer = new OpcPlcServer();

        // Start the web app in a background task to serve pn.json without blocking the OPC server.
        Task.Run(() => RunWebApp(args));

        // Start OPC UA server.
        OpcPlcServer.StartAsync(args).Wait();
    }

    private static void RunWebApp(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        // Add MVC services for controllers
        builder.Services.AddControllers();
        
        // Register the configuration as a singleton for dependency injection
        builder.Services.AddSingleton(OpcPlcServer.Config);
        
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

