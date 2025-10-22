using NUnit.Framework;
using SqlcGenCsharp.Generators;
using System.Reflection;

namespace CodegenTests;

public class QueriesGenCommentRemovalTests
{
    [Test]
    [TestCase("INSERT INTO table VALUES (1, 2, 3)", "INSERT INTO table VALUES (1, 2, 3)")]
    [TestCase("INSERT INTO table -- comment\nVALUES (1, 2, 3)", "INSERT INTO table VALUES (1, 2, 3)")]
    [TestCase("INSERT INTO table VALUES (1, 2, 3) -- end comment", "INSERT INTO table VALUES (1, 2, 3)")]
    [TestCase("SELECT * FROM table WHERE id = 1 -- filter comment", "SELECT * FROM table WHERE id = 1")]
    [TestCase("-- full line comment\nSELECT * FROM table", "SELECT * FROM table")]
    [TestCase("SELECT *\n-- middle comment\nFROM table", "SELECT * FROM table")]
    [TestCase("SELECT * FROM table -- comment with special chars !@#$%^&*()", "SELECT * FROM table")]
    public void RemoveInlineCommentsAndCollapseWhitespace_InlineComments_RemovesCorrectly(string input, string expected)
    {
        // Use reflection to test the private method
        var method = typeof(QueriesGen).GetMethod("RemoveInlineCommentsAndCollapseWhitespace",
            BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = method?.Invoke(null, new object[] { input }) as string;

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    [TestCase("SELECT   *   FROM   table", "SELECT * FROM table")]
    [TestCase("SELECT\n\n*\nFROM\n\ntable", "SELECT * FROM table")]
    [TestCase("SELECT\t*\t\tFROM\ttable", "SELECT * FROM table")]
    [TestCase("  SELECT * FROM table  ", "SELECT * FROM table")]
    [TestCase("SELECT\r\n*\r\nFROM\r\ntable", "SELECT * FROM table")]
    public void RemoveInlineCommentsAndCollapseWhitespace_ExcessiveWhitespace_CollapsesCorrectly(string input, string expected)
    {
        // Use reflection to test the private method
        var method = typeof(QueriesGen).GetMethod("RemoveInlineCommentsAndCollapseWhitespace",
            BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = method?.Invoke(null, new object[] { input }) as string;

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    [TestCase("INSERT INTO authors (\n    id, -- this is an id\n    name, -- this is a name\n    bio -- comment?\n    ) VALUES (?, ?, ?)",
              "INSERT INTO authors ( id, name, bio ) VALUES (?, ?, ?)")]
    [TestCase("SELECT * FROM table -- comment\n   WHERE id = 1 -- another comment\n   ORDER BY name",
              "SELECT * FROM table WHERE id = 1 ORDER BY name")]
    public void RemoveInlineCommentsAndCollapseWhitespace_RealWorldExamples_ProcessesCorrectly(string input, string expected)
    {
        // Use reflection to test the private method
        var method = typeof(QueriesGen).GetMethod("RemoveInlineCommentsAndCollapseWhitespace",
            BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = method?.Invoke(null, new object[] { input }) as string;

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void RemoveInlineCommentsAndCollapseWhitespace_EmptyString_ReturnsEmpty()
    {
        // Use reflection to test the private method
        var method = typeof(QueriesGen).GetMethod("RemoveInlineCommentsAndCollapseWhitespace",
            BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = method?.Invoke(null, new object[] { "" }) as string;

        // Assert
        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void RemoveInlineCommentsAndCollapseWhitespace_OnlyComments_ReturnsEmpty()
    {
        // Use reflection to test the private method
        var method = typeof(QueriesGen).GetMethod("RemoveInlineCommentsAndCollapseWhitespace",
            BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = method?.Invoke(null, new object[] { "-- only a comment" }) as string;

        // Assert
        Assert.That(result, Is.EqualTo(""));
    }

    [Test]
    public void RemoveInlineCommentsAndCollapseWhitespace_CommentInMiddleOfLine_RemovesToEndOfLine()
    {
        // Use reflection to test the private method
        var method = typeof(QueriesGen).GetMethod("RemoveInlineCommentsAndCollapseWhitespace",
            BindingFlags.NonPublic | BindingFlags.Static);

        var input = "SELECT col1, -- this comment\ncol2 FROM table";
        var expected = "SELECT col1, col2 FROM table";

        // Act
        var result = method?.Invoke(null, new object[] { input }) as string;

        // Assert
        Assert.That(result, Is.EqualTo(expected));
    }
}