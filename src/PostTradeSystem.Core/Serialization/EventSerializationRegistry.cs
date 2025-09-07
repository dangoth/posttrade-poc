using PostTradeSystem.Core.Events;
using System.Collections.Concurrent;

namespace PostTradeSystem.Core.Serialization;

public class EventSerializationRegistry
{
    private readonly ConcurrentDictionary<string, EventTypeRegistry> _eventTypes = new();
    private readonly ConcurrentDictionary<string, object> _converters = new();

    public void RegisterContract<T>(Func<IDomainEvent, T> fromDomainEvent, Func<T, IDomainEvent> toDomainEvent)
        where T : IVersionedEventContract
    {
        var contract = Activator.CreateInstance<T>();
        var eventType = contract.EventType;
        var version = contract.Version;

        var registry = _eventTypes.GetOrAdd(eventType, _ => new EventTypeRegistry());
        registry.RegisterVersion(version, typeof(T), fromDomainEvent, toDomainEvent);
    }

    public void RegisterConverter<TFrom, TTo>(IEventVersionConverter<TFrom, TTo> converter)
        where TFrom : IVersionedEventContract
        where TTo : IVersionedEventContract
    {
        var key = $"{typeof(TFrom).Name}->{typeof(TTo).Name}";
        _converters[key] = converter;
    }

    public void RegisterConverter(object converter)
    {
        var converterType = converter.GetType();
        var interfaces = converterType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventVersionConverter<,>))
            .ToArray();

        foreach (var interfaceType in interfaces)
        {
            var genericArgs = interfaceType.GetGenericArguments();
            var fromType = genericArgs[0];
            var toType = genericArgs[1];
            var key = $"{fromType.Name}->{toType.Name}";
            _converters[key] = converter;
        }
    }

    public Type? GetContractType(string eventType, int version)
    {
        if (_eventTypes.TryGetValue(eventType, out var registry))
        {
            return registry.GetContractType(version);
        }
        return null;
    }

    public int GetLatestVersion(string eventType)
    {
        if (_eventTypes.TryGetValue(eventType, out var registry))
        {
            return registry.GetLatestVersion();
        }
        throw new ArgumentException($"Event type '{eventType}' not registered");
    }

    public IEnumerable<int> GetSupportedVersions(string eventType)
    {
        if (_eventTypes.TryGetValue(eventType, out var registry))
        {
            return registry.GetSupportedVersions();
        }
        return Enumerable.Empty<int>();
    }

    public IVersionedEventContract ConvertFromDomainEvent(IDomainEvent domainEvent, int targetVersion)
    {
        var typeName = domainEvent.GetType().Name;
        var eventType = typeName.EndsWith("Event") ? typeName[..^5] : typeName;
        
        if (!_eventTypes.TryGetValue(eventType, out var registry))
        {
            throw new ArgumentException($"Event type '{eventType}' not registered");
        }

        return registry.ConvertFromDomainEvent(domainEvent, targetVersion);
    }

    public IDomainEvent ConvertToDomainEvent(IVersionedEventContract contract)
    {
        var eventType = contract.EventType;
        
        if (!_eventTypes.TryGetValue(eventType, out var registry))
        {
            throw new ArgumentException($"Event type '{eventType}' not registered");
        }

        return registry.ConvertToDomainEvent(contract);
    }

    public TTo ConvertVersion<TFrom, TTo>(TFrom source)
        where TFrom : IVersionedEventContract
        where TTo : IVersionedEventContract
    {
        var key = $"{typeof(TFrom).Name}->{typeof(TTo).Name}";
        
        if (_converters.TryGetValue(key, out var converter) && 
            converter is IEventVersionConverter<TFrom, TTo> typedConverter)
        {
            return typedConverter.Convert(source);
        }

        throw new InvalidOperationException($"No converter registered for {typeof(TFrom).Name} -> {typeof(TTo).Name}");
    }

    private class EventTypeRegistry
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
                throw new ArgumentException($"Version {targetVersion} not supported");
            }

            var converter = versionInfo.FromDomainEvent;
            var method = converter.GetType().GetMethod("Invoke");
            var result = method?.Invoke(converter, new object[] { domainEvent });
            
            return (IVersionedEventContract)result!;
        }

        public IDomainEvent ConvertToDomainEvent(IVersionedEventContract contract)
        {
            if (!_versions.TryGetValue(contract.Version, out var versionInfo))
            {
                throw new ArgumentException($"Version {contract.Version} not supported");
            }

            var converter = versionInfo.ToDomainEvent;
            var method = converter.GetType().GetMethod("Invoke");
            var result = method?.Invoke(converter, new object[] { contract });
            
            return (IDomainEvent)result!;
        }

        private record VersionInfo(Type ContractType, object FromDomainEvent, object ToDomainEvent);
    }
}