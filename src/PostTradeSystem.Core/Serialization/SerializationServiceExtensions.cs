using Microsoft.Extensions.DependencyInjection;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Core.Services;
using PostTradeSystem.Core.Serialization.Contracts;

namespace PostTradeSystem.Core.Serialization;

public static class SerializationServiceExtensions
{
    public static IServiceCollection AddSerializationManagement(this IServiceCollection services)
    {
        // Core serialization services
        services.AddSingleton<ITradeRiskService, TradeRiskService>();
        services.AddSingleton<IEventVersionManager>(provider =>
        {
            var versionManager = new EventVersionManager();
            
            // Register converters
            versionManager.RegisterConverter(new TradeCreatedEventV1ToV2Converter());
            versionManager.RegisterConverter(new TradeCreatedEventV2ToV1Converter());
            versionManager.RegisterConverter(new TradeStatusChangedEventV1ToV2Converter());
            versionManager.RegisterConverter(new TradeStatusChangedEventV2ToV1Converter());
            
            return versionManager;
        });
        services.AddSingleton<IEventSerializer, EventSerializer>();
        services.AddSingleton<EventSerializationOrchestrator>();
        services.AddSingleton<EventSerializationRegistry>();
        services.AddSingleton<SerializationManagementService>(provider =>
        {
            var registry = provider.GetRequiredService<EventSerializationRegistry>();
            var schemaRegistry = provider.GetRequiredService<ISchemaRegistry>();
            var validator = provider.GetRequiredService<JsonSchemaValidator>();
            var tradeRiskService = provider.GetRequiredService<ITradeRiskService>();
            return new SerializationManagementService(registry, schemaRegistry, validator, tradeRiskService);
        });
        
        // Schema services - will be registered by infrastructure layer
        services.AddSingleton<JsonSchemaValidator>();
        
        // Event serializer - uses the management service internally for backward compatibility
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

    public Task<SerializedEvent> SerializeAsync<T>(T domainEvent, int? targetSchemaVersion = null) where T : IDomainEvent
    {
        return _managementService.SerializeAsync(domainEvent, targetSchemaVersion);
    }

    public Task<IDomainEvent> DeserializeAsync(SerializedEvent serializedEvent)
    {
        return Task.FromResult(_managementService.Deserialize(serializedEvent));
    }

}