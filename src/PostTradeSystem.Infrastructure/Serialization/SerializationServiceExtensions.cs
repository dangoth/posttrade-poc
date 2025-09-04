using Microsoft.Extensions.DependencyInjection;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Infrastructure.Schemas;

namespace PostTradeSystem.Infrastructure.Serialization;

public static class SerializationServiceExtensions
{
    public static IServiceCollection AddEventSerialization(this IServiceCollection services)
    {
        services.AddSingleton<EventSerializationRegistry>();
        services.AddSingleton<ISchemaRegistry, InMemorySchemaRegistry>();
        services.AddSingleton<JsonSchemaValidator>();
        services.AddSingleton<SchemaRegistrationService>();
        services.AddSingleton<IEventSerializer, JsonEventSerializer>();
        services.AddSingleton<KafkaEventSerializer>();
        services.AddSingleton<ISerializationConfigurator, SerializationConfigurator>();

        return services;
    }
}

public interface ISerializationConfigurator
{
    Task ConfigureAsync();
}

public class SerializationConfigurator : ISerializationConfigurator
{
    private readonly EventSerializationRegistry _registry;
    private readonly SchemaRegistrationService _schemaRegistration;

    public SerializationConfigurator(EventSerializationRegistry registry, SchemaRegistrationService schemaRegistration)
    {
        _registry = registry;
        _schemaRegistration = schemaRegistration;
    }

    public async Task ConfigureAsync()
    {
        EventSerializationConfiguration.Configure(_registry);
        await _schemaRegistration.RegisterEventContractSchemasAsync();
    }
}