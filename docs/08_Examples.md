# Examples
<details>
<summary>Npgsql</summary>

## Engine `postgresql`: [NpgsqlExample](examples/NpgsqlExample)
### [Schema](examples/config/postgresql/authors/schema.sql) | [Queries](examples/config/postgresql/authors/query.sql) | [End2End Test](end2end/EndToEndTests/NpgsqlTester.cs)
### Config
```yaml
useDapper: false
targetFramework: net8.0
generateCsproj: true
namespaceName: NpgsqlExampleGen
overrides:
- column: "GetPostgresFunctions:max_integer"
  csharp_type:
    type: "int"
    notNull: false
- column: "GetPostgresFunctions:max_varchar"
  csharp_type:
    type: "string"
    notNull: false
- column: "GetPostgresFunctions:max_timestamp"
  csharp_type:
    type: "DateTime"
    notNull: true
- column: "GetPostgresSpecialTypesCnt:c_json"
  csharp_type:
    type: "JsonElement"
    notNull: false
- column: "GetPostgresSpecialTypesCnt:c_jsonb"
  csharp_type:
    type: "JsonElement"
    notNull: false
- column: "*:c_json_string_override"
  csharp_type:
    type: "string"
    notNull: false
- column: "*:c_xml_string_override"
  csharp_type:
    type: "string"
    notNull: false
- column: "*:c_macaddr8"
  csharp_type:
    type: "string"
    notNull: false
- column: "*:c_timestamp_noda_instant_override"
  csharp_type:
    type: "Instant"
    notNull: false
```

</details>
<details>
<summary>NpgsqlDapper</summary>

## Engine `postgresql`: [NpgsqlDapperExample](examples/NpgsqlDapperExample)
### [Schema](examples/config/postgresql/authors/schema.sql) | [Queries](examples/config/postgresql/authors/query.sql) | [End2End Test](end2end/EndToEndTests/NpgsqlDapperTester.cs)
### Config
```yaml
useDapper: true
targetFramework: net8.0
generateCsproj: true
namespaceName: NpgsqlDapperExampleGen
overrides:
- column: "GetPostgresFunctions:max_integer"
  csharp_type:
    type: "int"
    notNull: false
- column: "GetPostgresFunctions:max_varchar"
  csharp_type:
    type: "string"
    notNull: false
- column: "GetPostgresFunctions:max_timestamp"
  csharp_type:
    type: "DateTime"
    notNull: true
- column: "GetPostgresSpecialTypesCnt:c_json"
  csharp_type:
    type: "JsonElement"
    notNull: false
- column: "GetPostgresSpecialTypesCnt:c_jsonb"
  csharp_type:
    type: "JsonElement"
    notNull: false
- column: "*:c_json_string_override"
  csharp_type:
    type: "string"
    notNull: false
- column: "*:c_xml_string_override"
  csharp_type:
    type: "string"
    notNull: false
- column: "*:c_macaddr8"
  csharp_type:
    type: "string"
    notNull: false
- column: "*:c_timestamp_noda_instant_override"
  csharp_type:
    type: "Instant"
    notNull: false
```

</details>
<details>
<summary>MySqlConnector</summary>

## Engine `mysql`: [MySqlConnectorExample](examples/MySqlConnectorExample)
### [Schema](examples/config/mysql/authors/schema.sql) | [Queries](examples/config/mysql/authors/query.sql) | [End2End Test](end2end/EndToEndTests/MySqlConnectorTester.cs)
### Config
```yaml
useDapper: false
targetFramework: net8.0
generateCsproj: true
namespaceName: MySqlConnectorExampleGen
overrides:
- column: "GetMysqlFunctions:max_int"
  csharp_type:
    type: "int"
    notNull: false
- column: "GetMysqlFunctions:max_varchar"
  csharp_type:
    type: "string"
    notNull: false
- column: "GetMysqlFunctions:max_timestamp"
  csharp_type:
    type: "DateTime"
    notNull: true
- column: "*:c_json_string_override"
  csharp_type:
    type: "string"
    notNull: false
- column: "*:c_timestamp_noda_instant_override"
  csharp_type:
    type: "Instant"
    notNull: false
```

</details>
<details>
<summary>MySqlConnectorDapper</summary>

## Engine `mysql`: [MySqlConnectorDapperExample](examples/MySqlConnectorDapperExample)
### [Schema](examples/config/mysql/authors/schema.sql) | [Queries](examples/config/mysql/authors/query.sql) | [End2End Test](end2end/EndToEndTests/MySqlConnectorDapperTester.cs)
### Config
```yaml
useDapper: true
targetFramework: net8.0
generateCsproj: true
namespaceName: MySqlConnectorDapperExampleGen
overrides:
- column: "GetMysqlFunctions:max_int"
  csharp_type:
    type: "int"
    notNull: false
- column: "GetMysqlFunctions:max_varchar"
  csharp_type:
    type: "string"
    notNull: false
- column: "GetMysqlFunctions:max_timestamp"
  csharp_type:
    type: "DateTime"
    notNull: true
- column: "*:c_json_string_override"
  csharp_type:
    type: "string"
    notNull: false
- column: "*:c_timestamp_noda_instant_override"
  csharp_type:
    type: "Instant"
    notNull: false
```

</details>
<details>
<summary>Sqlite</summary>

## Engine `sqlite`: [SqliteExample](examples/SqliteExample)
### [Schema](examples/config/sqlite/authors/schema.sql) | [Queries](examples/config/sqlite/authors/query.sql) | [End2End Test](end2end/EndToEndTests/SqliteTester.cs)
### Config
```yaml
useDapper: false
targetFramework: net8.0
generateCsproj: true
namespaceName: SqliteExampleGen
overrides:
- column: "GetSqliteFunctions:max_integer"
  csharp_type:
    type: "int"
- column: "GetSqliteFunctions:max_varchar"
  csharp_type:
    type: "string"
- column: "GetSqliteFunctions:max_real"
  csharp_type:
    type: "decimal"
- column: "*:c_text_datetime_override"
  csharp_type:
    type: "DateTime"
- column: "*:c_integer_datetime_override"
  csharp_type:
    type: "DateTime"
- column: "*:c_text_bool_override"
  csharp_type:
    type: "bool"
- column: "*:c_integer_bool_override"
  csharp_type:
    type: "bool"
- column: "*:c_text_noda_instant_override"
  csharp_type:
    type: "Instant"
- column: "*:c_integer_noda_instant_override"
  csharp_type:
    type: "Instant"
```

</details>
<details>
<summary>SqliteDapper</summary>

## Engine `sqlite`: [SqliteDapperExample](examples/SqliteDapperExample)
### [Schema](examples/config/sqlite/authors/schema.sql) | [Queries](examples/config/sqlite/authors/query.sql) | [End2End Test](end2end/EndToEndTests/SqliteDapperTester.cs)
### Config
```yaml
useDapper: true
targetFramework: net8.0
generateCsproj: true
namespaceName: SqliteDapperExampleGen
overrides:
- column: "GetSqliteFunctions:max_integer"
  csharp_type:
    type: "int"
- column: "GetSqliteFunctions:max_varchar"
  csharp_type:
    type: "string"
- column: "GetSqliteFunctions:max_real"
  csharp_type:
    type: "decimal"
- column: "*:c_text_datetime_override"
  csharp_type:
    type: "DateTime"
- column: "*:c_integer_datetime_override"
  csharp_type:
    type: "DateTime"
- column: "*:c_text_bool_override"
  csharp_type:
    type: "bool"
- column: "*:c_integer_bool_override"
  csharp_type:
    type: "bool"
- column: "*:c_text_noda_instant_override"
  csharp_type:
    type: "Instant"
- column: "*:c_integer_noda_instant_override"
  csharp_type:
    type: "Instant"
```

</details>