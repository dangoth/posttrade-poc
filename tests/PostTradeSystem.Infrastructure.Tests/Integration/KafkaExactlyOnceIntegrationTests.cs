using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostTradeSystem.Infrastructure.Configuration;
using PostTradeSystem.Infrastructure.Kafka;
using PostTradeSystem.Infrastructure.Tests.TestBase;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.Messages;
using Xunit;
using Moq;

namespace PostTradeSystem.Infrastructure.Tests.Integration;

[Collection("SqlServer")]
public class KafkaExactlyOnceIntegrationTests : IntegrationTestBase
{
    public KafkaExactlyOnceIntegrationTests(SqlServerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public void KafkaProducerService_WithExactlyOnceEnabled_InitializesCorrectly()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var testConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kafka:BootstrapServers"] = "localhost:9092"
            })
            .Build();
        services.AddSingleton<IConfiguration>(testConfig);
        
        services.Configure<KafkaExactlyOnceConfiguration>(options =>
        {
            options.EnableExactlyOnceSemantics = true;
            options.EnableIdempotentProducer = true;
            options.TransactionTimeoutMs = 30000;
        });

        var mockSerializationService = new Mock<ISerializationManagementService>();
        services.AddSingleton(mockSerializationService.Object);

        var serviceProvider = services.BuildServiceProvider();
        
        var kafkaConfig = serviceProvider.GetRequiredService<IOptions<KafkaExactlyOnceConfiguration>>();
        
        Assert.True(kafkaConfig.Value.EnableExactlyOnceSemantics);
        Assert.True(kafkaConfig.Value.EnableIdempotentProducer);
    }

    [Fact]
    public void KafkaConsumerService_WithExactlyOnceEnabled_ConfiguresCorrectly()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var testConfig2 = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kafka:BootstrapServers"] = "localhost:9092"
            })
            .Build();
        services.AddSingleton<IConfiguration>(testConfig2);
        
        services.Configure<KafkaExactlyOnceConfiguration>(options =>
        {
            options.EnableExactlyOnceSemantics = true;
            options.EnableManualOffsetManagement = true;
            options.ConsumerGroupId = "test-group";
        });

        var serviceProvider = services.BuildServiceProvider();
        var kafkaConfig = serviceProvider.GetRequiredService<IOptions<KafkaExactlyOnceConfiguration>>();
        
        Assert.True(kafkaConfig.Value.EnableExactlyOnceSemantics);
        Assert.True(kafkaConfig.Value.EnableManualOffsetManagement);
        Assert.Equal("test-group", kafkaConfig.Value.ConsumerGroupId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void KafkaExactlyOnceConfiguration_FromConfiguration_LoadsCorrectly(bool enableExactlyOnce)
    {
        var configData = new Dictionary<string, string?>
        {
            ["Kafka:ExactlyOnce:EnableExactlyOnceSemantics"] = enableExactlyOnce.ToString(),
            ["Kafka:ExactlyOnce:ConsumerGroupId"] = "test-consumer-group",
            ["Kafka:ExactlyOnce:TransactionTimeoutMs"] = "45000"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.Configure<KafkaExactlyOnceConfiguration>(
            configuration.GetSection(KafkaExactlyOnceConfiguration.SectionName));

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<KafkaExactlyOnceConfiguration>>();

        Assert.Equal(enableExactlyOnce, options.Value.EnableExactlyOnceSemantics);
        Assert.Equal("test-consumer-group", options.Value.ConsumerGroupId);
        Assert.Equal(45000, options.Value.TransactionTimeoutMs);
    }
}