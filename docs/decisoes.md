# Decisões de implementação

Decisões tomadas durante a implementação que o `PLANO.md` não cobria ou que precisaram de ajuste.

## Fase 0

- **Versões:** Avalonia 12.1.4, CommunityToolkit.Mvvm 8.4.2, xUnit v3 4.0.2. Versões centralizadas em `Directory.Packages.props`; SDK fixado em `global.json` (10.0.400, `latestFeature`).
- **Testes com Microsoft.Testing.Platform:** o .NET 10 SDK não roda mais xUnit v3 pelo VSTest; `global.json` liga o novo `dotnet test`.
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

## Fase 1

- **Interfaces de SO no Core:** o `ExtensionRunner` recebe as interfaces de plataforma por injeção, mas o Core não pode referenciar o `Improve.Platform` (que referencia o Core). Por isso `IClipboard`, `IKeyboard`, `IFocusService` e `ISelectionService` ficam em `Improve.Core.Execution`; as implementações por SO ficam no `Improve.Platform`.
- **Lógica da área de transferência no Core:** `ClipboardSelectionService` implementa `ISelectionService` sobre duas primitivas simples (`IClipboard`, `IKeyboard`): salvar → limpar → copiar → esperar (15 ms, até 350 ms) → restaurar; colar → esperar 400 ms → restaurar só se ainda for o texto do improve. Assim essas regras ficam testadas sem SO. Uma plataforma pode trocar a implementação se precisar (por exemplo, detectar mudança pelo contador de sequência do SO).
- **Área de transferência restaurada logo após a captura:** a seleção já está em memória, então o conteúdo do usuário volta antes da chamada à IA, e não só no fim. O texto anterior também alimenta `{{clipboard}}`.
- **Timeout por inatividade:** os 60 s contam desde o último pedaço recebido, não desde o início, para não cortar respostas longas que ainda estão chegando. Timeout é mapeado para "provedor indisponível"; o cancelamento do usuário segue como `OperationCanceledException`.
- **`max_tokens`:** a Anthropic exige o campo (usamos 8192). Na API compatível com OpenAI ele não é enviado, porque os modelos de raciocínio da OpenAI rejeitam `max_tokens`.
- **"Testar conexão" lista os modelos:** confere a chave e mede a latência sem gastar tokens.
- **Erros do provedor:** `ProviderException.Message` já é a mensagem em pt-BR para a UI (`Resources/CoreStrings.resx`). Para 400/404, mostra a mensagem do provedor resumida em 200 caracteres.
- **Logs:** `Microsoft.Extensions.Logging.Abstractions` (dependência nova) no `ExtensionRunner`, só com provedor, modelo, duração, tamanhos e tipo de erro.
- **Extensões de exemplo sem modelo:** como não fixamos IDs de modelo, as 4 extensões da primeira execução ficam com `model` vazio e ligadas ao primeiro provedor. Executar sem modelo dá a mensagem "Escolha um modelo…".
- **Arquivo de configuração corrompido:** é renomeado para `*.corrompido-<data>` e o app recomeça do padrão. Arquivo de versão mais nova gera erro e não é tocado.
- **Segredos:** Windows usa DPAPI (`System.Security.Cryptography.ProtectedData`, dependência nova) em `secrets.dat`; macOS usa o CLI `security` como primeiro passo, e a chave aparece por um instante nos argumentos do processo ao gravar, então o próximo passo é trocar por P/Invoke no Security.framework (fase 5); Linux usa `secret-tool`, com a chave pelo stdin, e cai para `secrets.json` com permissão 600 (`IsSecure = false`) se não houver serviço de segredos.
- **Avisos de análise desligados:** CA1863 (cachear `CompositeFormat`), porque as strings vêm de `.resx` e variam com a cultura; CA1707 só em `tests/`, porque os nomes de teste usam sublinhados.
