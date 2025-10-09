using Confluent.Kafka;
using PostTradeSystem.Core.Messages;
using PostTradeSystem.Core.Common;

namespace PostTradeSystem.Infrastructure.Kafka;

public interface IKafkaProducerService : IDisposable
{
    Task<Result<DeliveryResult<string, string>>> ProduceAsync<T>(string topic, T message) where T : TradeMessage;
    Task<Result<DeliveryResult<string, string>>> ProduceAsync(
        string topic, 
        string key, 
        string value, 
        Dictionary<string, string>? headers = null, 
        CancellationToken cancellationToken = default);
}