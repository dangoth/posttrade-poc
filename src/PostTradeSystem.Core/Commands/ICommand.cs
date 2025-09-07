namespace PostTradeSystem.Core.Commands;

public interface ICommand
{
    string CommandId { get; }
    string AggregateId { get; }
    string CorrelationId { get; }
    string CausedBy { get; }
    DateTime IssuedAt { get; }
}

public interface ICommand<TResult> : ICommand
{
}