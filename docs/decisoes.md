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

## Fase 2

- **`ExtensionGenerator` separado do `ExtensionRunner`:** a geração (prompt → streaming → limpeza) não depende do SO, então o botão "Testar" da janela funciona antes das integrações da fase 3. O `ExtensionRunner` cuida da captura e da aplicação e delega a geração.
- **`ConfigState` no Core:** configurações e extensões em memória, compartilhadas pela janela (e depois pelo popup), com evento `Changed`. Toda alteração é gravada na hora. O evento pode chegar fora da thread de UI; os ViewModels usam `Dispatcher.UIThread.Post`.
- **Salvar explícito nos editores, imediato na aba Geral:** extensão e provedor têm Salvar/Cancelar (para não gravar um prompt pela metade); atalho, tema e "iniciar com o sistema" gravam assim que mudam.
- **Atalhos (`Hotkey`):** formato texto `Ctrl+Alt+Shift+Cmd+Tecla` (no Windows/Linux o modificador Meta aparece como `Win`). Exige um modificador, exceto F1–F24. Conflito detectado entre o atalho global e os atalhos das extensões ativas. Conflito com atalhos de outros apps só é detectável ao registrar (fase 3).
- **Importação:** o arquivo exportado não leva provedor nem atalho; ao importar, uma barra pede o provedor antes de adicionar.
- **Realce de `{{variáveis}}` sem dependência nova:** um `TextBox` com texto transparente e um `TextBlock` com a mesma fonte, quebra e margem por cima, desenhando as variáveis em `accent-ink` sobre `accent-soft`. Só a cor muda, para não desalinhar do texto real (conferido sobrepondo os dois).
- **Seletor de ícones:** busca por partes do nome entre os 2.275 ícones do Lucide, mostrando no máximo 120 resultados para abrir rápido.
- **Reordenar:** arrastando pela alça (ponteiro, sem a API de drag-and-drop) ou com Alt+↑/↓ na alça.
- **Controles com `ControlTheme` próprio:** botão (primário, secundário, fantasma, perigo), interruptor e segmentado seguem `referencia-componentes.css` com tokens. Campos e combos mantêm o template do Fluent, com cores sobrescritas pelos tokens.
- **Layout em colunas:** a pedido do usuário, a janela de configurações tem dois painéis arredondados (`radius-lg`) sobre `surface-100`: navegação em `surface-200` e conteúdo em `surface-100` com contorno `border-subtle`. Nenhum token novo.
- **"Iniciar com o sistema" e "Pausar improve":** a opção é gravada e o estado de pausa existe no app, mas o efeito real (chave Run, LaunchAgent, desligar o atalho) vem com as integrações de cada SO (fases 3, 5 e 6).
- **`IMPROVE_CONFIG_DIR`:** variável de ambiente que troca a pasta de configuração (testes manuais sem mexer na configuração real; instalação portátil).
- **Dependências novas:** `Microsoft.Extensions.DependencyInjection` (previsto no plano) e `Microsoft.Extensions.Logging` (implementação do `ILogger` usado pelo Core; nenhum destino de log configurado ainda).
- **Arraste pelo cartão inteiro:** os eventos de ponteiro são ouvidos no túnel do contêiner da lista (inclusive os já tratados, porque a alça é um botão). O arraste começa após 4 px, então um clique continua abrindo o editor. O destino é o espaço cujo centro está mais perto do ponteiro, medido no início do arraste (durante o arraste o layout ainda não refletiu as trocas).
- **Animações (tokens `motion-fast` 120 ms e `motion-ease`):** a pedido do usuário, o app ganhou transições curtas: interruptor desliza, hover em botões, menu, segmentado, chips e cartões, e entrada de página/editor com fade e leve subida (8 px). Duração única de 120 ms, igual ao limite do BRAND para o popup. Tokens novos em `design/tokens.json` (seção `motion`), gerados em `Tokens.axaml`; a página do design system precisa receber a mesma seção. Avalonia não anima `RenderTransform` em keyframes: a entrada usa `TranslateTransform.Y`.
- **Editor de prompt com AvaloniaEdit:** a pedido do usuário, o prompt usa `Avalonia.AvaloniaEdit` 12.0.0 (dependência nova, MIT): edição nativa (cursor, desfazer, seleção) com realce do Markdown comum em prompts (títulos, listas, negrito, itálico, `código`, citações) e das `{{variáveis}}`, por um `DocumentColorizingTransformer` próprio. Não usamos o TextMate (gramáticas e biblioteca nativa a mais). Substitui a sobreposição TextBox + TextBlock. O `Markdown.Avalonia` só renderiza, não edita, e o resultado da IA não deve ter Markdown, então não foi adotado.

## Fase 3

- **SharpHook 8.0.1** (dependência prevista): atalho global e simulação de Ctrl+C/V. Usamos o `SimpleGlobalHook` (handlers síncronos na thread do hook) para poder marcar `SuppressEvent` e a tecla do atalho não chegar ao app em foco; o trabalho segue numa thread do pool. O simulador vem de `EventSimulator.Create("improve")` (API do 8.x). Eventos simulados são ignorados pelo hook.
- **Espera dos modificadores:** o hook acompanha Ctrl/Shift/Alt/Meta pressionados; `WaitForModifiersReleasedAsync` espera até 400 ms.
- **Área de transferência pela API Win32, não pela do Avalonia:** o instantâneo precisa de todos os formatos (imagem, HTML, RTF, arquivos) para devolver ao usuário exatamente o que ele tinha, e a API nativa faz isso sem depender da thread de UI. Formatos que são handles GDI (bitmap, metafile, paleta) e os textos sintetizados (CF_TEXT/CF_OEMTEXT) não são copiados; CF_DIB e CF_UNICODETEXT cobrem esses casos. `OpenClipboard(NULL)` funcionou para gravar (testado), apesar do aviso da documentação.
- **Foco:** `GetForegroundWindow` na captura; na aplicação, se a janela em foco for de outro processo que não o improve, o usuário mudou de janela e nada é colado. Para reativar o alvo, `AttachThreadInput` com a thread em foco + `SetForegroundWindow`; espera até 200 ms confirmar.
- **Atalhos ignorados com o improve em foco:** evita disparar ao gravar um atalho nas configurações e não tem utilidade aplicar extensões no próprio improve.
- **Instância única:** mutex `Local\improve-<usuário>` + named pipe com `PipeOptions.CurrentUserOnly`. Um segundo processo repassa os argumentos e sai: `--trigger` executa o atalho global; sem argumentos, abre as configurações. Isso já cobre o `improve --trigger` da fase 6.
- **Iniciar com o Windows:** valor `improve` em `HKCU\...\Run` com o caminho do executável, sincronizado com a opção da aba Geral a cada mudança.
- **Provisório até a fase 4:** sem popup, o atalho global executa a primeira extensão ativa; os modos "sugerir" e o caso "a janela mudou" copiam o resultado para a área de transferência. Erros e "sem seleção" só vão para o log (só metadados).
- **Teste ponta a ponta automatizado (local):** servidor falso compatível com OpenAI em 127.0.0.1 + janela WinForms de teste + `improve --trigger`: texto substituído e área de transferência original restaurada.
- **Limitação conhecida:** atalhos com Alt no Windows podem ativar a barra de menu do app de origem (o app vê Alt pressionado e solto sem outra tecla, já que a tecla do atalho é suprimida). O atalho padrão não usa Alt.

## Pedidos durante a fase 3

- **Chips de variável no prompt:** abaixo do editor, a dica "Aceita Markdown…" e botões `{{selection}}`, `{{app}}`, `{{clipboard}}` (com tooltip) que inserem a variável no cursor ou no lugar da seleção.
- **Multi-idioma (pt-BR, en, es):** o plano previa só pt-BR com recursos prontos para tradução; o usuário pediu os três idiomas já. `Strings.resx`/`CoreStrings.resx` continuam em pt-BR (idioma neutro) e ganharam `.en.resx` e `.es.resx`. Nova opção `language` em `settings.json` (`system`, `pt-BR`, `en`, `es`; ausente = `system`, sem migração). "Seguir o sistema" usa o idioma da interface do SO quando for português ou espanhol; qualquer outro vira inglês. O idioma é aplicado antes de carregar as extensões, para os exemplos da primeira execução saírem no idioma certo (em inglês, a extensão de tradução vira "Translate to Spanish"). Trocar o idioma reaplica a cultura, refaz o menu da bandeja e reabre a janela de configurações na mesma página. Os testes fixam pt-BR por um `ModuleInitializer`, porque o CI roda em inglês.
- **A instrução-base do prompt continua em português** em todos os idiomas: é texto para o modelo, não para o usuário, e já manda preservar o idioma original do texto.

## Fase 4

- **Uma janela flutuante, quatro estados:** popup de ações, "processando", painel de sugestão e aviso rápido vivem na mesma `PopupWindow` (criada no início e oculta). Assim o popup "vira" o painel no mesmo lugar, e só uma janela precisa ser aquecida.
- **Aquecimento:** na inicialização a janela é mostrada uma vez invisível (opacidade 0, fora da tela, sem foco) já com a lista real de extensões, e escondida. Sem isso, a primeira exibição levava ~200 ms a mais.
- **Sem acrílico no v1:** o `TransparencyLevelHint` do Avalonia aplica o blur no retângulo inteiro da janela, e a sombra `shadow-popup` precisa de uma margem transparente em volta do cartão; com acrílico, essa margem viraria um retângulo borrado. Usamos janela transparente + cartão `surface-200` (o fallback que o próprio token `surface-glass` indica) + sombra. Rever com blur por região no futuro.
- **`WindowDecorations="None"`** (Avalonia 12), `Topmost`, `ShowInTaskbar=False`, `SizeToContent`. Posição em pixels físicos: o cartão (não a janela, que tem a margem da sombra) fica 12 px abaixo/à direita do cursor, vira para o outro lado se não couber e é limitado à área de trabalho do monitor do cursor; reposiciona quando o tamanho muda (lista → painel).
- **Foco:** ao abrir, a janela força o primeiro plano com o mesmo truque do `AttachThreadInput` (`IWindowActivator`). Ao aplicar, o foco volta ao app de origem e só então o popup some (callback novo em `ExtensionRunner.ApplyAsync`), antes de colar.
- **Captura confiável no Windows:** dois problemas apareceram no teste automatizado, ambos no primeiro uso: (1) a primeira simulação do SharpHook demora a inicializar; (2) ler a área de transferência a cada 15 ms disputa com o app de origem enquanto ele grava a cópia. Soluções: Ctrl+C/V com `SendInput` nativo e espera dos modificadores com `GetAsyncKeyState` (`WindowsKeyboard`); a espera usa `GetClipboardSequenceNumber` (`IClipboard.ChangeCount`) e só abre a área de transferência depois que ela mudou. O SharpHook segue no atalho global e na simulação do macOS/Linux. Depois disso, 12 de 12 execuções seguidas passaram.
- **Latência medida** (log, atalho → popup visível, incluindo a captura): primeiro uso ~140–160 ms, seguintes 55–95 ms.
- **Teclado:** ↑/↓ (com volta), ↵ executa, 1–9 pelo número da lista completa (mesmo filtrada), digitar filtra, Esc fecha/cancela. No painel, ↵ aplica; no campo Refinar, ↵ refina.
- **Fechar ao perder o foco:** no popup de ações e no painel; não durante o processamento nem durante o streaming.
- **Fonte mono no painel:** quando o ícone da extensão é de código (`code`, `terminal`, `braces`).
- **Giro do indicador** de 0,9 s: animação contínua, não uma transição, por isso fora do token `motion-fast`.
- **Log em arquivo opcional:** `IMPROVE_LOG=1` grava `improve.log` na pasta de configuração, só com metadados (tipo de atalho, tamanhos, tempos, provedor/modelo).
- **Tipografia gerada com `:is(TextBlock)`:** para os estilos `body`, `code` etc. valerem também em `SelectableTextBlock` (resultado do painel).

## Ajustes após o primeiro uso real (DeepSeek)

- **Campo de modelo:** volta a ser `AutoCompleteBox` (filtra ao digitar, aceita ID livre), mas abre a lista completa ao receber o foco, e a legenda diz "clique no campo para ver a lista". O `ComboBox` editável do Fluent foi testado e descartado: a caixa de texto interna fica com moldura própria e escolher um item não atualizava o texto ligado ao modelo.
- **Abrir mostra a janela:** aberto pelo usuário, o app abre as configurações (só a bandeja dava a impressão de que nada tinha aberto). O início automático com o Windows usa `--background` e fica só na bandeja.
- **Instância única por pasta de configuração:** o nome do mutex/pipe inclui um hash da pasta. Com a pasta padrão nada muda; uma pasta de teste (`IMPROVE_CONFIG_DIR`) roda como instância separada e não recebe nem envia `--trigger` para a instância do usuário.
- **Prompts de exemplo mais explícitos:** "Deixar formal" removia saudações informais ("meu amigo, como vai"). Os exemplos agora pedem para manter todas as informações e adaptar saudações ao tom em vez de removê-las. Vale só para novas instalações.
- **Feedback de espera no painel:** até o primeiro pedaço chegar, o corpo mostra o indicador girando e "Gerando resposta…"; durante o streaming, o cursor limão pisca.
- **Traduções no repositório:** `tools/gen-strings/gen-strings.py` guarda as tabelas pt-BR/en/es e gera os seis `.resx`. Edite as tabelas e rode o script, em vez de editar os `.resx` à mão.
