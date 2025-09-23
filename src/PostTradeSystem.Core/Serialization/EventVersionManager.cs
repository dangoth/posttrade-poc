using PostTradeSystem.Core.Events;
using PostTradeSystem.Core.Serialization.Contracts;
using System.Collections.Concurrent;

namespace PostTradeSystem.Core.Serialization;

public interface IEventVersionManager
{
    bool CanHandle(string eventType, int version);
    IEnumerable<int> GetSupportedVersions(string eventType);
    T ConvertToVersion<T>(IVersionedEventContract source, int targetVersion) where T : IVersionedEventContract;
    IVersionedEventContract ConvertFromDomainEvent(IDomainEvent domainEvent, int targetVersion);
    IDomainEvent ConvertToDomainEvent(IVersionedEventContract contract);
    void Register<T>(string eventType, Func<IDomainEvent, T> fromDomainEvent, Func<T, IDomainEvent> toDomainEvent) where T : IVersionedEventContract;
    Type? GetContractType(string eventType, int version);
    int GetLatestVersion(string eventType);
}

public class EventVersionManager : IEventVersionManager
{
    private readonly ConcurrentDictionary<string, EventTypeInfo> _eventTypes = new();
    private readonly ConverterRegistry _converterRegistry = new();

    public void Register<T>(string eventType, Func<IDomainEvent, T> fromDomainEvent, Func<T, IDomainEvent> toDomainEvent) where T : IVersionedEventContract
    {
        var contract = Activator.CreateInstance<T>();
        var schemaVersion = contract.SchemaVersion;

        var eventTypeInfo = _eventTypes.GetOrAdd(eventType, _ => new EventTypeInfo());
        eventTypeInfo.RegisterVersion(schemaVersion, typeof(T), fromDomainEvent, toDomainEvent);
    }

    public void RegisterConverter<TFrom, TTo>(IEventConverter<TFrom, TTo> converter)
        where TFrom : IVersionedEventContract
        where TTo : IVersionedEventContract
    {
        _converterRegistry.RegisterConverter(converter);
    }

    public void RegisterConverter(object converter)
    {
        _converterRegistry.RegisterConverter(converter);
    }

    public bool CanHandle(string eventType, int version)
    {
        return GetContractType(eventType, version) != null;
    }

    public IEnumerable<int> GetSupportedVersions(string eventType)
    {
        if (_eventTypes.TryGetValue(eventType, out var eventTypeInfo))
        {
            return eventTypeInfo.GetSupportedVersions();
        }
        return Enumerable.Empty<int>();
    }

    public T ConvertToVersion<T>(IVersionedEventContract source, int targetVersion) where T : IVersionedEventContract
    {
        if (source.SchemaVersion == targetVersion)
        {
            return (T)source;
        }

        // Handle specific known conversions
        if (source is TradeCreatedEventV1 v1 && typeof(T) == typeof(TradeCreatedEventV2))
        {
            return (T)(object)_converterRegistry.Convert<TradeCreatedEventV1, TradeCreatedEventV2>(v1);
        }
        
        if (source is TradeCreatedEventV2 v2 && typeof(T) == typeof(TradeCreatedEventV1))
        {
            return (T)(object)_converterRegistry.Convert<TradeCreatedEventV2, TradeCreatedEventV1>(v2);
        }
        
        if (source is TradeStatusChangedEventV1 sv1 && typeof(T) == typeof(TradeStatusChangedEventV2))
        {
            return (T)(object)_converterRegistry.Convert<TradeStatusChangedEventV1, TradeStatusChangedEventV2>(sv1);
        }
        
        if (source is TradeStatusChangedEventV2 sv2 && typeof(T) == typeof(TradeStatusChangedEventV1))
        {
            return (T)(object)_converterRegistry.Convert<TradeStatusChangedEventV2, TradeStatusChangedEventV1>(sv2);
        }

        throw new InvalidOperationException($"No converter registered for {source.GetType().Name} -> {typeof(T).Name}");
    }

    public IVersionedEventContract ConvertFromDomainEvent(IDomainEvent domainEvent, int targetVersion)
    {
        var typeName = domainEvent.GetType().Name;
        var eventType = typeName.EndsWith("Event") ? typeName[..^5] : typeName;
        
        if (!_eventTypes.TryGetValue(eventType, out var eventTypeInfo))
        {
            var availableTypes = string.Join(", ", _eventTypes.Keys);
            throw new ArgumentException($"Event type '{eventType}' not registered. Available types: [{availableTypes}]");
        }

        return eventTypeInfo.ConvertFromDomainEvent(domainEvent, targetVersion);
    }

    public IDomainEvent ConvertToDomainEvent(IVersionedEventContract contract)
    {
        var eventType = contract.EventType;
        
        if (!_eventTypes.TryGetValue(eventType, out var eventTypeInfo))
        {
            throw new ArgumentException($"Event type '{eventType}' not registered");
        }

        return eventTypeInfo.ConvertToDomainEvent(contract);
    }

    public Type? GetContractType(string eventType, int version)
    {
        if (_eventTypes.TryGetValue(eventType, out var eventTypeInfo))
        {
            return eventTypeInfo.GetContractType(version);
        }
        return null;
    }

    public int GetLatestVersion(string eventType)
    {
        if (_eventTypes.TryGetValue(eventType, out var eventTypeInfo))
        {
            return eventTypeInfo.GetLatestVersion();
        }
        throw new ArgumentException($"Event type '{eventType}' not registered");
    }

    private class EventTypeInfo
    {
        private readonly ConcurrentDictionary<int, VersionInfo> _versions = new();

        public void RegisterVersion(int version, Type contractType, object fromDomainEvent, object toDomainEvent)
        {
            _versions[version] = new VersionInfo(contractType, fromDomainEvent, toDomainEvent);
        }

        public Type? GetContractType(int version)
        {
            return _versions.TryGetValue(version, out var info) ? info.ContractType : null;
        }

        public int GetLatestVersion()
        {
            return _versions.Keys.Max();
        }

        public IEnumerable<int> GetSupportedVersions()
        {
            return _versions.Keys.OrderBy(v => v);
        }

        public IVersionedEventContract ConvertFromDomainEvent(IDomainEvent domainEvent, int targetVersion)
        {
            if (!_versions.TryGetValue(targetVersion, out var versionInfo))
            {
                var availableVersions = string.Join(", ", _versions.Keys);
                throw new ArgumentException($"Schema version {targetVersion} not supported. Available versions: [{availableVersions}]");
            }

            var converter = versionInfo.FromDomainEvent;
            var method = converter.GetType().GetMethod("Invoke");
            
            try
            {
                var result = method?.Invoke(converter, new object[] { domainEvent });
                return (IVersionedEventContract)result!;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to convert domain event to version {targetVersion}", ex);
            }
        }

        public IDomainEvent ConvertToDomainEvent(IVersionedEventContract contract)
        {
            if (!_versions.TryGetValue(contract.SchemaVersion, out var versionInfo))
            {
                throw new ArgumentException($"Schema version {contract.SchemaVersion} not supported");
            }

            var converter = versionInfo.ToDomainEvent;
            var method = converter.GetType().GetMethod("Invoke");
            var result = method?.Invoke(converter, new object[] { contract });
            
            return (IDomainEvent)result!;
        }

        private record VersionInfo(Type ContractType, object FromDomainEvent, object ToDomainEvent);
    }
}