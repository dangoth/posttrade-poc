namespace PostTradeSystem.Infrastructure.Configuration;

public class KafkaExactlyOnceConfiguration
{
    public const string SectionName = "Kafka:ExactlyOnce";
    
    public bool EnableExactlyOnceSemantics { get; set; } = true;
    public int TransactionTimeoutMs { get; set; } = 60000;
    public int ProducerMaxInFlight { get; set; } = 1;
    public int ConsumerSessionTimeoutMs { get; set; } = 30000;
    public int ConsumerHeartbeatIntervalMs { get; set; } = 10000;
    public int ConsumerMaxPollIntervalMs { get; set; } = 300000;
    public string ConsumerGroupId { get; set; } = "posttrade-consumer-group";
    public bool EnableManualOffsetManagement { get; set; } = true;
    public bool EnableIdempotentProducer { get; set; } = true;
}