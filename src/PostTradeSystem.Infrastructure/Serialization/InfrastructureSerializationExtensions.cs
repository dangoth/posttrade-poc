using Microsoft.Extensions.DependencyInjection;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.Services;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Infrastructure.Schemas;

namespace PostTradeSystem.Infrastructure.Serialization;

public static class InfrastructureSerializationExtensions
{
    public static IServiceCollection AddInfrastructureSerialization(this IServiceCollection services)
    {
        // Add infrastructure-specific schema registry
        services.AddSingleton<ISchemaRegistry, InMemorySchemaRegistry>();
        
        // Add the core serialization management
        services.AddSerializationManagement();
        
        // Add Kafka-specific serializer
        services.AddSingleton<KafkaEventSerializer>();

        return services;
    }
}