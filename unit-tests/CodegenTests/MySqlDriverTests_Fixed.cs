using NUnit.Framework;
using Plugin;
using SqlcGenCsharp;
using SqlcGenCsharp.Drivers;
using System.Reflection;

namespace CodegenTests;

public class MySqlDriverTests_Fixed
{
    private MySqlConnectorDriver _driver;
    private Catalog _catalog;
    private List<Query> _queries;

    [SetUp]
    public void SetUp()
    {
        // Create a minimal GenerateRequest for Options
        var generateRequest = new GenerateRequest
        {
            Settings = new Settings
            {
                Engine = "mysql",
                Codegen = new Codegen { Out = "test" }
            },
            PluginOptions = Google.Protobuf.ByteString.CopyFromUtf8("{}")
        };

        var options = new Options(generateRequest);
        _catalog = new Catalog();
        _queries = new List<Query>();

        _driver = new MySqlConnectorDriver(options, _catalog, _queries);
    }

    [Test]
    public void TransformQueryText_WithInlineComment_RemovesCommentAndPreservesParameters()
    {
        // Arrange
        var sql = "INSERT INTO authors (name, bio) VALUES (?, ?) -- comment with ?";

        // Act
        var result = _driver.TransformQueryText(sql);

        // Assert
        Assert.That(result, Is.EqualTo("INSERT INTO authors (name, bio) VALUES (@name, @bio)"));
    }

    [Test]
    public void TransformQueryText_WithInlineCommentAtBeginning_RemovesComment()
    {
        // Arrange
        var sql = "-- comment with ?\nINSERT INTO authors (name, bio) VALUES (?, ?)";

        // Act
        var result = _driver.TransformQueryText(sql);

        // Assert
        Assert.That(result, Is.EqualTo("INSERT INTO authors (name, bio) VALUES (@name, @bio)"));
    }

    [Test]
    public void TransformQueryText_WithBlockComment_RemovesCommentAndPreservesParameters()
    {
        // Arrange
        var sql = "INSERT INTO authors (name, bio) /* comment with ? */ VALUES (?, ?)";

        // Act
        var result = _driver.TransformQueryText(sql);

        // Assert
        Assert.That(result, Is.EqualTo("INSERT INTO authors (name, bio)  VALUES (@name, @bio)"));
    }

    [Test]
    public void TransformQueryText_WithMultilineBlockComment_RemovesCommentAndPreservesParameters()
    {
        // Arrange
        var sql = @"INSERT INTO authors (name, bio) 
/* comment with ? 
   more comment with ? */ 
VALUES (?, ?)";

        // Act
        var result = _driver.TransformQueryText(sql);

        // Assert
        Assert.That(result, Is.EqualTo("INSERT INTO authors (name, bio) \n \nVALUES (@name, @bio)"));
    }

    [Test]
    public void TransformQueryText_WithMixedComments_RemovesCommentsAndPreservesParameters()
    {
        // Arrange
        var sql = @"-- comment with ?
INSERT INTO authors (name, bio) /* comment with ? */ VALUES (?, ?) -- final comment";

        // Act
        var result = _driver.TransformQueryText(sql);

        // Assert
        Assert.That(result, Is.EqualTo("\nINSERT INTO authors (name, bio)  VALUES (@name, @bio) "));
    }

    [Test]
    public void TransformQueryText_WithoutComments_ReplacesParametersOnly()
    {
        // Arrange
        var sql = "INSERT INTO authors (name, bio) VALUES (?, ?)";

        // Act
        var result = _driver.TransformQueryText(sql);

        // Assert
        Assert.That(result, Is.EqualTo("INSERT INTO authors (name, bio) VALUES (@name, @bio)"));
    }

    [Test]
    public void TransformQueryText_ParameterOrderPreservedAfterCommentRemoval()
    {
        // Arrange
        var sql = "SELECT * FROM authors WHERE name = ? AND bio = ? -- comment with ?";

        // Act
        var result = _driver.TransformQueryText(sql);

        // Assert
        Assert.That(result, Is.EqualTo("SELECT * FROM authors WHERE name = @name AND bio = @bio "));
    }

    [Test]
    public void RemoveComments_WithInlineComment_RemovesComment()
    {
        // Arrange
        var removeCommentsMethod = GetRemoveCommentsMethod();
        var sql = "SELECT * FROM authors -- comment with ?";

        // Act
        var result = (string)removeCommentsMethod.Invoke(_driver, new[] { sql });

        // Assert
        Assert.That(result, Is.EqualTo("SELECT * FROM authors "));
    }

    [Test]
    public void RemoveComments_WithBlockComment_RemovesComment()
    {
        // Arrange
        var removeCommentsMethod = GetRemoveCommentsMethod();
        var sql = "SELECT * FROM authors /* comment with ? */ WHERE id = 1";

        // Act
        var result = (string)removeCommentsMethod.Invoke(_driver, new[] { sql });

        // Assert
        Assert.That(result, Is.EqualTo("SELECT * FROM authors  WHERE id = 1"));
    }

    private MethodInfo GetRemoveCommentsMethod()
    {
        return typeof(MySqlConnectorDriver).GetMethod("RemoveComments",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
    }

    [Test]
    public void RemoveComments_Integration_RealWorldQuery()
    {
        // Arrange
        var removeCommentsMethod = GetRemoveCommentsMethod();
        var sql = @"-- Create an author with a bio that includes a question mark
INSERT INTO authors (name, bio) 
VALUES (?, ?) -- bio might contain '?'";

        // Act
        var result = (string)removeCommentsMethod.Invoke(_driver, new[] { sql });

        // Assert
        Assert.That(result, Does.Not.Contain("--"));
        Assert.That(result, Does.Contain("INSERT INTO authors"));
        Assert.That(result, Does.Contain("VALUES (?, ?)"));
    }
}