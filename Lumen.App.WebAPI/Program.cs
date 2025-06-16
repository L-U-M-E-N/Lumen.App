using Lumen.App.ModuleLoader;
using Lumen.App.WebAPI.HostedServices;
using Lumen.App.WebAPI.Middlewares;
using Lumen.Modules.Sdk;

using Microsoft.EntityFrameworkCore;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using Swashbuckle.AspNetCore.SwaggerUI;

namespace Lumen.App.WebAPI;

public class Program {
    protected Program() { }

    public static async Task Main(string[] args) {
        ModuleLoaderHelper.RunsOn = Lumen.Modules.Sdk.LumenModuleRunsOnFlag.API;

        var builder = WebApplication.CreateBuilder(args);

        // Config
        builder.Configuration
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddUserSecrets<Program>(true)
            .AddEnvironmentVariables()
            .AddJsonFile("appsettings.json", false)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true)
            .Build();

        var openTelemetryBuilder = builder.Services.AddOpenTelemetry()
        .WithTracing(otBuilder => {

            otBuilder.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation();
            if (builder.Environment.EnvironmentName == "Development") {
                otBuilder.AddConsoleExporter();
            }
        })
        .WithMetrics(otBuilder => {
            otBuilder.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation();
        })
        .WithLogging(otBuilder => {

        });

        if (builder.Environment.EnvironmentName != "Development") {
            openTelemetryBuilder.UseOtlpExporter();
        }

        // Add services to the container.
        var connectionString = GetConnectionString();
        var modulesAssemblies = builder.Services.LoadModules(GetConfigurationEntries(builder.Configuration), connectionString);

        var mvcBuilder = builder.Services.AddControllers();
        foreach (var module in modulesAssemblies) {
            // Web controllers
            mvcBuilder.AddApplicationPart(module);
        }
        mvcBuilder.AddControllersAsServices();

        builder.Services.AddOpenApi();

        builder.Services.AddHostedService<ModuleExecutionHostedService>();

        var app = builder.Build();

        using (var scope = app.Services.CreateScope()) {
            var modulesList = scope.ServiceProvider.GetServices<LumenModuleBase>();
            foreach (var module in modulesList) {
                var dbContext = scope.ServiceProvider.GetService(module.GetDatabaseContextType());
                if (dbContext is null) {
                    throw new NullReferenceException(nameof(dbContext));
                }
                var typedDbContext = (DbContext)dbContext;
                await typedDbContext.Database.MigrateAsync();
            }
        }

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment()) {
            app.MapOpenApi();
            app.UseSwaggerUI(options => {
                options.ConfigObject.Urls = [new UrlDescriptor {
                    Name = "LUMEN API",
                    Url = "/openapi/v1.json"
                }];
            });
        }

        app.UseMiddleware<ApiKeyMiddleware>();
        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }

    private static IEnumerable<ConfigEntry> GetConfigurationEntries(ConfigurationManager configuration) {
        var moduleSection = configuration.GetSection("Modules");
        var entries = new List<ConfigEntry>();
        foreach (var module in moduleSection.GetChildren()) {
            foreach (var configEntry in module.GetChildren()) {
                entries.Add(new ConfigEntry {
                    ConfigKey = configEntry.Key,
                    ConfigValue = configEntry.Value,
                    ModuleName = module.Key,
                });
            }
        }

        return entries;
    }

    private static string GetConnectionString() {
        var host = Environment.GetEnvironmentVariable("PG_HOST");
        var username = Environment.GetEnvironmentVariable("PG_USER");
        var password = Environment.GetEnvironmentVariable("PG_PASSWORD");
        var database = Environment.GetEnvironmentVariable("PG_DB");

        return $"Host={host};Database={database};Username={username};Password={password}";
    }
}
