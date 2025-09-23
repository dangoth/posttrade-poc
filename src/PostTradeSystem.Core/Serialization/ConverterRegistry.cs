using System.Collections.Concurrent;
using System.Reflection;

namespace PostTradeSystem.Core.Serialization;

public class ConverterRegistry
{
    private readonly ConcurrentDictionary<string, object> _converters = new();

    public void RegisterConverter<TFrom, TTo>(IEventConverter<TFrom, TTo> converter)
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
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventConverter<,>))
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

    public TTo Convert<TFrom, TTo>(TFrom source)
        where TFrom : IVersionedEventContract
        where TTo : IVersionedEventContract
    {
        var key = $"{typeof(TFrom).Name}->{typeof(TTo).Name}";
        
        if (_converters.TryGetValue(key, out var converter) && 
            converter is IEventConverter<TFrom, TTo> typedConverter)
        {
            return typedConverter.Convert(source);
        }

        throw new InvalidOperationException($"No converter registered for {typeof(TFrom).Name} -> {typeof(TTo).Name}");
    }

    public bool CanConvert<TFrom, TTo>()
        where TFrom : IVersionedEventContract
        where TTo : IVersionedEventContract
    {
        var key = $"{typeof(TFrom).Name}->{typeof(TTo).Name}";
        return _converters.ContainsKey(key);
    }

    public bool CanConvert(Type fromType, Type toType)
    {
        var key = $"{fromType.Name}->{toType.Name}";
        return _converters.ContainsKey(key);
    }

    public void AutoRegisterConverters(Assembly assembly)
    {
        var converterTypes = assembly.GetTypes()
            .Where(t => t.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventConverter<,>)))
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .ToArray();

        foreach (var converterType in converterTypes)
        {
            try
            {
                var instance = Activator.CreateInstance(converterType);
                if (instance != null)
                {
                    RegisterConverter(instance);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to auto-register converter {converterType.Name}", ex);
            }
        }
    }
}