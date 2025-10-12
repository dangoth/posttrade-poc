using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PostTradeSystem.Core.Aggregates;
using PostTradeSystem.Infrastructure.Data;
using PostTradeSystem.Infrastructure.Extensions;
using PostTradeSystem.Infrastructure.Repositories;
using PostTradeSystem.Infrastructure.Services;
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
        services.AddInfrastructureSerialization();
        services.AddLogging();

        services.Should().Contain(s => s.ServiceType == typeof(PostTradeDbContext));
        services.Should().Contain(s => s.ServiceType == typeof(IEventStoreRepository));
        services.Should().Contain(s => s.ServiceType == typeof(IAggregateRepository<>));
        services.Should().Contain(s => s.ServiceType == typeof(IRetryService));
        services.Should().Contain(s => s.ServiceType == typeof(PostTradeSystem.Core.Serialization.IEventSerializer));
        services.Should().Contain(s => s.ServiceType == typeof(PostTradeSystem.Core.Serialization.EventSerializationRegistry));
        services.Should().Contain(s => s.ServiceType == typeof(PostTradeSystem.Core.Serialization.ISerializationManagementService));
        services.Should().Contain(s => s.ServiceType == typeof(PostTradeSystem.Core.Schemas.ISchemaRegistry));
        services.Should().Contain(s => s.ServiceType == typeof(PostTradeSystem.Core.Schemas.IJsonSchemaValidator));
        services.Should().Contain(s => s.ServiceType == typeof(Func<string, string, IEnumerable<PostTradeSystem.Core.Events.IDomainEvent>, TradeAggregate>));
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

        var dbContextDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(PostTradeDbContext));
        dbContextDescriptor.Should().NotBeNull();
        dbContextDescriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
        
    }

    [Fact]
    public void AddInfrastructure_RegistersInitializationHostedServices()
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
        var hostedServiceDescriptors = services.Where(s => 
            s.ServiceType.Name == "IHostedService");

        hostedServiceDescriptors.Should().HaveCount(2);
        hostedServiceDescriptors.Should().Contain(s => s.ImplementationType != null && s.ImplementationType.Name == "DatabaseMigrationService");
        hostedServiceDescriptors.Should().Contain(s => s.ImplementationType != null && s.ImplementationType.Name == "SerializationInitializationService");
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
        eventStoreDescriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);

        var outboxRepoDescriptor = services.First(s => s.ServiceType == typeof(IOutboxRepository));
        outboxRepoDescriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);

        var aggregateRepoDescriptor = services.First(s => s.ServiceType == typeof(IAggregateRepository<>));
        aggregateRepoDescriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }
}