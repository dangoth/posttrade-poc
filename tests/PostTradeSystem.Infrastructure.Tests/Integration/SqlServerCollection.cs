using Xunit;

namespace PostTradeSystem.Infrastructure.Tests.Integration;

[CollectionDefinition("SqlServer")]
public class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
}