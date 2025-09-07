using PostTradeSystem.Core.Schemas;
using Microsoft.Extensions.DependencyInjection;

namespace PostTradeSystem.Core.Serialization;

public static class SerializationServiceConfiguration
{
    public static IServiceCollection AddEventSerialization(this IServiceCollection services)
    {
        services.AddSingleton<EventSerializationRegistry>();
        services.AddSingleton<JsonSchemaValidator>();
        services.AddSingleton<SchemaValidationService>();
        services.AddSingleton<EventVersioningStrategy>();
        
        services.AddSingleton(provider =>
        {
            var registry = provider.GetRequiredService<EventSerializationRegistry>();
            var strategy = provider.GetRequiredService<EventVersioningStrategy>();
            strategy.ConfigureEventVersioning();
            return registry;
        });

        return services;
    }

    public static void ConfigureEventSerialization(IServiceProvider serviceProvider)
    {
        var strategy = serviceProvider.GetRequiredService<EventVersioningStrategy>();
        strategy.ConfigureEventVersioning();
    }
}