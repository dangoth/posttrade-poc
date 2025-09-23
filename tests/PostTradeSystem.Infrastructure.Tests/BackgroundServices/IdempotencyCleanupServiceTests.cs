using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PostTradeSystem.Infrastructure.Repositories;
using PostTradeSystem.Infrastructure.Tests.TestBase;
using Xunit;
using FluentAssertions;
using PostTradeSystem.Infrastructure.Tests.Integration;

namespace PostTradeSystem.Infrastructure.Tests.BackgroundServices;

[Collection("SqlServer")]
public class IdempotencyCleanupServiceTests : IntegrationTestBase
{
    public IdempotencyCleanupServiceTests(SqlServerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task PerformCleanupAsync_ShouldRemoveExpiredIdempotencyKeys()
    {
        await EventStoreRepository.SaveIdempotencyAsync(
            "expired-key-1",
            "aggregate-1",
            "hash-1",
            "processed",
            TimeSpan.FromSeconds(-10));

        await EventStoreRepository.SaveIdempotencyAsync(
            "expired-key-2",
            "aggregate-2",
            "hash-2",
            "processed",
            TimeSpan.FromSeconds(-5));

        await EventStoreRepository.SaveIdempotencyAsync(
            "valid-key-1",
            "aggregate-3",
            "hash-3",
            "processed",
            TimeSpan.FromHours(1));

        await EventStoreRepository.SaveIdempotencyAsync(
            "valid-key-2",
            "aggregate-4",
            "hash-4",
            "processed",
            TimeSpan.FromHours(24));

        var totalCount = await Context.IdempotencyKeys.CountAsync();
        totalCount.Should().Be(4);

        var expiredCount = await Context.IdempotencyKeys
            .CountAsync(i => i.ExpiresAt <= DateTime.UtcNow);
        expiredCount.Should().Be(2);

        await EventStoreRepository.CleanupExpiredIdempotencyKeysAsync();

        var remainingCount = await Context.IdempotencyKeys.CountAsync();
        remainingCount.Should().Be(2);

        var remainingKeys = await Context.IdempotencyKeys
            .Select(i => i.IdempotencyKey)
            .ToListAsync();

        remainingKeys.Should().Contain("valid-key-1");
        remainingKeys.Should().Contain("valid-key-2");
        remainingKeys.Should().NotContain("expired-key-1");
        remainingKeys.Should().NotContain("expired-key-2");
    }

    [Fact]
    public async Task PerformCleanupAsync_WithNoExpiredKeys_ShouldNotAffectDatabase()
    {
        await EventStoreRepository.SaveIdempotencyAsync(
            "valid-key-1",
            "aggregate-1",
            "hash-1",
            "processed",
            TimeSpan.FromHours(1));

        await EventStoreRepository.SaveIdempotencyAsync(
            "valid-key-2",
            "aggregate-2",
            "hash-2",
            "processed",
            TimeSpan.FromHours(24));

        var initialCount = await Context.IdempotencyKeys.CountAsync();
        initialCount.Should().Be(2);

        await EventStoreRepository.CleanupExpiredIdempotencyKeysAsync();

        var finalCount = await Context.IdempotencyKeys.CountAsync();
        finalCount.Should().Be(2);
    }

    [Fact]
    public async Task PerformCleanupAsync_WithEmptyTable_ShouldNotThrow()
    {

        var initialCount = await Context.IdempotencyKeys.CountAsync();
        initialCount.Should().Be(0);

        var act = async () => await EventStoreRepository.CleanupExpiredIdempotencyKeysAsync();
        
        await act.Should().NotThrowAsync();

        var finalCount = await Context.IdempotencyKeys.CountAsync();
        finalCount.Should().Be(0);
    }
}