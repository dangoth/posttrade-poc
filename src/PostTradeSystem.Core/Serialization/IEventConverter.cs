namespace PostTradeSystem.Core.Serialization;

public interface IEventConverter<TFrom, TTo> 
    where TFrom : IVersionedEventContract 
    where TTo : IVersionedEventContract
{
    TTo Convert(TFrom source);
    bool CanConvert(int fromVersion, int toVersion);
}