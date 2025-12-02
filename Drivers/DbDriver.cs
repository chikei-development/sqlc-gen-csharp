using Microsoft.CodeAnalysis.CSharp.Syntax;
using Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SqlcGenCsharp.Drivers;

public record ConnectionGenCommands(string EstablishConnection, string ConnectionOpen);

public abstract class DbDriver
{
    protected const string DefaultDapperVersion = "2.1.66";
    protected const string DefaultSystemTextJsonVersion = "9.0.6";
    protected const string DefaultNodaTimeVersion = "3.2.0";

    public Options Options { get; }

    public string DefaultSchema { get; }

    public abstract string TransactionClassName { get; }

    public Dictionary<string, Dictionary<string, Table>> Tables { get; }

    protected IList<Query> Queries { get; }

    private HashSet<string> NullableTypesInDotnetCore { get; } =
    ["string", "object", "PhysicalAddress", "IPAddress"];

    protected HashSet<string> NullableTypes { get; } =
    [
        "bool",
        "byte",
        "short",
        "int",
        "long",
        "float",
        "double",
        "decimal",
        "DateTime",
        "TimeSpan",
        "Guid",
        "NpgsqlPoint",
        "NpgsqlLine",
        "NpgsqlLSeg",
        "NpgsqlBox",
        "NpgsqlPath",
        "NpgsqlPolygon",
        "NpgsqlCircle",
        "JsonElement",
        "NpgsqlCidr",
        "Instant",
    ];

    protected abstract Dictionary<string, ColumnMapping> ColumnMappings { get; }

    protected const string TransformQueryForSliceArgsImpl = """
        public static string TransformQueryForSliceArgs(string originalSql, int sliceSize, string paramName)
        {
            var paramArgs = Enumerable.Range(0, sliceSize).Select(i => $"@{paramName}Arg{i}").ToList();
            return originalSql.Replace($"/*SLICE:{paramName}*/@{paramName}", string.Join(",", paramArgs));
        }
        """;

    public readonly string TransactionConnectionNullExcetionThrow = $"""
        if (this.{Variable.Transaction.AsPropertyName()}?.Connection == null || this.{Variable.Transaction.AsPropertyName()}?.Connection.State != System.Data.ConnectionState.Open)
            throw new InvalidOperationException("Transaction is provided, but its connection is null.");
        """;

    protected static readonly SqlMapperImplFunc DateTimeNodaInstantTypeHandler = _ =>
        $$"""
            private class NodaInstantTypeHandler : SqlMapper.TypeHandler<Instant>
            {
                public override Instant Parse(object value)
                {
                    if (value is DateTime dt)
                    {
                        if (dt.Kind != DateTimeKind.Utc)
                            dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                        return dt.ToInstant();
                    }
                    throw new DataException($"Cannot convert {value?.GetType()} to Instant");
                }

                public override void SetValue(IDbDataParameter parameter, Instant value)
                {
                    parameter.Value = value;
                }
            }
            """;

    protected DbDriver(Options options, Catalog catalog, IList<Query> queries)
    {
        Options = options;
        DefaultSchema = catalog.DefaultSchema;
        Tables = ConstructTablesLookup(catalog);
        Queries = queries;

        if (!Options.DotnetFramework.IsDotnetCore())
            return;

        foreach (var t in NullableTypesInDotnetCore)
            NullableTypes.Add(t);
    }

    private static readonly HashSet<string> _excludedSchemas = ["pg_catalog", "information_schema"];

    private static Dictionary<string, Dictionary<string, Table>> ConstructTablesLookup(
        Catalog catalog
    )
    {
        return catalog
            .Schemas.Where(s => !_excludedSchemas.Contains(s.Name))
            .ToDictionary(
                s => s.Name == catalog.DefaultSchema ? string.Empty : s.Name,
                s => s.Tables.ToDictionary(t => t.Rel.Name, t => t)
            );
    }

    public virtual IDictionary<string, string> GetPackageReferences()
    {
        return new Dictionary<string, string>
        {
            {
                "Dapper",
                Options.OverrideDapperVersion != string.Empty
                    ? Options.OverrideDapperVersion
                    : DefaultDapperVersion
            },
        }
            .MergeIf(
                new Dictionary<string, string>
                {
                    { "System.Text.Json", DefaultSystemTextJsonVersion },
                },
                IsSystemTextJsonNeeded()
            )
            .MergeIf(
                new Dictionary<string, string> { { "NodaTime", DefaultNodaTimeVersion } },
                TypeExistsInQueries("Instant")
            );
    }

    public virtual ISet<string> GetUsingDirectivesForQueries()
    {
        return new HashSet<string>
        {
            "System",
            "System.Collections.Generic",
            "System.Threading.Tasks",
        }
            .AddRangeIf(["Dapper"], Options.UseDapper)
            .AddRangeExcludeNulls(GetUsingDirectivesForColumnMappings());
    }

    private ISet<string> GetUsingDirectivesForColumnMappings()
    {
        var usingDirectives = new HashSet<string>();
        foreach (var query in Queries)
        {
            // Check return columns
            foreach (var column in query.Columns)
            {
                AddUsingDirectivesForColumn(usingDirectives, column, query);
            }

            // Check parameters
            foreach (var param in query.Params)
            {
                AddUsingDirectivesForColumn(usingDirectives, param.Column, query);
            }
        }
        return usingDirectives;
    }

    private void AddUsingDirectivesForColumn(HashSet<string> usingDirectives, Column column, Query? query)
    {
        // Check if this is an embedded table
        if (column.EmbedTable != null)
        {
            AddUsingDirectivesForEmbeddedTable(usingDirectives, column.EmbedTable);
        }
        else
        {
            var csharpType = GetCsharpTypeWithoutNullableSuffix(column, query);
            if (ColumnMappings.ContainsKey(csharpType))
            {
                var columnMapping = ColumnMappings[csharpType];
                usingDirectives.AddRangeIf(
                    columnMapping.UsingDirectives!,
                    columnMapping.UsingDirectives is not null
                );
            }
        }
    }

    private void AddUsingDirectivesForEmbeddedTable(HashSet<string> usingDirectives, Plugin.Identifier embedTable)
    {
        // Find the table in the catalog
        var schemaName = string.IsNullOrEmpty(embedTable.Schema) ? string.Empty : embedTable.Schema;
        if (!Tables.TryGetValue(schemaName, out var schemaDict))
            return;

        if (!schemaDict.TryGetValue(embedTable.Name, out var table))
            return;

        // Check each column of the embedded table using the same logic as regular columns
        foreach (var tableColumn in table.Columns)
        {
            // For embedded table columns, we don't have a query context, so pass null
            AddUsingDirectivesForColumn(usingDirectives, tableColumn, null);
        }
    }

    public virtual ISet<string> GetUsingDirectivesForUtils()
    {
        return new HashSet<string> { "System.Linq" }
            .AddRangeIf(["System.Data", "Dapper"], Options.UseDapper)
            .AddRangeIf(GetUsingDirectivesForColumnMappings(), Options.UseDapper);
    }

    public virtual ISet<string> GetUsingDirectivesForModels()
    {
        return new HashSet<string> { "System.Linq" }.AddRangeExcludeNulls(
            GetUsingDirectivesForColumnMappings()
        );
    }

    public virtual ClassDeclarationSyntax GetClassDeclaration(
        string className,
        IEnumerable<MemberDeclarationSyntax> classMembers,
        Dictionary<string, Dictionary<string, Plugin.Enum>> enums
    )
    {
        var dapperStatements = Options.UseDapper
            ? $$"""
                  Utils.ConfigureSqlMapper();
                  Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
                """
            : string.Empty;
        var classDeclaration = (ClassDeclarationSyntax)
            ParseMemberDeclaration(
                $$"""
                public class {{className}}
                {
                    public {{className}}()
                    {
                        {{dapperStatements}}
                    }

                    public {{className}}(string {{Variable.ConnectionString.AsVarName()}}) : this()
                    {
                        {{GetConstructorStatements().JoinByNewLine()}}
                    }

                    private {{className}}({{TransactionClassName}} {{Variable.Transaction.AsVarName()}}) : this()
                    {
                        {{GetTransactionConstructorStatements().JoinByNewLine()}}
                    }

                    public static {{className}} WithTransaction({{TransactionClassName}} {{Variable.Transaction.AsVarName()}})
                    {
                        return new {{className}}({{Variable.Transaction.AsVarName()}});
                    }

                    private {{AddNullableSuffixIfNeeded(
                    TransactionClassName,
                    false
                )}} {{Variable.Transaction.AsPropertyName()}} { get; }
                    private {{AddNullableSuffixIfNeeded(
                    "string",
                    false
                )}} {{Variable.ConnectionString.AsPropertyName()}} { get; }
                }
                """
            )!;
        return classDeclaration.AddMembers(classMembers.ToArray());
    }

    public virtual string[] GetConstructorStatements()
    {
        return
        [
            $"this.{Variable.ConnectionString.AsPropertyName()} = {Variable.ConnectionString.AsVarName()};",
        ];
    }

    public string[] GetTransactionConstructorStatements()
    {
        return
        [
            $"this.{Variable.Transaction.AsPropertyName()} = {Variable.Transaction.AsVarName()};",
        ];
    }

    protected virtual ISet<string> GetConfigureSqlMappings()
    {
        return ColumnMappings
            .Where(m => TypeExistsInQueries(m.Key) && m.Value.SqlMapper is not null)
            .Select(m => m.Value.SqlMapper!)
            .ToHashSet();
    }

    public virtual MemberDeclarationSyntax[] GetMemberDeclarationsForUtils()
    {
        if (!Options.UseDapper)
            return [];
        return
        [
            .. GetSqlMapperMemberDeclarations(),
            ParseMemberDeclaration(
                $$"""
                  public static void ConfigureSqlMapper()
                  {
                      {{GetConfigureSqlMappings().JoinByNewLine()}}
                  }
                """
            )!,
        ];
    }

    private MemberDeclarationSyntax[] GetSqlMapperMemberDeclarations()
    {
        return
        [
            .. ColumnMappings
                .Where(m => TypeExistsInQueries(m.Key) && m.Value.SqlMapperImpl is not null)
                .Select(m =>
                    ParseMemberDeclaration(
                        m.Value.SqlMapperImpl!(Options.DotnetFramework.IsDotnetCore())
                    )!
                ),
        ];
    }

    public abstract string TransformQueryText(Query query);

    public abstract ConnectionGenCommands EstablishConnection(Query query);

    public abstract string CreateSqlCommand(string sqlTextConstant);

    /// <summary>
    /// Gets the connection establishment code for scenarios without transaction.
    /// Override this for DataSource-based drivers like Npgsql.
    /// </summary>
    public virtual string GetNoTransactionConnectionCode(Query query)
    {
        var (establishConnection, connectionOpen) = EstablishConnection(query);
        return $$"""
            using ({{establishConnection}})
            {
                {{connectionOpen.AppendSemicolonUnlessEmpty()}}
            """;
    }

    /// <summary>
    /// Gets the connection establishment code for scenarios with transaction.
    /// Override this for DataSource-based drivers that need different transaction handling.
    /// </summary>
    public virtual string GetWithTransactionConnectionCode(Query query)
    {
        return string.Empty; // Default: no special connection code for transactions
    }

    /// <summary>
    /// Gets the command creation code for scenarios without transaction.
    /// Override this for DataSource-based drivers like Npgsql.
    /// </summary>
    public virtual string GetNoTransactionCommandCode(string sqlVar, Query query)
    {
        var createSqlCommand = CreateSqlCommand(sqlVar);
        return $$"""
            using ({{createSqlCommand}})
            {
            """;
    }

    /// <summary>
    /// Gets the command creation code for scenarios with transaction.
    /// Default implementation works for most drivers.
    /// </summary>
    public virtual string GetWithTransactionCommandCode(string sqlVar, Query query)
    {
        var transactionProperty = Variable.Transaction.AsPropertyName();
        var commandVar = Variable.Command.AsVarName();
        return $$"""
            using (var {{commandVar}} = this.{{transactionProperty}}.Connection.CreateCommand())
            {
                {{commandVar}}.CommandText = {{sqlVar}};
                {{commandVar}}.Transaction = this.{{transactionProperty}};
            """;
    }

    /// <summary>
    /// Generates the complete method body for non-transaction scenarios.
    /// This is a helper method that combines connection and command patterns.
    /// </summary>
    public virtual string GenerateNoTransactionMethodBody(
        string sqlVar,
        Query query,
        string innerBody
    )
    {
        var connectionCode = GetNoTransactionConnectionCode(query);
        var commandCode = GetNoTransactionCommandCode(sqlVar, query);
        var commandParameters = AddParametersToCommand(query);

        // For DataSource-based drivers (like Npgsql), connectionCode will be empty
        var connectionWrapper = string.IsNullOrEmpty(connectionCode)
            ? string.Empty
            : connectionCode;
        var connectionClose = string.IsNullOrEmpty(connectionCode) ? string.Empty : "    }";

        return $$"""
               {{connectionWrapper}}
               {{commandCode}}
                  {{commandParameters}}
                   {{innerBody}}
               }
               {{connectionClose}}
            """;
    }

    /// <summary>
    /// Generates the complete method body for transaction scenarios.
    /// </summary>
    public virtual string GenerateWithTransactionMethodBody(
        string sqlVar,
        Query query,
        string innerBody
    )
    {
        var commandCode = GetWithTransactionCommandCode(sqlVar, query);
        var commandParameters = AddParametersToCommand(query);

        return $$"""
               {{TransactionConnectionNullExcetionThrow}}
               {{commandCode}}
                   {{commandParameters}}
                   {{innerBody}}
               }
            """;
    }

    /* Since there is no indication of the primary key column in SQLC protobuf (assuming it is a single column),
       this method uses a few heuristics to assess the data type of the id column
    */
    public string GetIdColumnType(Query query)
    {
        var tableColumns = Tables[query.InsertIntoTable.Schema][query.InsertIntoTable.Name].Columns;
        var idColumn = tableColumns.First(c =>
            c.Name.Equals("id", StringComparison.OrdinalIgnoreCase)
        );
        if (idColumn is not null)
            return GetCsharpType(idColumn, query);

        idColumn = tableColumns.First(c =>
            c.Name.Contains("id", StringComparison.CurrentCultureIgnoreCase)
        );
        return GetCsharpType(idColumn ?? tableColumns[0], query);
    }

    public virtual string[] GetLastIdStatement(Query query)
    {
        var idColumnType = GetIdColumnType(query);
        var convertFunc =
            ColumnMappings[idColumnType].ConvertFunc
            ?? throw new InvalidOperationException(
                $"ConvertFunc is missing for id column type {idColumnType}"
            );
        var convertFuncCall = convertFunc(Variable.Result.AsVarName());
        return
        [
            $"var {Variable.Result.AsVarName()} = await {Variable.Command.AsVarName()}.ExecuteScalarAsync();",
            $"return {convertFuncCall};",
        ];
    }

    public virtual string AddParametersToCommand(Query query)
    {
        // Deduplicate parameters by Column.Name to avoid adding the same parameter multiple times
        // This handles cases where the same named parameter is used multiple times in SQL
        var uniqueParams = query
            .Params.GroupBy(p => p.Column.Name)
            .Select(g => g.First()) // Take the first parameter for each unique name
            .ToList();

        return uniqueParams
            .Select(p =>
            {
                var commandVar = Variable.Command.AsVarName();
                var param = $"{Variable.Args.AsVarName()}.{p.Column.Name.ToPascalCase()}";
                var columnMapping = GetCsharpTypeWithoutNullableSuffix(p.Column, query);

                if (p.Column.IsSqlcSlice)
                    return $$"""
                    for (int i = 0; i < {{param}}.Length; i++)
                        {{commandVar}}.Parameters.AddWithValue($"@{{p.Column.Name}}Arg{i}", {{param}}[i]);
                    """;

                var writerFn = GetWriterFn(p.Column, query);
                var paramToWrite = writerFn is null
                    ? param
                    : writerFn(
                        param,
                        p.Column.Type.Name,
                        IsColumnNotNull(p.Column, query),
                        Options.UseDapper,
                        Options.DotnetFramework.IsDotnetLegacy()
                    );
                var addParamToCommand =
                    $"""{commandVar}.Parameters.AddWithValue("@{p.Column.Name}", {paramToWrite});""";
                return addParamToCommand;
            })
            .JoinByNewLine();
    }

    public Column GetColumnFromParam(Parameter queryParam, Query query)
    {
        if (string.IsNullOrEmpty(queryParam.Column.Name))
            queryParam.Column.Name =
                $"{GetCsharpType(queryParam.Column, query).Replace("[]", "Arr")}_{queryParam.Number}";
        return queryParam.Column;
    }

    protected bool TypeExistsInQueries(string csharpType)
    {
        return Queries.Any(q => TypeExistsInQuery(csharpType, q));
    }

    protected bool TypeExistsInQuery(string csharpType, Query query)
    {
        return query.Columns.Any(column =>
                csharpType == GetCsharpTypeWithoutNullableSuffix(column, query)
            )
            || query.Params.Any(p =>
                csharpType == GetCsharpTypeWithoutNullableSuffix(p.Column, query)
            );
    }

    protected bool SliceQueryExists()
    {
        return Queries.Any(q => q.Params.Any(p => p.Column.IsSqlcSlice));
    }

    protected bool CopyFromQueryExists()
    {
        return Queries.Any(q => q.Cmd is ":copyfrom");
    }

    private OverrideOption? FindOverrideForQueryColumn(Query? query, Column column)
    {
        if (query is null)
            return null;
        foreach (var overrideOption in Options.Overrides)
            if (
                overrideOption.Column == $"{query.Name}:{column.Name}"
                || overrideOption.Column == $"*:{column.Name}"
            )
                return overrideOption;
        return null;
    }

    // If the column data type is overridden, we need to check for nulls in generated code
    public bool IsColumnNotNull(Column column, Query? query)
    {
        if (FindOverrideForQueryColumn(query, column) is { CsharpType: var csharpType })
            return csharpType.NotNull;
        return column.NotNull;
    }

    /* Data type methods */
    public string GetCsharpType(Column column, Query? query)
    {
        var csharpType = GetCsharpTypeWithoutNullableSuffix(column, query);
        return AddNullableSuffixIfNeeded(csharpType, IsColumnNotNull(column, query));
    }

    public string AddNullableSuffixIfNeeded(string csharpType, bool notNull)
    {
        if (notNull)
            return csharpType;
        return IsTypeNullable(csharpType) ? $"{csharpType}?" : csharpType;
    }

    public bool IsTypeNullable(string csharpType)
    {
        if (NullableTypes.Contains(csharpType.Replace("?", "")))
            return true;
        return Options.DotnetFramework.IsDotnetCore(); // non-primitives in .Net Core are inherently nullable
    }

    protected virtual string GetCsharpTypeWithoutNullableSuffix(Column column, Query? query)
    {
        if (column.EmbedTable != null)
            return column.EmbedTable.Name.ToModelName(column.EmbedTable.Schema, DefaultSchema);

        if (string.IsNullOrEmpty(column.Type.Name))
            return "object";

        if (FindOverrideForQueryColumn(query, column) is { CsharpType: var csharpType })
            return csharpType.Type;

        foreach (
            var columnMapping in ColumnMappings.Where(columnMapping =>
                DoesColumnMappingApply(columnMapping.Value, column)
            )
        )
        {
            if (column.IsArray || column.IsSqlcSlice)
                return $"{columnMapping.Key}[]";
            return columnMapping.Key;
        }
        throw new NotSupportedException(
            $"Column {column.Name} has unsupported column type: {column.Type.Name} in {GetType().Name}"
        );
    }

    private static bool DoesColumnMappingApply(ColumnMapping columnMapping, Column column)
    {
        var columnType = column.Type.Name.ToLower();
        if (!columnMapping.DbTypes.TryGetValue(columnType, out var typeInfo))
            return false;
        if (typeInfo.Length is null)
            return true;
        return typeInfo.Length.Value == column.Length;
    }

    public virtual WriterFn? GetWriterFn(Column column, Query query)
    {
        var csharpType = GetCsharpTypeWithoutNullableSuffix(column, query);
        var writerFn = ColumnMappings.GetValueOrDefault(csharpType)?.WriterFn;
        if (writerFn is not null)
            return writerFn;

        static string DefaultWriterFn(
            string el,
            string dbType,
            bool notNull,
            bool isDapper,
            bool isLegacy
        ) => notNull ? el : $"{el} ?? (object)DBNull.Value";
        return Options.UseDapper ? null : DefaultWriterFn;
    }

    /* Column reader methods */
    private string GetColumnReader(CsharpTypeOption csharpTypeOption, Column column, int ordinal)
    {
        if (ColumnMappings.TryGetValue(csharpTypeOption.Type, out var value))
            return value.ReaderFn(ordinal, column.Type.Name);
        throw new NotSupportedException(
            $"Could not find column mapping for type override: {csharpTypeOption.Type}"
        );
    }

    public virtual string GetColumnReader(Column column, int ordinal, Query? query)
    {
        if (FindOverrideForQueryColumn(query, column) is { CsharpType: var csharpType })
            return GetColumnReader(csharpType, column, ordinal);

        foreach (
            var columnMapping in ColumnMappings.Values.Where(columnMapping =>
                DoesColumnMappingApply(columnMapping, column)
            )
        )
        {
            if (column.IsArray)
                return columnMapping.ReaderArrayFn?.Invoke(ordinal, column.Type.Name)
                    ?? throw new InvalidOperationException("ReaderArrayFn is null");
            return columnMapping.ReaderFn(ordinal, column.Type.Name);
        }
        throw new NotSupportedException(
            $"column {column.Name} has unsupported column type: {column.Type.Name} in {GetType().Name}"
        );
    }

    private bool IsSystemTextJsonNeeded()
    {
        if (Options.DotnetFramework.IsDotnetCore())
            return false;
        return TypeExistsInQueries("JsonElement");
    }
}