# Testes manuais

Roteiro para o que não é coberto por `dotnet test`. Atualizado a cada fase.

## Fase 0 — Fundação

Rodar: `dotnet run --project src/Improve.App`

1. O app não abre janela; aparece o glifo do improve na bandeja (claro em barra escura, escuro em barra clara).
2. Menu da bandeja → "Abrir configurações": abre a janela com o tile do ícone `wand-sparkles`, o wordmark "improve" em Inter e o slogan.
3. Trocar o tema do SO (Windows: Configurações > Personalização > Cores > Modo do app) com a janela aberta: fundo, texto e tile alternam entre os tokens escuros e claros sem reiniciar.
4. Trocar o modo do Windows (barra de tarefas): o glifo da bandeja troca de claro para escuro.
5. Menu da bandeja → "Sair": o processo termina.

## Fase 1 — Core

Streaming real (a chave fica só na variável de ambiente):

```
$env:IMPROVE_API_KEY = "<chave>"
dotnet run tools/try-provider/try-provider.cs -- anthropic                         # testa a conexão e lista os modelos
dotnet run tools/try-provider/try-provider.cs -- anthropic <modelo> "oi gente, reuniao atrasa 15min"
dotnet run tools/try-provider/try-provider.cs -- openai <modelo> "texto" https://api.deepseek.com/v1
dotnet run tools/try-provider/try-provider.cs -- openai <modelo> "texto" http://localhost:11434/v1   # Ollama, sem chave
```

1. Com chave válida, o texto aparece aos poucos (streaming) e a linha final mostra o resultado limpo.
2. Com chave inválida: "Chave de API inválida para Anthropic. Abra Configurações > Provedores."
3. Sem rede (ou URL errada): "Sem conexão com …".
4. Ctrl+C durante o streaming: "Cancelado."

Armazenamento de chaves (por SO):

- Windows: a chave gravada fica em `%APPDATA%\improve\secrets.dat` cifrada (não aparece em texto puro).
- macOS: aparece no app Acesso às Chaves como item `improve`, conta = id do provedor.
- Linux com GNOME Keyring/KWallet: `secret-tool lookup service improve account <id>` devolve a chave. Sem serviço de segredos: `~/.config/improve/secrets.json` com permissão `-rw-------`.

## Fase 2 — Configurações

Para não mexer na configuração real: `$env:IMPROVE_CONFIG_DIR = "$env:TEMP\improve-teste"; dotnet run --project src/Improve.App`

1. Bandeja → "Abrir configurações": janela com menu lateral (Extensões, Provedores, Geral, Sobre) e conteúdo em painéis arredondados.
2. Provedores → Anthropic: colar a chave, Salvar. Reabrir: "Chave salva. Deixe em branco para manter." Conferir que a chave não aparece em `settings.json` nem `extensions.json`.
3. "Testar conexão" com chave válida: ícone e texto em verde com a latência. Com chave inválida: vermelho, "Chave de API inválida…".
4. Adicionar provedor "Compatível com OpenAI" (DeepSeek ou Ollama `http://localhost:11434/v1`); trocar o tipo troca a URL padrão.
5. Extensões → Nova extensão: escolher ícone pela busca, escrever prompt com `{{selection}}` (fica realçado), escolher provedor; a lista de modelos carrega sozinha (ou pelo botão ⟳); escolher modelo e modo.
6. Atalho próprio: clicar no campo e pressionar uma combinação; repetir o atalho global mostra "Em uso: atalho global."; Backspace limpa.
7. "Testar": o resultado aparece em streaming; "Parar" interrompe.
8. Salvar; o cartão aparece na lista com o chip do modo. Arrastar pela alça reordena; Alt+↑/↓ também. O interruptor desliga a extensão (o chip fica neutro e o número do atalho some).
9. Exportar e depois Importar o arquivo: a barra pede o provedor; as extensões entram no fim da lista.
10. Geral: trocar o tema alterna a janela na hora; o atalho global aceita só combinações válidas.
11. Fechar o app (Sair) e abrir de novo: tudo persiste.
12. Bandeja → "Pausar improve" vira "Retomar improve".

## Fase 3 — Integração Windows

Preparação: provedor com chave válida, uma extensão em modo **Substituir** como a primeira da lista e, opcionalmente, um atalho próprio numa extensão. Antes de cada teste, copie um texto qualquer ("CLIP") para conferir que ele volta.

Para cada app — Bloco de Notas, Word, Chrome (campo de texto e Gmail), VS Code, Slack/Teams, Windows Terminal:

1. Selecionar um trecho e pressionar o atalho global (Ctrl+Shift+Space): o trecho é substituído pelo resultado (até a fase 4, o atalho global executa a primeira extensão ativa).
2. Ctrl+Z no app desfaz a substituição.
3. Colar (Ctrl+V): sai "CLIP", a área de transferência do usuário voltou intacta. Repetir com uma imagem copiada antes.
4. Sem seleção: nada acontece e a área de transferência continua igual.
5. Atalho próprio de uma extensão: executa direto aquela extensão.
6. Durante o processamento, clicar em outra janela: nada é colado nela; o resultado fica na área de transferência (provisório até a fase 4).
7. Bandeja → Pausar improve: o atalho passa a chegar ao app normalmente; Retomar volta a funcionar.
8. Com a janela de configurações em foco, o atalho não dispara (dá para gravar o próprio atalho no campo).
9. Rodar `improve.exe --trigger` com outro app em foco e um trecho selecionado: mesmo efeito do atalho. Abrir `improve.exe` de novo: abre as configurações da instância que já roda.
10. Geral → Iniciar com o sistema: aparece/some o valor `improve` em `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

Windows Terminal: Ctrl+C sem seleção envia interrupção ao processo; com seleção, copia. Testar sempre com seleção.

## Idiomas e variáveis do prompt

1. Geral → Idioma → English: a janela reabre em inglês na mesma página; o menu da bandeja muda para "Open settings / Pause improve / Quit". Repetir com Español e Português (Brasil).
2. "Seguir o sistema" com o Windows em inglês ou em outro idioma não suportado: interface em inglês.
3. Primeira execução (pasta de configuração vazia) com idioma inglês: as extensões de exemplo vêm em inglês.
4. Mensagens de erro do provedor (chave inválida) aparecem no idioma escolhido.
5. Editor de extensão: clicar em `{{selection}}`, `{{app}}` e `{{clipboard}}` insere a variável no cursor; com um trecho selecionado no prompt, substitui o trecho.

## Fase 4 — Popup e painel

Para medir: `$env:IMPROVE_LOG = "1"` antes de abrir o app; o tempo "Popup visível em N ms" fica em `improve.log` na pasta de configuração.

1. Selecionar texto e pressionar o atalho global: o popup aparece junto ao cursor (12 px abaixo/à direita), com a primeira extensão selecionada, em menos de 150 ms.
2. Perto da borda direita/inferior da tela, o popup vira para caber; com dois monitores e escalas diferentes, aparece no monitor do cursor.
3. Só pelo teclado: ↑/↓ mudam a seleção, digitar filtra, 1–9 executa direto, ↵ executa, Esc fecha.
4. Extensão "Substituir": aparece "Processando… Esc cancela"; Esc cancela sem mexer no texto; sem Esc, o texto é substituído e o foco volta ao app de origem.
5. Extensão "Sugerir": o painel mostra o texto chegando com o cursor limão; ↵ aplica; Copiar copia e fecha; "Refinar" + ↵ ajusta; "Tentar de novo" gera outra vez.
6. Extensão "Copiar": aviso "Copiado" e o resultado na área de transferência.
7. Durante "Processando", clicar em outra janela: o resultado aparece no painel com "A janela mudou; use Copiar ou Aplicar.".
8. Chave inválida: painel com o erro em vermelho e o botão "Abrir configurações".
9. Sem extensões ativas: "Nenhuma extensão ainda." com "Criar primeira extensão", que abre o editor.
10. Atalho sem seleção: aviso "Selecione um texto primeiro" junto ao cursor, que some sozinho, sem tirar o foco do app.
11. Clicar fora do popup fecha; durante o streaming do painel, não fecha.
12. Repetir 1, 5 e 8 no tema claro.
