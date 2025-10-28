using Npgsql;
using NpgsqlDapperExampleGen;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace EndToEndTests;

public partial class NpgsqlDapperTester
{
    private QuerySql QuerySql { get; }

    public NpgsqlDapperTester()
    {
        var connString = Environment.GetEnvironmentVariable(EndToEndCommon.PostgresConnectionStringEnv);
        NpgsqlDataSourceBuilder dataSourceBuilder = new NpgsqlDataSourceBuilder(connString);
        QuerySql.ConfigureEnumMappings(dataSourceBuilder);
        var dataSource = dataSourceBuilder.Build();
        this.QuerySql = new QuerySql(dataSource);
    }

    [TearDown]
    public async Task EmptyTestsTable()
    {
        await QuerySql.TruncateAuthors();
        await QuerySql.TruncatePostgresNumericTypes();
        await QuerySql.TruncatePostgresStringTypes();
        await QuerySql.TruncatePostgresDateTimeTypes();
        await QuerySql.TruncatePostgresGeoTypes();
        await QuerySql.TruncatePostgresNetworkTypes();
        await QuerySql.TruncatePostgresArrayTypes();
        await QuerySql.TruncatePostgresSpecialTypes();
    }
}