using NUnit.Framework;
using SqliteExampleGen;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace EndToEndTests;

public partial class SqliteTester
{
    private QuerySql QuerySql { get; } = new(
        Environment.GetEnvironmentVariable(EndToEndCommon.SqliteConnectionStringEnv)!);

    [TearDown]
    public async Task EmptyTestsTable()
    {
        await QuerySql.DeleteAllAuthors();
        await QuerySql.DeleteAllSqliteTypes();
    }

    [Test]
    public async Task TestParameterDeduplication()
    {
        // Insert test authors
        await QuerySql.CreateAuthor(new QuerySql.CreateAuthorArgs
        {
            Id = 3333,
            Name = "Test Author",
            Bio = "A test biography with Test Author mentioned"
        });

        await QuerySql.CreateAuthor(new QuerySql.CreateAuthorArgs
        {
            Id = 4444,
            Name = "Another Author",
            Bio = "Different biography content"
        });

        // Test parameter deduplication where:
        // - 'author_name' parameter is used twice in the query (name match and bio LIKE)
        // - 'min_id' parameter is used twice (greater than and less than conditions)
        var results = await QuerySql.GetAuthorsWithDuplicateParams(new QuerySql.GetAuthorsWithDuplicateParamsArgs
        {
            AuthorName = "Test Author",
            MinId = 3000
        });

        // Should find the "Test Author" record since:
        // - name matches "Test Author" 
        // - bio contains "Test Author"
        // - id (3333) > min_id (3000) 
        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.GreaterThan(0));
        Assert.That(results.Any(r => r.Name == "Test Author"), Is.True);
    }
}