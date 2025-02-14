using Lumen.App.ModuleLoader;
using Lumen.App.WebAPI.HostedServices;
using Lumen.Modules.Sdk;

using Microsoft.EntityFrameworkCore;

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

        // Add services to the container.
        var connectionString = builder.Configuration.GetConnectionString("Lumen") ?? throw new NullReferenceException("The connection string is not defined !");
        var modulesAssemblies = builder.Services.LoadModules([], connectionString); // TODO: Config

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

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}
