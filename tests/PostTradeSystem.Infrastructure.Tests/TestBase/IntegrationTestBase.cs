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
    protected readonly IJsonSchemaValidator SchemaValidator;
    protected readonly SerializationManagementService SerializationService;
    protected readonly IEventStoreRepository EventStoreRepository;
    protected readonly IOutboxService OutboxService;
    protected readonly IOutboxRepository OutboxRepository;
    private readonly IServiceScope _serviceScope;
    protected readonly IServiceProvider ServiceProvider;

    protected IntegrationTestBase(SqlServerFixture fixture) : base(fixture)
    {
        ServiceProvider = CreateServiceProvider(fixture);
        _serviceScope = ServiceProvider.CreateScope();
        
        SchemaValidator = ServiceProvider.GetRequiredService<IJsonSchemaValidator>();
        SerializationService = (SerializationManagementService)ServiceProvider.GetRequiredService<ISerializationManagementService>();
        EventStoreRepository = _serviceScope.ServiceProvider.GetRequiredService<IEventStoreRepository>();
        OutboxService = _serviceScope.ServiceProvider.GetRequiredService<IOutboxService>();
        OutboxRepository = _serviceScope.ServiceProvider.GetRequiredService<IOutboxRepository>();
    }

    protected T GetRequiredService<T>() where T : notnull
    {
        return _serviceScope.ServiceProvider.GetRequiredService<T>();
    }

    private static IServiceProvider CreateServiceProvider(SqlServerFixture fixture)
    {
        var services = new ServiceCollection();
        
        services.AddSingleton(fixture.Context);
        services.AddSingleton<PostTradeDbContext>(fixture.Context);
        
        services.AddSingleton<ISchemaRegistry, InMemorySchemaRegistry>();
        services.AddSingleton<ITradeRiskService, TradeRiskService>();
        
        services.AddCompleteSerializationSetup();
        
        services.AddSingleton<IJsonSchemaValidator>(provider =>
        {
            var validator = new JsonSchemaValidator();
            
            validator.RegisterSchema("EquityTradeMessage", MessageSchemas.EquityTradeMessageSchema);
            validator.RegisterSchema("FxTradeMessage", MessageSchemas.FxTradeMessageSchema);
            validator.RegisterSchema("OptionTradeMessage", MessageSchemas.OptionTradeMessageSchema);
            validator.RegisterSchema("TradeMessage", MessageSchemas.TradeMessageSchema);
            validator.RegisterSchema("TradeMessageEnvelope", MessageSchemas.TradeMessageEnvelopeSchema);
            
            return validator;
        });
        
        var mockKafkaProducer = new Mock<IKafkaProducerService>();
        services.AddSingleton(mockKafkaProducer.Object);
        
        services.AddSingleton<ITimeProvider>(new SystemTimeProvider());
        
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IEventStoreRepository, EventStoreRepository>();
        services.AddScoped<IRetryService, RetryService>();
        services.AddScoped<IOutboxService, OutboxService>();
        
        services.AddScoped(typeof(IAggregateRepository<>), typeof(AggregateRepository<>));
        services.AddScoped<Func<string, string, IEnumerable<Core.Events.IDomainEvent>, Core.Aggregates.TradeAggregate>>(
            provider => (id, partitionKey, events) => Core.Aggregates.TradeAggregate.FromHistory(id, partitionKey, events));
        
        services.AddScoped<Core.Handlers.ICommandHandler<Core.Commands.CreateTradeCommand>, Infrastructure.Handlers.CreateTradeCommandHandler>();
        services.AddScoped<Core.Handlers.ICommandHandler<Core.Commands.UpdateTradeStatusCommand>, Infrastructure.Handlers.UpdateTradeStatusCommandHandler>();
        services.AddScoped<Core.Handlers.ICommandHandler<Core.Commands.EnrichTradeCommand>, Infrastructure.Handlers.EnrichTradeCommandHandler>();
        services.AddScoped<Core.Handlers.ICommandHandler<Core.Commands.ValidateTradeCommand>, Infrastructure.Handlers.ValidateTradeCommandHandler>();
        
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