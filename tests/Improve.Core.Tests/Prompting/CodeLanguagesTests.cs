using Improve.Core.Prompting;

namespace Improve.Core.Tests.Prompting;

public class CodeLanguagesTests
{
    [Theory]
    [InlineData("public sealed class Pedido\n{\n    public string Nome { get; set; }\n    public async Task SalvarAsync() { }\n}", "csharp")]
    [InlineData("def soma(a, b):\n    return a + b\n\nprint(soma(1, 2))", "python")]
    [InlineData("const total = itens.reduce((s, i) => {\n  return s + i;\n}, 0);\nconsole.log(total);", "javascript")]
    [InlineData("interface Pedido {\n  nome: string;\n}\nexport const p: Pedido = { nome: 'a' };", "typescript")]
    [InlineData("SELECT nome, total FROM pedidos WHERE status = 'pago' ORDER BY total", "sql")]
    [InlineData("{ \"nome\": \"improve\", \"versao\": 1 }", "json")]
    [InlineData("package main\n\nimport \"fmt\"\n\nfunc main() {\n\tx := 1\n\tfmt.Println(x)\n}", "go")]
    [InlineData("fn main() {\n    let mut v = Vec::new();\n    println!(\"{}\", v.len());\n}", "rust")]
    [InlineData("#include <iostream>\nint main() {\n  std::cout << \"oi\";\n}", "cpp")]
    [InlineData("<div class=\"card\">\n  <p>Olá</p>\n</div>", "html")]
    [InlineData(".card {\n  color: red;\n  padding: 4px;\n}", "css")]
    [InlineData("#!/bin/bash\nif [ -f arquivo ]; then\n  echo $HOME\nfi", "shellscript")]
    [InlineData("$itens = Get-ChildItem\nif ($itens.Count -gt 0) { Write-Host 'ok' }", "powershell")]
    [InlineData("nome: improve\nversao: 1\nautor: igor", "yaml")]
    public void Detects_common_languages(string code, string expected) => Assert.Equal(expected, CodeLanguages.Detect(code));

    [Theory]
    [InlineData("oi pessoal, a reunião de amanhã vai atrasar 15 minutos")]
    [InlineData("")]
    [InlineData("   ")]
    public void Plain_text_has_no_language(string text) => Assert.Null(CodeLanguages.Detect(text));

    [Fact]
    public void Every_language_has_a_unique_id() =>
        Assert.Equal(CodeLanguages.All.Count, CodeLanguages.All.Select(l => l.Id).Distinct().Count());
}
