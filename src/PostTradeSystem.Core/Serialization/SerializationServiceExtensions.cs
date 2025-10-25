using Microsoft.Extensions.DependencyInjection;
using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Core.Services;
using PostTradeSystem.Core.Serialization.Contracts;
using PostTradeSystem.Core.Common;

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
            var externalDataService = provider.GetRequiredService<IExternalDataService>();
            
            var deterministicMock = new DeterministicMockExternalDataService();
            versionManager.RegisterConverter(new TradeCreatedEventV1ToV2Converter(deterministicMock));
            versionManager.RegisterConverter(new TradeCreatedEventV2ToV1Converter());
            versionManager.RegisterConverter(new TradeStatusChangedEventV1ToV2Converter(deterministicMock));
            versionManager.RegisterConverter(new TradeStatusChangedEventV2ToV1Converter());
            
            return versionManager;
        });
        services.AddSingleton<IEventSerializer, EventSerializer>();
        services.AddSingleton<EventSerializationOrchestrator>();
        services.AddSingleton<EventSerializationRegistry>();
        services.AddSingleton<ISerializationManagementService, SerializationManagementService>();
        
        // Schema services - registered by infrastructure
        services.AddSingleton<IJsonSchemaValidator, JsonSchemaValidator>();
        
        // Event serializer - uses the management service internally for backward compatibility
        services.AddSingleton<IEventSerializer, ManagedEventSerializer>();

        return services;
    }

}

internal class ManagedEventSerializer : IEventSerializer
{
    private readonly ISerializationManagementService _managementService;

    public ManagedEventSerializer(ISerializationManagementService managementService)
    {
        _managementService = managementService;
    }

    public Task<Result<SerializedEvent>> SerializeAsync<T>(T domainEvent, int? targetSchemaVersion = null) where T : IDomainEvent
    {
        return _managementService.SerializeAsync(domainEvent, targetSchemaVersion);
    }

    public Task<Result<IDomainEvent>> DeserializeAsync(SerializedEvent serializedEvent)
    {
        var result = _managementService.Deserialize(serializedEvent);
        return Task.FromResult(result);
    }

}