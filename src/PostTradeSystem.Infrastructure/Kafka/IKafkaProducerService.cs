using Confluent.Kafka;
using PostTradeSystem.Core.Messages;

namespace PostTradeSystem.Infrastructure.Kafka;

public interface IKafkaProducerService : IDisposable
{
    Task<DeliveryResult<string, string>> ProduceAsync<T>(string topic, T message) where T : TradeMessage;
    Task<DeliveryResult<string, string>> ProduceAsync(
        string topic, 
        string key, 
        string value, 
        Dictionary<string, string>? headers = null, 
        CancellationToken cancellationToken = default);
}