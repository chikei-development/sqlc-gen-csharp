using Npgsql;
using NpgsqlLegacyExampleGen;
using NUnit.Framework;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace EndToEndTests
{
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
}