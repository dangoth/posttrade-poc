using PostTradeSystem.Core.Serialization.Contracts;

namespace PostTradeSystem.Core.Serialization;
public abstract class EventVersionConverterBase<TFrom, TTo> : IEventVersionConverter<TFrom, TTo>
    where TFrom : BaseEventContract
    where TTo : BaseEventContract
{
    public abstract TTo Convert(TFrom source);
    
    public abstract bool CanConvert(int fromVersion, int toVersion);

    /// <summary>
    /// Helper method to copy base properties from source to target
    /// </summary>
    protected static void CopyBaseProperties(TFrom source, TTo target)
    {
        EventContractPropertyMapper.MapBaseProperties(source, target);
    }

    /// <summary>
    /// Helper method to copy dictionary properties safely
    /// </summary>
    protected static Dictionary<string, object> CopyDictionary(Dictionary<string, object> source)
    {
        return new Dictionary<string, object>(source);
    }

    /// <summary>
    /// Helper method to determine regulatory classification based on trade type
    /// </summary>
    protected static string DetermineRegulatoryClassification(string tradeType)
    {
        return tradeType.ToUpper() switch
        {
            "EQUITY" => "MiFID_II_EQUITY",
            "OPTION" => "MiFID_II_DERIVATIVE", 
            "FX" => "EMIR_FX",
            _ => "UNCLASSIFIED"
        };
    }

    /// <summary>
    /// Helper method to calculate notional value
    /// </summary>
    protected static decimal CalculateNotionalValue(decimal quantity, decimal price)
    {
        return quantity * price;
    }

    /// <summary>
    /// Helper method to create audit trail message
    /// </summary>
    protected static string CreateAuditTrail(string previousStatus, string newStatus, string reason)
    {
        return $"Status changed from {previousStatus} to {newStatus}. Reason: {reason}";
    }
}