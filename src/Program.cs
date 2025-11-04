namespace OpcPlc;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

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
        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // Middleware to serve pn.json (moved from Startup.cs Configure method).
        app.Run(async context =>
        {
            if (context.Request.Method == "GET" &&
                context.Request.Path == (Program.OpcPlcServer.Config.PnJson[0] != '/' ? "/" : string.Empty) + Program.OpcPlcServer.Config.PnJson &&
                File.Exists(Program.OpcPlcServer.Config.PnJson))
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(await File.ReadAllTextAsync(Program.OpcPlcServer.Config.PnJson).ConfigureAwait(false)).ConfigureAwait(false);
            }
            else
            {
                context.Response.StatusCode = 404;
            }
           });
   
           app.Run();
       }
   }

