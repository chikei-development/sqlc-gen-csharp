using Npgsql;
using NpgsqlExampleGen;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace EndToEndTests;

public partial class NpgsqlTester
{
    private QuerySql QuerySql { get; }

    public NpgsqlTester()
    {
        var connString = Environment.GetEnvironmentVariable(EndToEndCommon.PostgresConnectionStringEnv);
        NpgsqlDataSourceBuilder dataSourceBuilder = new NpgsqlDataSourceBuilder(connString);
        QuerySql.ConfigureEnumMappings(dataSourceBuilder);
        var dataSource = dataSourceBuilder.Build();
        this.QuerySql = new QuerySql(dataSource);
    }

    [TearDown]
    public async Task EmptyTestsTables()
    {
        await QuerySql.TruncateAuthors();
        await QuerySql.TruncatePostgresNumericTypes();
        await QuerySql.TruncatePostgresStringTypes();
        await QuerySql.TruncatePostgresDateTimeTypes();
        await QuerySql.TruncatePostgresGeoTypes();
        await QuerySql.TruncatePostgresNetworkTypes();
        await QuerySql.TruncatePostgresArrayTypes();
        await QuerySql.TruncatePostgresSpecialTypes();
        await QuerySql.TruncatePostgresNotNullTypes();
    }

    [Test]
    public async Task TestParameterDeduplication()
    {
        // Create test data with explicit dates
        var testDate = new DateTime(2023, 1, 1, 10, 0, 0, DateTimeKind.Unspecified); // this needs to be unspecified for Postgres timestamp without time zone

        // Insert test authors with specified creation and update dates
        // Note: This test will work once the schema and code generation include the new query
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

        // This test demonstrates parameter deduplication where:
        // - 'author_name' parameter is used twice in the query (name match and bio LIKE)
        // - 'min_id' parameter is used twice (greater than and less than conditions)  
        // - 'date_filter' parameter is used twice (created_at and updated_at filters)

        var results = await QuerySql.GetAuthorsWithDuplicateParams(new QuerySql.GetAuthorsWithDuplicateParamsArgs
        {
            AuthorName = "Test Author",
            MinId = 3000,
            DateFilter = testDate
        });

        // Should find the "Test Author" record since:
        // - name matches "Test Author" 
        // - bio contains "Test Author"
        // - id (3333) > min_id (3000) 
        // - dates will be after testDate (assuming default NOW() timestamps)
        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.GreaterThan(0));
        Assert.That(results.Any(r => r.Name == "Test Author"), Is.True);
    }
}