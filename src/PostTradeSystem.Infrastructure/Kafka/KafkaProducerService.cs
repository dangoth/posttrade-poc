using Confluent.Kafka;
using PostTradeSystem.Core.Messages;
using PostTradeSystem.Core.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using PostTradeSystem.Infrastructure.Configuration;
using PostTradeSystem.Core.Common;

namespace PostTradeSystem.Infrastructure.Kafka;

public class KafkaProducerService : IKafkaProducerService
{
    private readonly IProducer<string, string> _producer;
    private readonly ISerializationManagementService _serializationService;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly KafkaExactlyOnceConfiguration _exactlyOnceConfig;

    public KafkaProducerService(IConfiguration configuration, ISerializationManagementService serializationService, IOptions<KafkaExactlyOnceConfiguration> exactlyOnceOptions)
    {
        _exactlyOnceConfig = exactlyOnceOptions.Value;
        
        var kafkaBootstrapServers = configuration.GetSection("Kafka:BootstrapServers").Value ?? 
                                   configuration.GetConnectionString("Kafka") ?? 
                                   "localhost:9092";
        
        var config = new ProducerConfig
        {
            BootstrapServers = kafkaBootstrapServers,
            EnableIdempotence = _exactlyOnceConfig.EnableIdempotentProducer,
            Acks = Acks.All,
            MessageTimeoutMs = 30000,
            RequestTimeoutMs = 30000,
            RetryBackoffMs = 1000,
            MessageSendMaxRetries = 3,
            MaxInFlight = _exactlyOnceConfig.ProducerMaxInFlight
        };

        if (_exactlyOnceConfig.EnableExactlyOnceSemantics)
        {
            config.TransactionalId = $"posttrade-producer-{Environment.MachineName}-{Guid.NewGuid():N}";
            config.TransactionTimeoutMs = _exactlyOnceConfig.TransactionTimeoutMs;
        }

        _producer = new ProducerBuilder<string, string>(config).Build();
        
        if (_exactlyOnceConfig.EnableExactlyOnceSemantics)
        {
            _producer.InitTransactions(TimeSpan.FromSeconds(30));
        }
        
        _serializationService = serializationService;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            Converters = { 
                new JsonStringEnumConverter(),
                new DictionaryObjectJsonConverter()
            }
        };
    }

    public async Task<Result<DeliveryResult<string, string>>> ProduceAsync<T>(string topic, T message) where T : TradeMessage
    {
        var envelope = new TradeMessageEnvelope<T>
        {
            MessageId = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            Version = "1.0",
            CorrelationId = Guid.NewGuid().ToString(),
            Payload = message,
            Headers = new Dictionary<string, string>
            {
                { "messageType", message.MessageType },
                { "sourceSystem", message.SourceSystem },
                { "schemaVersion", "1.0" }
            }
        };

        var jsonMessage = JsonSerializer.Serialize(envelope, _jsonOptions);
        

        var kafkaMessage = new Message<string, string>
        {
            Key = message.GetPartitionKey(),
            Value = jsonMessage,
            Headers = new Headers
            {
                { "messageType", System.Text.Encoding.UTF8.GetBytes(message.MessageType) },
                { "version", System.Text.Encoding.UTF8.GetBytes("1.0") },
                { "timestamp", System.Text.Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()) }
            }
        };

        // Use transaction for exactly-once semantics if enabled
        try
        {
            if (_exactlyOnceConfig.EnableExactlyOnceSemantics)
            {
                _producer.BeginTransaction();
                try
                {
                    var result = await _producer.ProduceAsync(topic, kafkaMessage);
                    _producer.CommitTransaction();
                    return Result<DeliveryResult<string, string>>.Success(result);
                }
                catch (Exception ex)
                {
                    _producer.AbortTransaction();
                    return Result<DeliveryResult<string, string>>.Failure($"Transaction failed: {ex.Message}");
                }
            }
            else
            {
                var result = await _producer.ProduceAsync(topic, kafkaMessage);
                return Result<DeliveryResult<string, string>>.Success(result);
            }
        }
        catch (Exception ex)
        {
            return Result<DeliveryResult<string, string>>.Failure($"Producer failed: {ex.Message}");
        }
    }

    public async Task<Result<DeliveryResult<string, string>>> ProduceAsync(
        string topic, 
        string key, 
        string value, 
        Dictionary<string, string>? headers = null, 
        CancellationToken cancellationToken = default)
    {
        var kafkaMessage = new Message<string, string>
        {
            Key = key,
            Value = value,
            Headers = new Headers()
        };

        if (headers != null)
        {
            foreach (var header in headers)
            {
                kafkaMessage.Headers.Add(header.Key, System.Text.Encoding.UTF8.GetBytes(header.Value));
            }
        }

        // Use transaction for exactly-once semantics if enabled
        try
        {
            if (_exactlyOnceConfig.EnableExactlyOnceSemantics)
            {
                _producer.BeginTransaction();
                try
                {
                    var result = await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken);
                    _producer.CommitTransaction();
                    return Result<DeliveryResult<string, string>>.Success(result);
                }
                catch (Exception ex)
                {
                    _producer.AbortTransaction();
                    return Result<DeliveryResult<string, string>>.Failure($"Transaction failed: {ex.Message}");
                }
            }
            else
            {
                var result = await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken);
                return Result<DeliveryResult<string, string>>.Success(result);
            }
        }
        catch (Exception ex)
        {
            return Result<DeliveryResult<string, string>>.Failure($"Producer failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _producer?.Flush(TimeSpan.FromSeconds(10));
        _producer?.Dispose();
    }
}