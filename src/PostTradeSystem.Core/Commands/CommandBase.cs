namespace PostTradeSystem.Core.Commands;

public abstract class CommandBase : ICommand
{
    protected CommandBase(string aggregateId, string correlationId, string causedBy)
    {
        CommandId = Guid.NewGuid().ToString();
        AggregateId = aggregateId;
        CorrelationId = correlationId;
        CausedBy = causedBy;
        IssuedAt = DateTime.UtcNow;
    }

    public string CommandId { get; private set; }
    public string AggregateId { get; private set; }
    public string CorrelationId { get; private set; }
    public string CausedBy { get; private set; }
    public DateTime IssuedAt { get; private set; }
}