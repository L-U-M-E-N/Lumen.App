﻿
using Lumen.Modules.Sdk;

using System.Timers;

namespace Lumen.App.WebAPI.HostedServices {
    public class ModuleExecutionHostedService(IServiceProvider serviceProvider, ILogger<ModuleExecutionHostedService> logger) : IHostedService {
        public const LumenModuleRunsOnFlag RunsOn = LumenModuleRunsOnFlag.API;
        private System.Timers.Timer modulesRunTimer;

        private async Task RunModulesTasks() {
            using var scope = serviceProvider.CreateScope();
            var now = DateTime.UtcNow;

            var modules = scope.ServiceProvider.GetServices<LumenModuleBase>();
            foreach (var module in modules) {
                if (module.ShouldRunNow(RunsOn, now)) {
                    await module.RunAsync(RunsOn, now);
                }
            }
        }

        private void RunModulesTasksFromTimer(object state, ElapsedEventArgs eventData) {
            RunModulesTasks().Wait();
        }

        public async Task StartAsync(CancellationToken cancellationToken) {
            using (var scope = serviceProvider.CreateScope()) {
                var modules = scope.ServiceProvider.GetServices<LumenModuleBase>();
                foreach (var module in modules) {
                    await module.InitAsync(RunsOn);
                }
            }

            await Task.Run(() => {
                modulesRunTimer = new System.Timers.Timer(1_000);
                modulesRunTimer.Elapsed += RunModulesTasksFromTimer;
                modulesRunTimer.AutoReset = true;
                modulesRunTimer.Enabled = true;
            }, cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) {
            modulesRunTimer.Stop();

            return Task.CompletedTask;
        }
    }
}
