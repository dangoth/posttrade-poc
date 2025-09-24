using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PostTradeSystem.Core.Aggregates;
using PostTradeSystem.Infrastructure.Data;
using PostTradeSystem.Infrastructure.Extensions;
using PostTradeSystem.Infrastructure.Repositories;
using PostTradeSystem.Infrastructure.Serialization;
using PostTradeSystem.Infrastructure.Tests.Integration;
using PostTradeSystem.Infrastructure.Tests.TestBase;
using Xunit;

namespace PostTradeSystem.Infrastructure.Tests.Extensions;

[Collection("SqlServer")]
public class ServiceCollectionExtensionsTests : IntegrationTestBase
{
    public ServiceCollectionExtensionsTests(SqlServerFixture fixture) : base(fixture)
    {
    }
    [Fact]
    public void AddInfrastructure_RegistersAllRequiredServices()
    {
        var services = new ServiceCollection();
        var configData = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=TestDb;Trusted_Connection=true"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        services.AddInfrastructure(configuration);
        
        // Add serialization services as they are required dependencies
        services.AddInfrastructureSerialization();

        var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetService<PostTradeDbContext>().Should().NotBeNull();
        serviceProvider.GetService<IEventStoreRepository>().Should().NotBeNull();
        serviceProvider.GetService<IAggregateRepository<TradeAggregate>>().Should().NotBeNull();
        serviceProvider.GetService<PostTradeSystem.Core.Serialization.IEventSerializer>().Should().NotBeNull();
        serviceProvider.GetService<PostTradeSystem.Core.Serialization.EventSerializationRegistry>().Should().NotBeNull();
        serviceProvider.GetService<PostTradeSystem.Core.Serialization.SerializationManagementService>().Should().NotBeNull();
        serviceProvider.GetService<PostTradeSystem.Core.Schemas.ISchemaRegistry>().Should().NotBeNull();
        serviceProvider.GetService<PostTradeSystem.Core.Schemas.JsonSchemaValidator>().Should().NotBeNull();
        
        var aggregateFactory = serviceProvider.GetService<Func<string, string, IEnumerable<PostTradeSystem.Core.Events.IDomainEvent>, TradeAggregate>>();
        aggregateFactory.Should().NotBeNull();
    }

    [Fact]
    public void AddInfrastructure_ConfiguresDbContextWithRetryPolicy()
    {
        var services = new ServiceCollection();
        var configData = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=TestDb;Trusted_Connection=true"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        services.AddInfrastructure(configuration);

        var serviceProvider = services.BuildServiceProvider();
        var dbContext = serviceProvider.GetRequiredService<PostTradeDbContext>();

        dbContext.Should().NotBeNull();
        dbContext.Database.Should().NotBeNull();
    }

    [Fact]
    public void AddInfrastructure_DoesNotRegisterHostedServices()
    {
        var services = new ServiceCollection();
        var configData = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=TestDb;Trusted_Connection=true"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        services.AddInfrastructure(configuration);

        // AddInfrastructure should not register any hosted services
        // Database initialization is now handled in Program.cs
        var hostedServiceDescriptors = services.Where(s => 
            s.ServiceType.Name == "IHostedService");

        hostedServiceDescriptors.Should().BeEmpty();
    }

    [Fact]
    public void AddInfrastructure_ConfiguresCorrectServiceLifetimes()
    {
        var services = new ServiceCollection();
        var configData = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=TestDb;Trusted_Connection=true"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        services.AddInfrastructure(configuration);

        var dbContextDescriptor = services.First(s => s.ServiceType == typeof(PostTradeDbContext));
        dbContextDescriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);

        var eventStoreDescriptor = services.First(s => s.ServiceType == typeof(IEventStoreRepository));
        eventStoreDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);

        var aggregateRepoDescriptor = services.First(s => s.ServiceType == typeof(IAggregateRepository<>));
        aggregateRepoDescriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }
}