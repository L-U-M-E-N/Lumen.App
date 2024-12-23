using Lumen.App.ModuleLoader;

using Swashbuckle.AspNetCore.SwaggerUI;

namespace Lumen.App.WebAPI;

public class Program {
    protected Program() { }

    public static void Main(string[] args) {
        ModuleLoaderHelper.RunsOn = Lumen.Modules.Sdk.LumenModuleRunsOnFlag.API;

        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        var modulesAssemblies = builder.Services.LoadModules();

        var mvcBuilder = builder.Services.AddControllers();
        foreach (var module in modulesAssemblies) {
            mvcBuilder.AddApplicationPart(module);
        }
        mvcBuilder.AddControllersAsServices();

        builder.Services.AddOpenApi();

        var app = builder.Build();

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
