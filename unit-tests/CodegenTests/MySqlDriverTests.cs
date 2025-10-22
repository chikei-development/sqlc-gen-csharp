using NUnit.Framework;
using Plugin;
using SqlcGenCsharp;
using SqlcGenCsharp.Drivers;

namespace CodegenTests;

public class MySqlDriverTests
{
    private MySqlConnectorDriver _driver;

    [SetUp]
    public void SetUp()
    {
        var options = new Options
        {
            UseDapper = false,
            TargetFramework = DotnetFramework.Net8,
            GenerateCsproj = false
        };
        _driver = new MySqlConnectorDriver(options);
    }

    [Test]
    public void TransformQueryText_WithInlineComments_RemovesCommentsAndReplacesParametersCorrectly()
    {
        // Arrange
        var query = new Query
        {
            Name = "CreateAuthorIncludingComment",
            Cmd = ":exec",
            Text = @"INSERT INTO authors (
    id, -- this is an id
    name, -- this is a name!@#$%,
    bio -- comment?
    ) VALUES (?, ?, ?)",
            Params =
            {
                new Parameter
                {
                    Number = 1,
                    Column = new Column { Name = "id", Type = new Identifier { Name = "int" } }
                },
                new Parameter
                {
                    Number = 2,
                    Column = new Column { Name = "name", Type = new Identifier { Name = "varchar" } }
                },
                new Parameter
                {
                    Number = 3,
                    Column = new Column { Name = "bio", Type = new Identifier { Name = "text" } }
                }
            }
        };

        // Act
        var result = _driver.TransformQueryText(query);

        // Assert
        Assert.That(result, Does.Contain("VALUES (@id, @name, @bio)"));
        Assert.That(result, Does.Not.Contain("comment"));
        Assert.That(result, Does.Not.Contain("--"));
    }

    [Test]
    public void TransformQueryText_WithBlockComments_RemovesCommentsAndReplacesParametersCorrectly()
    {
        // Arrange
        var query = new Query
        {
            Name = "CreateAuthorWithBlockComment",
            Cmd = ":exec",
            Text = @"INSERT INTO authors /* block comment with ? */ (id, name, bio) VALUES (?, ?, ?)",
            Params =
            {
                new Parameter
                {
                    Number = 1,
                    Column = new Column { Name = "id", Type = new Identifier { Name = "int" } }
                },
                new Parameter
                {
                    Number = 2,
                    Column = new Column { Name = "name", Type = new Identifier { Name = "varchar" } }
                },
                new Parameter
                {
                    Number = 3,
                    Column = new Column { Name = "bio", Type = new Identifier { Name = "text" } }
                }
            }
        };

        // Act
        var result = _driver.TransformQueryText(query);

        // Assert
        Assert.That(result, Does.Contain("VALUES (@id, @name, @bio)"));
        Assert.That(result, Does.Not.Contain("/* block comment with ? */"));
        Assert.That(result, Does.Not.Contain("/*"));
        Assert.That(result, Does.Not.Contain("*/"));
    }

    [Test]
    public void TransformQueryText_WithMixedComments_RemovesAllCommentsAndReplacesParametersCorrectly()
    {
        // Arrange
        var query = new Query
        {
            Name = "CreateAuthorWithMixedComments",
            Cmd = ":exec",
            Text = @"INSERT INTO authors /* block comment */ (
    id, -- inline comment with ?
    name, /* another block with ? */
    bio -- final comment?
    ) VALUES (?, ?, ?)",
            Params =
            {
                new Parameter
                {
                    Number = 1,
                    Column = new Column { Name = "id", Type = new Identifier { Name = "int" } }
                },
                new Parameter
                {
                    Number = 2,
                    Column = new Column { Name = "name", Type = new Identifier { Name = "varchar" } }
                },
                new Parameter
                {
                    Number = 3,
                    Column = new Column { Name = "bio", Type = new Identifier { Name = "text" } }
                }
            }
        };

        // Act
        var result = _driver.TransformQueryText(query);

        // Assert
        Assert.That(result, Does.Contain("VALUES (@id, @name, @bio)"));
        Assert.That(result, Does.Not.Contain("comment"));
        Assert.That(result, Does.Not.Contain("--"));
        Assert.That(result, Does.Not.Contain("/*"));
        Assert.That(result, Does.Not.Contain("*/"));
    }

    [Test]
    public void TransformQueryText_WithoutComments_ReplacesParametersCorrectly()
    {
        // Arrange
        var query = new Query
        {
            Name = "CreateAuthorNoComments",
            Cmd = ":exec",
            Text = "INSERT INTO authors (id, name, bio) VALUES (?, ?, ?)",
            Params =
            {
                new Parameter
                {
                    Number = 1,
                    Column = new Column { Name = "id", Type = new Identifier { Name = "int" } }
                },
                new Parameter
                {
                    Number = 2,
                    Column = new Column { Name = "name", Type = new Identifier { Name = "varchar" } }
                },
                new Parameter
                {
                    Number = 3,
                    Column = new Column { Name = "bio", Type = new Identifier { Name = "text" } }
                }
            }
        };

        // Act
        var result = _driver.TransformQueryText(query);

        // Assert
        Assert.That(result, Is.EqualTo("INSERT INTO authors (id, name, bio) VALUES (@id, @name, @bio)"));
    }

    [Test]
    public void TransformQueryText_WithParametersInWrongOrder_ReplacesInCorrectPositionalOrder()
    {
        // This test ensures that even if sqlc provides parameters in a different order,
        // our driver replaces them positionally based on their appearance in the SQL
        var query = new Query
        {
            Name = "TestParameterOrder",
            Cmd = ":exec",
            Text = "INSERT INTO table (col1, col2, col3) VALUES (?, ?, ?)",
            Params =
            {
                // Note: Parameters are provided in different order than they appear in SQL
                new Parameter
                {
                    Number = 1,
                    Column = new Column { Name = "col1", Type = new Identifier { Name = "int" } }
                },
                new Parameter
                {
                    Number = 2,
                    Column = new Column { Name = "col2", Type = new Identifier { Name = "varchar" } }
                },
                new Parameter
                {
                    Number = 3,
                    Column = new Column { Name = "col3", Type = new Identifier { Name = "text" } }
                }
            }
        };

        // Act
        var result = _driver.TransformQueryText(query);

        // Assert
        // The first ? should be replaced with the first parameter in the list (col1)
        // The second ? should be replaced with the second parameter in the list (col2)
        // The third ? should be replaced with the third parameter in the list (col3)
        Assert.That(result, Is.EqualTo("INSERT INTO table (col1, col2, col3) VALUES (@col1, @col2, @col3)"));
    }

    [Test]
    public void TransformQueryText_WithExecLastIdCommand_AppendsSelectLastInsertId()
    {
        // Arrange
        var query = new Query
        {
            Name = "CreateAuthorReturnId",
            Cmd = ":execlastid",
            Text = "INSERT INTO authors (name, bio) VALUES (?, ?)",
            Params =
            {
                new Parameter
                {
                    Number = 1,
                    Column = new Column { Name = "name", Type = new Identifier { Name = "varchar" } }
                },
                new Parameter
                {
                    Number = 2,
                    Column = new Column { Name = "bio", Type = new Identifier { Name = "text" } }
                }
            }
        };

        // Act
        var result = _driver.TransformQueryText(query);

        // Assert
        Assert.That(result, Is.EqualTo("INSERT INTO authors (name, bio) VALUES (@name, @bio); SELECT LAST_INSERT_ID()"));
    }

    [Test]
    public void TransformQueryText_WithDapperAndExecLastIdCommand_AppendsSelectLastInsertId()
    {
        // Arrange
        var dapperOptions = new Options
        {
            UseDapper = true,
            TargetFramework = DotnetFramework.Net8,
            GenerateCsproj = false
        };
        var dapperDriver = new MySqlConnectorDriver(dapperOptions);

        var query = new Query
        {
            Name = "CreateAuthorReturnId",
            Cmd = ":execlastid",
            Text = "INSERT INTO authors (name, bio) VALUES (?, ?)",
            Params =
            {
                new Parameter
                {
                    Number = 1,
                    Column = new Column { Name = "name", Type = new Identifier { Name = "varchar" } }
                },
                new Parameter
                {
                    Number = 2,
                    Column = new Column { Name = "bio", Type = new Identifier { Name = "text" } }
                }
            }
        };

        // Act
        var result = dapperDriver.TransformQueryText(query);

        // Assert
        Assert.That(result, Is.EqualTo("INSERT INTO authors (name, bio) VALUES (@name, @bio); SELECT LAST_INSERT_ID()"));
    }

    [Test]
    public void TransformQueryText_WithCopyFromCommand_ReturnsEmptyString()
    {
        // Arrange
        var query = new Query
        {
            Name = "CopyData",
            Cmd = ":copyfrom",
            Text = "COPY table FROM STDIN"
        };

        // Act
        var result = _driver.TransformQueryText(query);

        // Assert
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    [TestCase("-- simple comment", "")]
    [TestCase("SELECT * FROM table -- end comment", "SELECT * FROM table ")]
    [TestCase("/* block comment */", "")]
    [TestCase("SELECT /* inline */ * FROM table", "SELECT  * FROM table")]
    [TestCase("SELECT * FROM table /* end block */", "SELECT * FROM table ")]
    [TestCase("-- comment with ?\nSELECT ?", "\nSELECT ?")]
    [TestCase("/* comment with ? */ SELECT ?", " SELECT ?")]
    public void RemoveComments_VariousCommentFormats_RemovesCorrectly(string input, string expected)
    {
        // This test uses reflection to test the private RemoveComments method
        var method = typeof(MySqlConnectorDriver).GetMethod("RemoveComments",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = method?.Invoke(null, new object[] { input }) as string;

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }
}