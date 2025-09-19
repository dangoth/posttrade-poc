using Microsoft.Extensions.DependencyInjection;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Schemas;

namespace PostTradeSystem.Core.Serialization;

public static class SerializationServiceExtensions
{
    public static IServiceCollection AddSerializationManagement(this IServiceCollection services)
    {
        // Core serialization services
        services.AddSingleton<EventSerializationRegistry>();
        services.AddSingleton<SerializationManagementService>();
        
        // Schema services - will be registered by infrastructure layer
        services.AddSingleton<JsonSchemaValidator>();
        
        // Event serializer - uses the management service internally
        services.AddSingleton<IEventSerializer>(provider =>
        {
            var managementService = provider.GetRequiredService<SerializationManagementService>();
            return new ManagedEventSerializer(managementService);
        });

        return services;
    }

    public static async Task InitializeSerializationAsync(this IServiceProvider serviceProvider)
    {
        var serializationService = serviceProvider.GetRequiredService<SerializationManagementService>();
        await serializationService.InitializeAsync();
    }
}

// Adapter that implements IEventSerializer using SerializationManagementService
internal class ManagedEventSerializer : IEventSerializer
{
    private readonly SerializationManagementService _managementService;

    public ManagedEventSerializer(SerializationManagementService managementService)
    {
        _managementService = managementService;
    }

    public Task<SerializedEvent> Serialize(IDomainEvent domainEvent)
    {
        return _managementService.SerializeAsync(domainEvent);
    }

    public IDomainEvent Deserialize(SerializedEvent serializedEvent)
    {
        return _managementService.Deserialize(serializedEvent);
    }

    public bool CanHandle(string eventType, int schemaVersion)
    {
        return _managementService.CanHandle(eventType, schemaVersion);
    }

    public IEnumerable<int> GetSupportedSchemaVersions(string eventType)
    {
        return _managementService.GetSupportedSchemaVersions(eventType);
    }
}