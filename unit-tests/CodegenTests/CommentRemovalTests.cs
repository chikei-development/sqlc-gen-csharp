using NUnit.Framework;

namespace CodegenTests;

public class CommentRemovalTests
{
    [Test]
    public void InlineCommentRegex_RemovesInlineComments()
    {
        // Test basic inline comment removal
        var pattern = @"--[^\r\n]*";
        var input = "SELECT * FROM authors -- this is a comment";
        var result = System.Text.RegularExpressions.Regex.Replace(input, pattern, "");

        Assert.That(result, Is.EqualTo("SELECT * FROM authors "));
    }

    [Test]
    public void BlockCommentRegex_RemovesBlockComments()
    {
        // Test basic block comment removal
        var pattern = @"/\*.*?\*/";
        var input = "SELECT * /* comment */ FROM authors";
        var result = System.Text.RegularExpressions.Regex.Replace(input, pattern, "",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        Assert.That(result, Is.EqualTo("SELECT *  FROM authors"));
    }

    [Test]
    public void CommentRemoval_WithQuestionMarkInComment_DoesNotAffectParameterCount()
    {
        // Test that question marks in comments don't interfere with parameter counting
        var sql = "INSERT INTO authors (name, bio) VALUES (?, ?) -- comment with ?";

        // Remove inline comments
        var withoutComments = System.Text.RegularExpressions.Regex.Replace(sql, @"--[^\r\n]*", "");

        // Count remaining question marks (actual parameters)
        var parameterCount = withoutComments.Count(c => c == '?');

        Assert.That(parameterCount, Is.EqualTo(2));
        Assert.That(withoutComments, Is.EqualTo("INSERT INTO authors (name, bio) VALUES (?, ?) "));
    }

    [Test]
    public void MixedComments_RemovalPreservesSQL()
    {
        // Test mixed comment removal
        var sql = @"-- Header comment
SELECT * FROM authors /* inline */ WHERE id = ? -- trailing comment";

        // Remove both types of comments
        var result = sql;
        result = System.Text.RegularExpressions.Regex.Replace(result, @"--[^\r\n]*", "");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"/\*.*?\*/", "",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        Assert.That(result, Does.Contain("SELECT * FROM authors"));
        Assert.That(result, Does.Contain("WHERE id = ?"));
        Assert.That(result, Does.Not.Contain("--"));
        Assert.That(result, Does.Not.Contain("/*"));
        Assert.That(result, Does.Not.Contain("*/"));
    }

    [Test]
    public void WhitespaceCollapse_CollapsesMultipleSpaces()
    {
        // Test whitespace collapsing after comment removal
        var input = "SELECT   *    FROM     authors";
        var result = System.Text.RegularExpressions.Regex.Replace(input, @"\s+", " ");

        Assert.That(result, Is.EqualTo("SELECT * FROM authors"));
    }
}