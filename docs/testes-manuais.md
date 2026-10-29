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
