using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stl.DependencyInjection;
using Stl.Extensibility;
using Stl.Serialization;

namespace TodoApp.Services
{
    public class Module : ModuleBase
    {
        public Module(IServiceCollection services) : base(services) { }

        public override void Use()
        {
            Services.TryAddSingleton(c => (Func<ISerializer<string>>) (
                () => new JsonNetSerializer(JsonNetSerializer.DefaultSettings)));
            Services.AttributeScanner().AddServicesFrom(GetType().Assembly);
        }
    }
}
