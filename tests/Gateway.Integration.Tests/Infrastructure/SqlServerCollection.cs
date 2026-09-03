namespace Gateway.Integration.Tests.Infrastructure;

/// <summary>
/// One container for every test in the collection. Starting SQL Server per test class would add
/// minutes to the suite for no extra coverage.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    /// <summary>
    /// The collection name tests reference.
    /// </summary>
    public const string Name = "sql-server";
}
