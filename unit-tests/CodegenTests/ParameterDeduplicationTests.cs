using Google.Protobuf;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;
using Plugin;
using SqlcGenCsharp;
using SqlcGenCsharp.Drivers;
using System.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace CodegenTests;

public class ParameterDeduplicationTests
{
    private readonly Settings _postgresSettings = new()
    {
        Engine = "postgresql",
        Codegen = new Codegen { Out = "DummyProject" }
    };

    private readonly Settings _mysqlSettings = new()
    {
        Engine = "mysql",
        Codegen = new Codegen { Out = "DummyProject" }
    };

    private readonly Settings _sqliteSettings = new()
    {
        Engine = "sqlite",
        Codegen = new Codegen { Out = "DummyProject" }
    };

    private readonly Catalog _emptyCatalog = new()
    {
        Schemas =
        {
            new Schema
            {
                Name = "public",
                Tables = { Capacity = 0 },
                Enums = { Capacity = 0 },
            }
        }
    };

    private readonly Catalog _emptyCatalogMysqlSqlite = new()
    {
        Schemas =
        {
            new Schema
            {
                Name = string.Empty,
                Tables = { Capacity = 0 },
                Enums = { Capacity = 0 },
            }
        }
    };

    private CodeGenerator CodeGenerator { get; } = new();

    private (string paramSyntax, string sqlText, Settings settings, Catalog catalog) GetEngineSpecificData(string engine)
    {
        return engine switch
        {
            "postgresql" => ("$", "", _postgresSettings, _emptyCatalog),
            "mysql" => ("?", "", _mysqlSettings, _emptyCatalogMysqlSqlite),
            "sqlite" => ("?", "", _sqliteSettings, _emptyCatalogMysqlSqlite),
            _ => throw new ArgumentException($"Unsupported engine: {engine}")
        };
    }

    [Test]
    [TestCase("postgresql", "text", "integer", "boolean")]
    [TestCase("mysql", "text", "int", "tinyint")]
    [TestCase("sqlite", "text", "integer", "integer")]
    public void TestParameterDeduplicationWithSameNamedParams(string engine, string textType, string intType, string boolType)
    {
        // Arrange - Create a query that simulates duplicate parameter names
        var duplicateColumn = new Column
        {
            Name = "param_a",
            Type = new Identifier { Name = textType },
            NotNull = false
        };

        var uniqueColumn1 = new Column
        {
            Name = "param_b",
            Type = new Identifier { Name = intType },
            NotNull = false
        };

        var uniqueColumn2 = new Column
        {
            Name = "param_c",
            Type = new Identifier { Name = boolType },
            NotNull = true
        };

        // Create multiple Parameter objects with the same Column.Name but different Number values
        // This simulates what sqlc sends when the same named parameter is used multiple times
        var parameters = new[]
        {
            new Parameter { Number = 1, Column = uniqueColumn1.Clone() },
            new Parameter { Number = 2, Column = duplicateColumn.Clone() },
            new Parameter { Number = 3, Column = duplicateColumn.Clone() }, // Duplicate name
            new Parameter { Number = 4, Column = duplicateColumn.Clone() }, // Duplicate name
            new Parameter { Number = 5, Column = uniqueColumn2.Clone() }
        };

        var resultColumns = new[]
        {
            new Column { Name = "id", Type = new Identifier { Name = intType } },
            new Column { Name = "name", Type = new Identifier { Name = textType } }
        };

        // Use engine-appropriate parameter syntax and catalog
        var (paramSyntax, sqlText, settings, catalog) = GetEngineSpecificData(engine);
        
        var query = new Query
        {
            Filename = "query.sql",
            Cmd = ":many",
            Name = "TestDuplicateParams",
            Text = $"SELECT id, name FROM table WHERE param_b = {paramSyntax}1 AND (param_a IS NULL OR condition1 = {paramSyntax}2 OR condition2 = {paramSyntax}3) AND param_c = {paramSyntax}4",
            Columns = { resultColumns },
            Params = { parameters }
        };

        var request = new GenerateRequest
        {
            Settings = settings,
            Catalog = catalog,
            Queries = { query },
            PluginOptions = ByteString.CopyFrom("{}", Encoding.UTF8)
        };

        // Act
        var response = CodeGenerator.Generate(request);

        // Assert
        Assert.That(response.Result.Files, Is.Not.Empty);

        var queryFile = response.Result.Files.First(f => f.Name == "QuerySql.cs");
        Assert.That(queryFile, Is.Not.Null);

        var generatedCode = queryFile.Contents.ToStringUtf8();
        var compilationUnit = ParseCompilationUnit(generatedCode);

        // Find the Args record/class
        var argsTypeDeclaration = compilationUnit.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault(t => t.Identifier.ValueText == "TestDuplicateParamsArgs");

        Assert.That(argsTypeDeclaration, Is.Not.Null, "Args type should be generated");

        // Get all property/parameter names from the Args type
        var propertyNames = new List<string>();

        if (argsTypeDeclaration is RecordDeclarationSyntax recordDecl)
        {
            // For record struct with primary constructor parameters
            var recordParameters = recordDecl.ParameterList?.Parameters ?? new SeparatedSyntaxList<ParameterSyntax>();
            propertyNames.AddRange(recordParameters.Select(p => p.Identifier.ValueText));
        }
        else if (argsTypeDeclaration is ClassDeclarationSyntax classDecl)
        {
            // For class with properties
            var properties = classDecl.DescendantNodes().OfType<PropertyDeclarationSyntax>();
            propertyNames.AddRange(properties.Select(p => p.Identifier.ValueText));
        }

        // Verify that each parameter name appears only once
        var expectedPropertyNames = new[]
        {
            "ParamB",
            "ParamA",  // Should appear only once, not three times
            "ParamC"
        };

        CollectionAssert.AreEquivalent(expectedPropertyNames, propertyNames,
            $"Args should contain each parameter only once for {engine} engine, even if used multiple times in SQL");

        // Verify that ParamA appears exactly once (not three times)
        var duplicateParamCount = propertyNames.Count(name => name == "ParamA");
        Assert.That(duplicateParamCount, Is.EqualTo(1),
            $"ParamA parameter should appear exactly once in Args for {engine} engine, not duplicated");

        // Additional verification: ensure the generated code compiles without duplicate parameter errors
        Assert.That(generatedCode.Contains("ParamA"), Is.True,
            $"Generated code should contain ParamA parameter for {engine} engine");

        // Verify that we don't have multiple consecutive ParamA parameters
        var paramAOccurrences = System.Text.RegularExpressions.Regex.Matches(
            generatedCode, @"\bParamA\b").Count;

        // Should appear in parameter declaration and usage, but not duplicated in parameter list
        Assert.That(paramAOccurrences, Is.LessThan(10),
            $"ParamA should not appear excessively due to duplication in parameter list for {engine} engine");
    }

    [Test]
    [TestCase("postgresql")]
    [TestCase("mysql")]
    [TestCase("sqlite")]
    public void TestParameterDeduplicationPreservesOrder(string engine)
    {
        // Arrange - Test that deduplication preserves the order of first occurrence
        var duplicateColumn = new Column
        {
            Name = "duplicate_param",
            Type = new Identifier { Name = "text" },
            NotNull = false
        };

        var parameters = new[]
        {
            new Parameter { Number = 1, Column = new Column { Name = "first_param", Type = new Identifier { Name = "text" } } },
            new Parameter { Number = 2, Column = duplicateColumn.Clone() },
            new Parameter { Number = 3, Column = new Column { Name = "middle_param", Type = new Identifier { Name = "text" } } },
            new Parameter { Number = 4, Column = duplicateColumn.Clone() }, // Duplicate
            new Parameter { Number = 5, Column = new Column { Name = "last_param", Type = new Identifier { Name = "text" } } }
        };

        var (paramSyntax, _, settings, catalog) = GetEngineSpecificData(engine);

        var query = new Query
        {
            Filename = "query.sql",
            Cmd = ":many",
            Name = "TestOrderQuery",
            Text = "SELECT 1",
            Columns = { new Column { Name = "result", Type = new Identifier { Name = "integer" } } },
            Params = { parameters }
        };

        var request = new GenerateRequest
        {
            Settings = settings,
            Catalog = catalog,
            Queries = { query },
            PluginOptions = ByteString.CopyFrom("{}", Encoding.UTF8)
        };

        // Act
        var response = CodeGenerator.Generate(request);

        // Assert
        var queryFile = response.Result.Files.First(f => f.Name == "QuerySql.cs");
        var generatedCode = queryFile.Contents.ToStringUtf8();
        var compilationUnit = ParseCompilationUnit(generatedCode);

        var argsTypeDeclaration = compilationUnit.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault(t => t.Identifier.ValueText == "TestOrderQueryArgs");

        Assert.That(argsTypeDeclaration, Is.Not.Null);

        // Get parameter order
        var propertyNames = new List<string>();
        if (argsTypeDeclaration is RecordDeclarationSyntax recordDecl)
        {
            var recordParameters = recordDecl.ParameterList?.Parameters ?? new SeparatedSyntaxList<ParameterSyntax>();
            propertyNames.AddRange(recordParameters.Select(p => p.Identifier.ValueText));
        }

        // Verify order is preserved and duplicates are removed
        var expectedOrder = new[] { "FirstParam", "DuplicateParam", "MiddleParam", "LastParam" };
        CollectionAssert.AreEqual(expectedOrder, propertyNames,
            $"Parameter order should be preserved with duplicates removed for {engine} engine");
    }

    [Test]
    [TestCase("postgresql")]
    [TestCase("mysql")]
    [TestCase("sqlite")]
    public void TestNoDuplicationWhenParametersAreUnique(string engine)
    {
        // Arrange - Verify that unique parameters work normally (regression test)
        var boolType = engine switch
        {
            "postgresql" => "boolean",
            "mysql" => "tinyint",
            "sqlite" => "integer",
            _ => "boolean"
        };

        var parameters = new[]
        {
            new Parameter { Number = 1, Column = new Column { Name = "param1", Type = new Identifier { Name = "text" } } },
            new Parameter { Number = 2, Column = new Column { Name = "param2", Type = new Identifier { Name = "integer" } } },
            new Parameter { Number = 3, Column = new Column { Name = "param3", Type = new Identifier { Name = boolType } } }
        };

        var (paramSyntax, _, settings, catalog) = GetEngineSpecificData(engine);

        var query = new Query
        {
            Filename = "query.sql",
            Cmd = ":many",
            Name = "UniqueParamsQuery",
            Text = "SELECT 1",
            Columns = { new Column { Name = "result", Type = new Identifier { Name = "integer" } } },
            Params = { parameters }
        };

        var request = new GenerateRequest
        {
            Settings = settings,
            Catalog = catalog,
            Queries = { query },
            PluginOptions = ByteString.CopyFrom("{}", Encoding.UTF8)
        };

        // Act
        var response = CodeGenerator.Generate(request);

        // Assert
        var queryFile = response.Result.Files.First(f => f.Name == "QuerySql.cs");
        var generatedCode = queryFile.Contents.ToStringUtf8();
        var compilationUnit = ParseCompilationUnit(generatedCode);

        var argsTypeDeclaration = compilationUnit.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault(t => t.Identifier.ValueText == "UniqueParamsQueryArgs");

        Assert.That(argsTypeDeclaration, Is.Not.Null);

        // Verify all unique parameters are present
        var propertyNames = new List<string>();
        if (argsTypeDeclaration is RecordDeclarationSyntax recordDecl)
        {
            var recordParameters = recordDecl.ParameterList?.Parameters ?? new SeparatedSyntaxList<ParameterSyntax>();
            propertyNames.AddRange(recordParameters.Select(p => p.Identifier.ValueText));
        }

        var expectedParams = new[] { "Param1", "Param2", "Param3" };
        CollectionAssert.AreEquivalent(expectedParams, propertyNames,
            $"All unique parameters should be present without any missing for {engine} engine");

        Assert.That(propertyNames, Has.Count.EqualTo(3),
            $"Should have exactly 3 parameters when all are unique for {engine} engine");
    }

    [Test]
    [TestCase("postgresql")]
    [TestCase("mysql")]
    [TestCase("sqlite")]
    public void TestParameterNullabilityConflictThrowsError(string engine)
    {
        // Arrange - Create parameters with the same name but different nullability
        var nullableColumn = new Column
        {
            Name = "conflicting_param",
            Type = new Identifier { Name = "text" },
            NotNull = false  // This represents sqlc.narg - nullable
        };

        var nonNullableColumn = new Column
        {
            Name = "conflicting_param",
            Type = new Identifier { Name = "text" },
            NotNull = true   // This represents sqlc.arg - non-nullable
        };

        var parameters = new[]
        {
            new Parameter { Number = 1, Column = new Column { Name = "other_param", Type = new Identifier { Name = "text" }, NotNull = true } },
            new Parameter { Number = 2, Column = nullableColumn.Clone() },
            new Parameter { Number = 3, Column = nonNullableColumn.Clone() }, // Same name, different nullability
            new Parameter { Number = 4, Column = new Column { Name = "final_param", Type = new Identifier { Name = "text" }, NotNull = false } }
        };

        var (paramSyntax, _, settings, catalog) = GetEngineSpecificData(engine);

        var query = new Query
        {
            Filename = "query.sql",
            Cmd = ":many",
            Name = "ConflictingNullabilityQuery",
            Text = $"SELECT 1 WHERE other_param = {paramSyntax}1 AND (conflicting_param IS NULL OR conflicting_param = {paramSyntax}2 OR conflicting_param != {paramSyntax}3) AND final_param = {paramSyntax}4",
            Columns = { new Column { Name = "result", Type = new Identifier { Name = "integer" } } },
            Params = { parameters }
        };

        var request = new GenerateRequest
        {
            Settings = settings,
            Catalog = catalog,
            Queries = { query },
            PluginOptions = ByteString.CopyFrom("{}", Encoding.UTF8)
        };

        // Act & Assert - Should throw an exception about conflicting nullability
        var exception = Assert.Throws<InvalidOperationException>(() => CodeGenerator.Generate(request));

        Assert.That(exception.Message, Contains.Substring("Duplicate identifier 'conflicting_param' used on nullable and non-nullable arguments"));
        Assert.That(exception.Message, Contains.Substring("query 'ConflictingNullabilityQuery'"));
    }

    [Test]
    [TestCase("postgresql")]
    [TestCase("mysql")]
    [TestCase("sqlite")]
    public void TestParameterSameNullabilityDoesNotThrowError(string engine)
    {
        // Arrange - Create parameters with the same name and same nullability (should work fine)
        var nullableColumn1 = new Column
        {
            Name = "same_param",
            Type = new Identifier { Name = "text" },
            NotNull = false  // Both nullable
        };

        var nullableColumn2 = new Column
        {
            Name = "same_param",
            Type = new Identifier { Name = "text" },
            NotNull = false  // Both nullable
        };

        var parameters = new[]
        {
            new Parameter { Number = 1, Column = new Column { Name = "other_param", Type = new Identifier { Name = "text" }, NotNull = true } },
            new Parameter { Number = 2, Column = nullableColumn1.Clone() },
            new Parameter { Number = 3, Column = nullableColumn2.Clone() }, // Same name, same nullability - OK
        };

        var (paramSyntax, _, settings, catalog) = GetEngineSpecificData(engine);

        var query = new Query
        {
            Filename = "query.sql",
            Cmd = ":many",
            Name = "SameNullabilityQuery",
            Text = $"SELECT 1 WHERE other_param = {paramSyntax}1 AND (same_param IS NULL OR same_param = {paramSyntax}2 OR same_param = {paramSyntax}3)",
            Columns = { new Column { Name = "result", Type = new Identifier { Name = "integer" } } },
            Params = { parameters }
        };

        var request = new GenerateRequest
        {
            Settings = settings,
            Catalog = catalog,
            Queries = { query },
            PluginOptions = ByteString.CopyFrom("{}", Encoding.UTF8)
        };

        // Act & Assert - Should NOT throw an exception
        Assert.DoesNotThrow(() =>
        {
            var response = CodeGenerator.Generate(request);
            Assert.That(response.Result.Files, Is.Not.Empty);
        });
    }

    [Test]
    [TestCase("postgresql")]
    [TestCase("mysql")]
    [TestCase("sqlite")]
    public void TestParameterDeduplicationInGeneratedMethodCode(string engine)
    {
        // Arrange - Create a query with duplicate parameter usage to test method generation
        var duplicateColumn = new Column
        {
            Name = "test_param",
            Type = new Identifier { Name = "text" },
            NotNull = false
        };

        var parameters = new[]
        {
            new Parameter { Number = 1, Column = new Column { Name = "other_param", Type = new Identifier { Name = "text" }, NotNull = true } },
            new Parameter { Number = 2, Column = duplicateColumn.Clone() },
            new Parameter { Number = 3, Column = duplicateColumn.Clone() }, // Duplicate - same name, same nullability
            new Parameter { Number = 4, Column = duplicateColumn.Clone() }  // Another duplicate
        };

        var (paramSyntax, _, settings, catalog) = GetEngineSpecificData(engine);

        var query = new Query
        {
            Filename = "query.sql",
            Cmd = ":many",
            Name = "TestMethodParameterDeduplication",
            Text = $"SELECT 1 WHERE other_param = {paramSyntax}1 AND (test_param IS NULL OR test_param = {paramSyntax}2 OR test_param != {paramSyntax}3 OR test_param LIKE {paramSyntax}4)",
            Columns = { new Column { Name = "result", Type = new Identifier { Name = "integer" } } },
            Params = { parameters }
        };

        var request = new GenerateRequest
        {
            Settings = settings,
            Catalog = catalog,
            Queries = { query },
            PluginOptions = ByteString.CopyFrom("{}", Encoding.UTF8)
        };

        // Act
        var response = CodeGenerator.Generate(request);

        // Assert
        Assert.That(response.Result.Files, Is.Not.Empty);

        var queryFile = response.Result.Files.First(f => f.Name == "QuerySql.cs");
        var generatedCode = queryFile.Contents.ToStringUtf8();

        // Get the appropriate parameter name based on engine
        var parameterName = engine switch
        {
            "postgresql" => "@test_param",
            "mysql" or "sqlite" => "@test_param", // All engines use @paramName in generated code
            _ => throw new ArgumentException($"Unsupported engine: {engine}")
        };

        // Verify that each parameter appears exactly twice (once per code branch - connection and transaction)
        // This is expected because we generate two code branches for handling different connection scenarios
        var testParamOccurrences = System.Text.RegularExpressions.Regex.Matches(
            generatedCode, @"command\.Parameters\.AddWithValue\(""@test_param""").Count;

        Assert.That(testParamOccurrences, Is.EqualTo(2),
            $"test_param should be added exactly twice (once per branch) in the generated method for {engine} engine");

        var otherParamOccurrences = System.Text.RegularExpressions.Regex.Matches(
            generatedCode, @"command\.Parameters\.AddWithValue\(""@other_param""").Count;

        Assert.That(otherParamOccurrences, Is.EqualTo(2),
            $"other_param should be added exactly twice (once per branch) in the generated method for {engine} engine");

        // Verify the Args record only has unique parameters (not duplicated in the record itself)
        Assert.That(generatedCode, Contains.Substring("TestMethodParameterDeduplicationArgs(string OtherParam, string? TestParam)"),
            $"Args record should contain unique parameters for {engine} engine");

        // Verify no consecutive duplicate parameter additions within the same branch
        // This regex looks for the same parameter being added twice in a row within the same block
        Assert.That(generatedCode, Does.Not.Match(@"AddWithValue\(""@test_param""[^}]*AddWithValue\(""@test_param"""),
            $"Should not have duplicate AddWithValue calls for the same parameter within a single code branch for {engine} engine");
    }
}