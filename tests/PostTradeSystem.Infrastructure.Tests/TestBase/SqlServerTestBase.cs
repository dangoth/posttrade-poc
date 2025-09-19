using Microsoft.EntityFrameworkCore;
using PostTradeSystem.Infrastructure.Data;
using PostTradeSystem.Infrastructure.Tests.Integration;
using Xunit;

namespace PostTradeSystem.Infrastructure.Tests.TestBase;

[Collection("SqlServer")]
public abstract class SqlServerTestBase : IAsyncLifetime
{
    protected PostTradeDbContext Context => _fixture.Context;
    private readonly SqlServerFixture _fixture;

    protected SqlServerTestBase(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public virtual async Task InitializeAsync()
    {
        // Reset database to clean state before each test
        await _fixture.ResetDatabaseAsync();
    }

    public virtual async Task DisposeAsync()
    {
        // Nothing to dispose - fixture handles the context
        await Task.CompletedTask;
    }
}