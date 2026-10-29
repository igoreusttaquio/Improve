using Improve.Core.Prompting;

namespace Improve.Core.Tests.Prompting;

public class ResponseCleanerTests
{
    [Fact]
    public void Trims_whitespace() => Assert.Equal("Olá", ResponseCleaner.Clean("  \n Olá \n ", "oi"));

    [Theory]
    [InlineData("```\nvar x = 1;\n```")]
    [InlineData("```csharp\nvar x = 1;\n```")]
    [InlineData("```csharp\r\nvar x = 1;\r\n```")]
    [InlineData("  ```js\nvar x = 1;```  ")]
    public void Removes_fence_around_whole_response(string response) =>
        Assert.Equal("var x = 1;", ResponseCleaner.Clean(response, "var x=1"));

    [Fact]
    public void Keeps_fence_when_original_had_one()
    {
        const string response = "```\ncode\n```";
        Assert.Equal(response, ResponseCleaner.Clean(response, "```\ncodigo\n```"));
    }

    [Fact]
    public void Keeps_fences_that_do_not_wrap_the_whole_response()
    {
        const string response = "Antes\n```\ncode\n```";
        Assert.Equal(response, ResponseCleaner.Clean(response, "x"));
    }

    [Fact]
    public void Keeps_multiple_code_blocks()
    {
        const string response = "```\na\n```\ntexto\n```\nb\n```";
        Assert.Equal(response, ResponseCleaner.Clean(response, "x"));
    }

    [Fact]
    public void Removes_residual_text_tags() =>
        Assert.Equal("Resultado", ResponseCleaner.Clean("<texto>\nResultado\n</texto>", "x"));

    [Theory]
    [InlineData("linha\n", "\n")]
    [InlineData("linha\r\n", "\r\n")]
    [InlineData("linha\n\n", "\n\n")]
    [InlineData("linha", "")]
    public void Preserves_trailing_line_breaks_of_selection(string selection, string expectedEnding) =>
        Assert.Equal("novo" + expectedEnding, ResponseCleaner.Clean("novo\n", selection));
}
