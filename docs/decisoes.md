# Decisões de implementação

Decisões tomadas durante a implementação que o `PLANO.md` não cobria ou que precisaram de ajuste.

## Fase 0

- **Versões:** Avalonia 12.1.4, CommunityToolkit.Mvvm 8.4.2, xUnit v3 4.0.2. Versões centralizadas em `Directory.Packages.props`; SDK fixado em `global.json` (10.0.400, `latestFeature`).
- **Testes com Microsoft.Testing.Platform:** o .NET 10 SDK não roda mais xUnit v3 pelo VSTest; `global.json` liga o novo `dotnet test`. Enquanto o Core não tem testes, o código de saída 8 ("nenhum teste") é ignorado em `Improve.Core.Tests.csproj`; remover na fase 1.
- **`WindowDecorations` em vez de `SystemDecorations`:** no Avalonia 12 `SystemDecorations` ficou obsoleta. As janelas flutuantes (fase 4) usarão `WindowDecorations="None"`.
- **Ícones Lucide sem Avalonia.Svg.Skia:** o pacote só existe para Avalonia 11. Seguindo a alternativa do plano, `tools/gen-icons/gen-lucide.cs` baixa o `lucide-static` 1.64.0 do registro npm e converte cada SVG (path, circle, rect, line, polyline…) num path data normalizado em `Assets/Lucide/lucide.json`. O controle `LucideIcon` desenha com traço 1.75 no espaço 24×24, escalado. Licença ISC em `licenses/Lucide-ISC.txt`.
- **`LucideIcon.Icon` em vez de `Name`:** `Name` já é o identificador de todo controle no Avalonia (usado por `x:Name`); escondê-lo quebraria a busca por nome. A propriedade do ícone se chama `Icon`.
- **Fontes estáticas:** o Avalonia não suporta fontes variáveis. Embutidas Inter 4.1 (Regular, Medium, SemiBold, Bold) e JetBrains Mono 2.304 (Regular, Medium), registradas como coleção `fonts:Improve`. O peso 650 do `display` é resolvido pelo Avalonia para o arquivo mais próximo.
- **Tokens gerados:** `tools/gen-tokens/gen-tokens.cs` gera `Tokens.Dark.axaml`, `Tokens.Light.axaml` (cores e sombra, em `ThemeDictionaries`), `Tokens.axaml` (espaços, raios, famílias de fonte) e `Typography.axaml` (estilos `TextBlock.*`). Roda manualmente; o CI falha se os arquivos estiverem desatualizados (`--check`). `letterSpacing` em `em` vira pixels (`-0.02em × 32px = -0.64`).
- **Ícones do app:** `tools/gen-icons/gen-app-icon.cs` renderiza os SVGs de `design/icons/` com Svg.Skia (só na ferramenta, não no app) e gera PNG 16–1024, `.ico` e `.icns`, além dos glifos da bandeja em 16/32/64 px.
- **Glifo da bandeja:** no Windows a barra de tarefas tem tema próprio (`SystemUsesLightTheme`), independente do tema dos apps; usamos o glifo claro em barra escura e o `mono` em barra clara. No macOS, o `mono` como template image. No Linux, segue o tema do app.
- **Strings:** `Resources/Strings.resx` com classe gerada pelo MSBuild (pública, para o `x:Static` do XAML). Idioma neutro pt-BR.
- **Janela de configurações provisória:** só mostra wordmark, ícone e slogan, para verificar fontes, ícones e tokens nos dois temas. A janela real vem na fase 2.
- **Downloads com certificado corporativo:** na máquina de desenvolvimento, o `curl` do Windows falha na checagem de revogação; as fontes foram baixadas com `--ssl-no-revoke` dos releases oficiais no GitHub.
