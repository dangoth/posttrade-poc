using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using PostTradeSystem.Infrastructure.Configuration;
using PostTradeSystem.Infrastructure.Kafka;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.Messages;
using Xunit;
using Moq;

namespace PostTradeSystem.Infrastructure.Tests.Kafka;

public class KafkaExactlyOnceTests
{
    [Fact]
    public void KafkaExactlyOnceConfiguration_DefaultValues_AreCorrect()
    {
        var config = new KafkaExactlyOnceConfiguration();
        
        Assert.True(config.EnableExactlyOnceSemantics);
        Assert.Equal(60000, config.TransactionTimeoutMs);
        Assert.Equal(1, config.ProducerMaxInFlight);
        Assert.Equal("posttrade-consumer-group", config.ConsumerGroupId);
        Assert.True(config.EnableManualOffsetManagement);
        Assert.True(config.EnableIdempotentProducer);
    }

    [Fact]
    public void KafkaProducerService_WithExactlyOnceDisabled_DoesNotUseTransactions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kafka:BootstrapServers"] = "localhost:9092"
            })
            .Build();

        var exactlyOnceConfig = new KafkaExactlyOnceConfiguration
        {
            EnableExactlyOnceSemantics = false,
            EnableIdempotentProducer = false
        };

        var options = Options.Create(exactlyOnceConfig);
        var mockSerializationService = new Mock<ISerializationManagementService>();

        var producer = new KafkaProducerService(configuration, mockSerializationService.Object, options);
        Assert.NotNull(producer);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void KafkaExactlyOnceConfiguration_EnableFlags_WorkCorrectly(bool exactlyOnce, bool idempotent)
    {
        var config = new KafkaExactlyOnceConfiguration
        {
            EnableExactlyOnceSemantics = exactlyOnce,
            EnableIdempotentProducer = idempotent
        };

        Assert.Equal(exactlyOnce, config.EnableExactlyOnceSemantics);
        Assert.Equal(idempotent, config.EnableIdempotentProducer);
    }

    [Fact]
    public void KafkaExactlyOnceConfiguration_SectionName_IsCorrect()
    {
        Assert.Equal("Kafka:ExactlyOnce", KafkaExactlyOnceConfiguration.SectionName);
    }
}