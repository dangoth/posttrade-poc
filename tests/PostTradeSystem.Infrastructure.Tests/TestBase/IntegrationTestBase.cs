using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Moq;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Core.Services;
using PostTradeSystem.Infrastructure.Data;
using PostTradeSystem.Infrastructure.Extensions;
using PostTradeSystem.Infrastructure.Kafka;
using PostTradeSystem.Infrastructure.Repositories;
using PostTradeSystem.Infrastructure.Schemas;
using PostTradeSystem.Infrastructure.Services;
using PostTradeSystem.Infrastructure.Tests.Integration;

namespace PostTradeSystem.Infrastructure.Tests.TestBase;

public abstract class IntegrationTestBase : SqlServerTestBase
{
    protected readonly JsonSchemaValidator SchemaValidator;
    protected readonly SerializationManagementService SerializationService;
    protected readonly IEventStoreRepository EventStoreRepository;
    protected readonly IOutboxService OutboxService;
    protected readonly IOutboxRepository OutboxRepository;
    private readonly IServiceScope _serviceScope;

    protected IntegrationTestBase(SqlServerFixture fixture) : base(fixture)
    {
        var serviceProvider = CreateServiceProvider(fixture);
        _serviceScope = serviceProvider.CreateScope();
        
        SchemaValidator = serviceProvider.GetRequiredService<JsonSchemaValidator>();
        SerializationService = (SerializationManagementService)serviceProvider.GetRequiredService<ISerializationManagementService>();
        EventStoreRepository = _serviceScope.ServiceProvider.GetRequiredService<IEventStoreRepository>();
        OutboxService = _serviceScope.ServiceProvider.GetRequiredService<IOutboxService>();
        OutboxRepository = _serviceScope.ServiceProvider.GetRequiredService<IOutboxRepository>();
    }

    private static IServiceProvider CreateServiceProvider(SqlServerFixture fixture)
    {
        var services = new ServiceCollection();
        
        services.AddSingleton(fixture.Context);
        services.AddSingleton<PostTradeDbContext>(fixture.Context);
        
        services.AddSingleton<JsonSchemaValidator>();
        services.AddSingleton<ISchemaRegistry, InMemorySchemaRegistry>();
        services.AddSingleton<ITradeRiskService, TradeRiskService>();
        
        services.AddCompleteSerializationSetup();
        
        var mockKafkaProducer = new Mock<IKafkaProducerService>();
        services.AddSingleton(mockKafkaProducer.Object);
        
        services.AddSingleton<ITimeProvider>(new SystemTimeProvider());
        
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IEventStoreRepository, EventStoreRepository>();
        services.AddScoped<IRetryService, RetryService>();
        services.AddScoped<IOutboxService, OutboxService>();
        
        services.AddSingleton(Mock.Of<ILogger<EventStoreRepository>>());
        services.AddSingleton(Mock.Of<ILogger<OutboxService>>());
        services.AddSingleton(Mock.Of<ILogger<RetryService>>());
        
        var serviceProvider = services.BuildServiceProvider();
        
        var serializationService = serviceProvider.GetRequiredService<ISerializationManagementService>();
        serializationService.InitializeAsync().GetAwaiter().GetResult();
        
        return serviceProvider;
    }


    public override async Task DisposeAsync()
    {
        _serviceScope?.Dispose();
        await base.DisposeAsync();
    }
}