using Lumen.App.ModuleLoader;
using Lumen.Modules.Sdk;

using Microsoft.EntityFrameworkCore;

using Swashbuckle.AspNetCore.SwaggerUI;

namespace Lumen.App.WebAPI;

public class Program {
    protected Program() { }

    public static async Task Main(string[] args) {
        ModuleLoaderHelper.RunsOn = Lumen.Modules.Sdk.LumenModuleRunsOnFlag.API;

        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        var modulesAssemblies = builder.Services.LoadModules("CONNECTIONSTRING"); // TODO: Config

        var mvcBuilder = builder.Services.AddControllers();
        foreach (var module in modulesAssemblies) {
            // Web controllers
            mvcBuilder.AddApplicationPart(module);
        }
        mvcBuilder.AddControllersAsServices();

        builder.Services.AddOpenApi();

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
