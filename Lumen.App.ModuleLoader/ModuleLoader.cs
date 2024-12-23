using Lumen.Modules.Sdk;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System.Reflection;

namespace Lumen.App.ModuleLoader {
    public static class ModuleLoaderHelper {
        public static LumenModuleRunsOnFlag RunsOn { get; set; }

        public static IEnumerable<Assembly> LoadModules(this IServiceCollection services) {
            var DLLlist = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory)
                .Where(x => x.EndsWith(".dll"));
            var modulesAssemblies = new List<Assembly>();

            foreach (var file in DLLlist) {
                var currentAssembly = Assembly.LoadFrom(file);
                IEnumerable<Type> modules = currentAssembly.ExportedTypes.Where(x => x.IsSubclassOf(typeof(LumenModuleBase)));

                foreach (var module in modules) {
                    services.AddTransient<LumenModuleBase>(x => {
                        var instance = Activator.CreateInstance(
                            module,
                            [
                                RunsOn,
                                    x.GetRequiredService<ILogger<LumenModuleBase>>()
                            ]
                        );

                        return instance is null
                            ? throw new ArgumentNullException($"Cannot instanciate the current module: {module.Name}")
                            : (LumenModuleBase)instance;
                    });
                }

                if (modules.Any()) {
                    modulesAssemblies.Add(currentAssembly);
                }
            }

            return modulesAssemblies;
        }
    }
}
