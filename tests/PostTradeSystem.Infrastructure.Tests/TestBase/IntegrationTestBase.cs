using Microsoft.Extensions.DependencyInjection;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Infrastructure.Data;
using PostTradeSystem.Infrastructure.Extensions;
using PostTradeSystem.Infrastructure.Repositories;
using PostTradeSystem.Infrastructure.Tests.Integration;

namespace PostTradeSystem.Infrastructure.Tests.TestBase;

public abstract class IntegrationTestBase : SqlServerTestBase
{
    protected readonly JsonSchemaValidator SchemaValidator;
    protected readonly SerializationManagementService SerializationService;
    protected readonly IEventStoreRepository EventStoreRepository;

    protected IntegrationTestBase(SqlServerFixture fixture) : base(fixture)
    {
        var serviceProvider = CreateServiceProvider(fixture);
        
        SchemaValidator = serviceProvider.GetRequiredService<JsonSchemaValidator>();
        SerializationService = serviceProvider.GetRequiredService<SerializationManagementService>();
        EventStoreRepository = serviceProvider.GetRequiredService<IEventStoreRepository>();
    }

    private static IServiceProvider CreateServiceProvider(SqlServerFixture fixture)
    {
        var services = new ServiceCollection();
        
        services.AddSingleton<PostTradeDbContext>(fixture.Context);
        
        services.AddSingleton<IEventStoreRepository, EventStoreRepository>();
        services.AddScoped(typeof(IAggregateRepository<>), typeof(AggregateRepository<>));
        
        services.AddSingleton<JsonSchemaValidator>(provider =>
        {
            var validator = new JsonSchemaValidator();
            validator.RegisterSchema("EquityTradeMessage", MessageSchemas.EquityTradeMessageSchema);
            validator.RegisterSchema("FxTradeMessage", MessageSchemas.FxTradeMessageSchema);
            validator.RegisterSchema("OptionTradeMessage", MessageSchemas.OptionTradeMessageSchema);
            validator.RegisterSchema("TradeMessage", MessageSchemas.TradeMessageSchema);
            validator.RegisterSchema("TradeMessageEnvelope", MessageSchemas.TradeMessageEnvelopeSchema);
            
            return validator;
        });
        
        services.AddCompleteSerializationSetup();
        
        services.AddLogging();
        
        var serviceProvider = services.BuildServiceProvider();
        
        serviceProvider.InitializeSerializationAsync().GetAwaiter().GetResult();
        
        return serviceProvider;
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }
}