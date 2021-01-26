using System;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor;
using MudBlazor.Services;
using Pluralize.NET;
using Stl.DependencyInjection;
using Stl.Extensibility;
using Stl.Fusion;
using Stl.Fusion.Blazor;
using Stl.Fusion.Client;

namespace TodoApp.UI
{
    [Module]
    public class Module : ModuleBase
    {
        public const string ClientSideScope = nameof(ClientSideScope);
        public WebAssemblyHostBuilder? HostBuilder { get; }

        public Module(IServiceCollection services, WebAssemblyHostBuilder? hostBuilder = null) : base(services)
            => HostBuilder = hostBuilder;

        public override void Use()
        {
            if (HostBuilder == null) {
                ConfigureSharedServices();
                return;
            }

            HostBuilder.Logging.SetMinimumLevel(LogLevel.Warning);

            var baseUri = new Uri(HostBuilder.HostEnvironment.BaseAddress);
            var apiBaseUri = new Uri($"{baseUri}api/");

            var fusion = Services.AddFusion();
            var fusionClient = fusion.AddRestEaseClient(
                (c, o) => {
                    o.BaseUri = baseUri;
                    o.MessageLogLevel = LogLevel.Information;
                }).ConfigureHttpClientFactory(
                (c, name, o) => {
                    var isFusionClient = (name ?? "").StartsWith("Stl.Fusion");
                    var clientBaseUri = isFusionClient ? baseUri : apiBaseUri;
                    o.HttpClientActions.Add(client => client.BaseAddress = clientBaseUri);
                });
            var fusionAuth = fusion.AddAuthentication().AddRestEaseClient().AddBlazor();

            // This method registers services marked with any of ServiceAttributeBase descendants, including:
            // [Service], [ComputeService], [RestEaseReplicaService], [LiveStateUpdater]
            Services.AttributeScanner(ClientSideScope).AddServicesFrom(GetType().Assembly);
            ConfigureSharedServices();
        }

        public virtual void ConfigureSharedServices()
        {
            Services.AddMudBlazorDialog();
            Services.AddMudBlazorSnackbar();
            Services.AddMudBlazorResizeListener();

            // Default delay for update delayers
            Services.AddSingleton(c => new UpdateDelayer.Options() {
                Delay = TimeSpan.FromSeconds(0.1),
            });

            Services.AddSingleton<IPluralize, Pluralizer>();

            // This method registers services marked with any of ServiceAttributeBase descendants, including:
            // [Service], [ComputeService], [RestEaseReplicaService], [LiveStateUpdater]
            Services.AttributeScanner().AddServicesFrom(GetType().Assembly);
        }
    }
}
