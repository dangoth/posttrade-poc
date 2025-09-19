namespace PostTradeSystem.Core.Serialization;

public interface IVersionedEventContract
{
    int SchemaVersion { get; }
    string EventType { get; }
}

public abstract class VersionedEventContractBase : IVersionedEventContract
{
    public abstract int SchemaVersion { get; }
    public abstract string EventType { get; }
}

public interface IEventVersionConverter<TFrom, TTo>
    where TFrom : IVersionedEventContract
    where TTo : IVersionedEventContract
{
    TTo Convert(TFrom source);
    bool CanConvert(int fromVersion, int toVersion);
}