using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nuts.Plugin.Handlers;
using Nuts.Plugin.Operations;
using Vonk.Core.Context;
using Vonk.Core.Pluggability;
using Task = Hl7.Fhir.Model.Task;

namespace Nuts.Plugin
{
    [VonkConfiguration(order: 3106)] // just before the first data processing plugin (FhirBatchConfiguration)
    public class NutsPluginConfiguration
    {
        public static IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.TryAddScoped<NotificationTaskHandler>();
            services.TryAddScoped<ReferOperation>();
            services.TryAddScoped<NutsClient>();
            services.Configure<NutsOptions>(configuration.GetSection(nameof(NutsOptions)));
            return services;
        }

        public static IApplicationBuilder Configure(IApplicationBuilder builder)
        {
            builder.OnInteraction(VonkInteraction.all_create).AndResourceTypes(nameof(Task))
                .PreHandleAsyncWith<NotificationTaskHandler>((svc, context) => svc.Handle(context));
            
            builder.OnCustomInteraction(VonkInteraction.type_custom, "refer").AndMethod("POST")
                .AndResourceTypes(nameof(Task)).HandleAsyncWith<ReferOperation>((svc, context) => svc.Execute(context));

            return builder;
        }
    }
}