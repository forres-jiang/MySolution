using My.XXX.APIs.Common.Middleware;
using My.XXX.APIs.DependencyInjection;
using My.XXX.Application;
using My.XXX.Application.Authorization.Ports;
using My.XXX.Infrastructure;
using My.XXX.Infrastructure.Caching;
using My.XXX.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Linq;

namespace My.XXX.APIs;

public class Program
{
    public static async System.Threading.Tasks.Task Main(string[] args)
    {
        try
        {
            if (args.Contains("--migrate", StringComparer.Ordinal))
            {
                await RunMigrations(args);
                return;
            }
            var app = CreateApplication(args);
            Log.Information("Application Starting.");
            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "The Application failed to start.");
            throw;
        }
        finally { Log.CloseAndFlush(); }
    }

    private static async System.Threading.Tasks.Task RunMigrations(string[] args)
    {
        var migrationArgs = args.Where(a => a != "--migrate").ToArray();
        var builder = WebApplication.CreateBuilder(migrationArgs);
        var connection = StorageRegistration.ResolveConnectionString(
            builder.Configuration.GetConnectionString("Default"), "Default", builder.Configuration["APP_ENCRYPTION_KEY"]);
        await My.XXX.Persistence.Migrations.MigrationRunner.ApplyAsync(connection,
            My.XXX.Persistence.DatabaseConfiguration.ParseProvider(builder.Configuration["DatabaseProviders:Default"]));
        Console.WriteLine("Database migrations completed.");
    }

    public static WebApplication CreateApplication(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(Program).Assembly.FullName
        });
        configure?.Invoke(builder);

        var telemetryEnabled = builder.AddProjectTelemetry();

        //Serilog
        builder.Host.UseSerilog((context, logger) =>
        {
            logger.ReadFrom.Configuration(context.Configuration);
            logger.Enrich.FromLogContext();
        }, writeToProviders: telemetryEnabled);

        builder.Services.AddConfiguration(builder.Configuration);
        builder.Services.AddDefaultService(builder.Configuration);
        var usePermissionCache = builder.Services.AddDBs(builder.Configuration);
        builder.Services.AddApplicationServices();
        builder.Services.AddRepositories();
        builder.Services.AddExternalAdapters();
        builder.Services.AddWebAdapters();
        builder.Services.Decorate<My.XXX.Application.Authentication.Ports.ISessionService, My.XXX.Infrastructure.Authentication.MeasuredSessionService>();
        if (usePermissionCache)
        {
            builder.Services.Decorate<IPermissionStore, CachedPermissionStore>();
        }

        var app = builder.Build();
        app.UseForwardedHeaders();
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            // TLS 终止在 ingress/代理层；此处仅下发 HSTS，不做应用层重定向以免与代理冲突。
            app.UseHsts();
        }

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("./v1/swagger.json", "XXX API v1"));
        }

        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseRateLimiter();

        //跨域
        app.UseCors("AppPolicy");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHealthChecks("/healthy", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = _ => false
        }).AllowAnonymous();
        app.MapHealthChecks("/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        }).AllowAnonymous();
        return app;
    }

}
