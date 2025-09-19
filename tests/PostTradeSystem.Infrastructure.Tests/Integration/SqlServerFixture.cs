using Testcontainers.MsSql;
using Xunit;
using Microsoft.Data.SqlClient;
using Respawn;
using PostTradeSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace PostTradeSystem.Infrastructure.Tests.Integration;

public class SqlServerFixture : IAsyncLifetime
{
    public MsSqlContainer Container { get; private set; } = null!;
    public PostTradeDbContext Context { get; private set; } = null!;
    private Respawner _respawner = null!;

    public async Task InitializeAsync()
    {
        Container = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("PostTrade123!")
            .WithCleanUp(true)
            .Build();

        await Container.StartAsync();

        // Set up database schema
        var connectionString = Container.GetConnectionString();
        var options = new DbContextOptionsBuilder<PostTradeDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        Context = new PostTradeDbContext(options);
        await Context.Database.EnsureCreatedAsync();

        // Create respawner
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            TablesToIgnore = new Respawn.Graph.Table[] { "__EFMigrationsHistory" },
            SchemasToInclude = new[] { "dbo" },
            DbAdapter = DbAdapter.SqlServer
        });
    }

    public async Task ResetDatabaseAsync()
    {
        var connectionString = Container.GetConnectionString();
        await _respawner.ResetAsync(connectionString);
        
        // Clear the EF Core change tracker after database reset
        Context.ChangeTracker.Clear();
    }

    public async Task DisposeAsync()
    {
        if (Context != null)
        {
            await Context.DisposeAsync();
        }
        if (Container != null)
        {
            await Container.DisposeAsync();
        }
    }
}