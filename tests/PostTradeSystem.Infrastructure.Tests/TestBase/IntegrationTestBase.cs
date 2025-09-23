using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Infrastructure.Extensions;
using PostTradeSystem.Core.Serialization;
using PostTradeSystem.Infrastructure.Data;
using PostTradeSystem.Infrastructure.Repositories;
using PostTradeSystem.Infrastructure.Serialization;
using PostTradeSystem.Infrastructure.Tests.Integration;

namespace PostTradeSystem.Infrastructure.Tests.TestBase;

public abstract class IntegrationTestBase : SqlServerTestBase
{
    protected readonly IServiceProvider ServiceProvider;
    private readonly JsonSchemaValidator _schemaValidator;
    private readonly SerializationManagementService _serializationService;

    protected IntegrationTestBase(SqlServerFixture fixture) : base(fixture)
    {
        ServiceProvider = CreateServiceProvider(fixture);
        
        _schemaValidator = ServiceProvider.GetRequiredService<JsonSchemaValidator>();
        _serializationService = ServiceProvider.GetRequiredService<SerializationManagementService>();
    }

    protected JsonSchemaValidator SchemaValidator => _schemaValidator;
    protected SerializationManagementService SerializationService => _serializationService;
    
    protected IEventStoreRepository EventStoreRepository => ServiceProvider.GetRequiredService<IEventStoreRepository>();

    private static IServiceProvider CreateServiceProvider(SqlServerFixture fixture)
    {
        var services = new ServiceCollection();
        
        // Register the shared DbContext instance from the fixture
        services.AddSingleton<PostTradeDbContext>(fixture.Context);
        
        services.AddScoped<IEventStoreRepository, EventStoreRepository>();
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
        
        using var scope = serviceProvider.CreateScope();
        var serializationService = scope.ServiceProvider.GetRequiredService<SerializationManagementService>();
        
        
        scope.ServiceProvider.InitializeSerializationAsync().GetAwaiter().GetResult();
        
        return serviceProvider;
    }

    protected IServiceScope CreateScope() => ServiceProvider.CreateScope();

    protected T GetService<T>() where T : notnull => ServiceProvider.GetRequiredService<T>();
    
    protected T GetScopedService<T>() where T : notnull
    {
        using var scope = CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }

    public override async Task DisposeAsync()
    {
        if (ServiceProvider is IDisposable disposable)
            disposable.Dispose();
        await base.DisposeAsync();
    }
}