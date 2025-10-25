using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PostTradeSystem.Core.Adapters;
using PostTradeSystem.Core.Routing;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Core.Services;
using PostTradeSystem.Infrastructure.Data;
using PostTradeSystem.Infrastructure.Repositories;
using PostTradeSystem.Infrastructure.Services;
using PostTradeSystem.Infrastructure.Configuration;
using PostTradeSystem.Infrastructure.Handlers;

namespace PostTradeSystem.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KafkaExactlyOnceConfiguration>(
            configuration.GetSection(KafkaExactlyOnceConfiguration.SectionName));
            
        services.AddSingleton<ITimeProvider, SystemTimeProvider>();

        services.AddDbContext<PostTradeDbContext>(options =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions =>
                {
                    sqlOptions.CommandTimeout(30);

                    sqlOptions.MigrationsAssembly(typeof(PostTradeDbContext).Assembly.GetName().Name);
                    sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory");
                });

            options.EnableSensitiveDataLogging(false);
            options.EnableDetailedErrors(true);
        });

        services.AddScoped(typeof(IAggregateRepository<>), typeof(AggregateRepository<>));
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IEventStoreRepository, EventStoreRepository>();
        services.AddScoped<IOutboxService, OutboxService>();
        services.AddScoped<IRetryService, RetryService>();
        // Register individual adapters with external data service dependency
        services.AddScoped<EquityTradeAdapter>();
        services.AddScoped<FxTradeAdapter>();
        services.AddScoped<OptionTradeAdapter>();
        services.AddScoped<ITradeMessageAdapterFactory, TradeMessageAdapterFactory>();
        services.AddScoped<IMessageRouter, MessageRouter>();
        services.AddScoped<ITradeRiskService, TradeRiskService>();
        services.AddScoped<IPositionAggregationService, PositionAggregationService>();
        
        // External data services for Step 7
        services.AddScoped<MockExternalDataService>();
        services.AddScoped<IExternalDataService>(provider => 
        {
            var mockService = provider.GetRequiredService<MockExternalDataService>();
            var configurableService = new ConfigurableExternalDataService(mockService);
            
            // Configure with reasonable defaults for production-like simulation
            configurableService.ConfigureLatency(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(200));
            configurableService.ConfigureFailureRate(0.02); // 2% failure rate
            configurableService.ConfigureCircuitBreaker(3, TimeSpan.FromMinutes(2));
            
            return configurableService;
        });
        
        services.AddHostedService<DatabaseMigrationService>();
        services.AddHostedService<SerializationInitializationService>();
        
        services.AddScoped<Func<string, string, IEnumerable<Core.Events.IDomainEvent>, Core.Aggregates.TradeAggregate>>(
            provider => (id, partitionKey, events) => Core.Aggregates.TradeAggregate.FromHistory(id, partitionKey, events));
        
        services.AddScoped<Core.Handlers.ICommandHandler<Core.Commands.CreateTradeCommand>, CreateTradeCommandHandler>();
        services.AddScoped<Core.Handlers.ICommandHandler<Core.Commands.UpdateTradeStatusCommand>, UpdateTradeStatusCommandHandler>();
        services.AddScoped<Core.Handlers.ICommandHandler<Core.Commands.EnrichTradeCommand>, EnrichTradeCommandHandler>();
        services.AddScoped<Core.Handlers.ICommandHandler<Core.Commands.ValidateTradeCommand>, ValidateTradeCommandHandler>();
        
        services.AddSingleton<IJsonSchemaValidator>(provider =>
        {
            var validator = new JsonSchemaValidator();
            
            // Register message schemas
            validator.RegisterSchema("EquityTradeMessage", MessageSchemas.EquityTradeMessageSchema);
            validator.RegisterSchema("FxTradeMessage", MessageSchemas.FxTradeMessageSchema);
            validator.RegisterSchema("OptionTradeMessage", MessageSchemas.OptionTradeMessageSchema);
            validator.RegisterSchema("TradeMessage", MessageSchemas.TradeMessageSchema);
            validator.RegisterSchema("TradeMessageEnvelope", MessageSchemas.TradeMessageEnvelopeSchema);
            validator.RegisterSchema("PositionSummary", MessageSchemas.PositionSummarySchema);
            
            return validator;
        });

        return services;
    }
}