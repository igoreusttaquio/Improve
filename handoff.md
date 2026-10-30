# Handoff

Estado do projeto para quem for continuar. Leia antes o `CLAUDE.md`, o `PLANO.md`, o `design/BRAND.md` e o `docs/decisoes.md`; este arquivo só resume onde o trabalho parou.

## Onde estamos

- **Fases 0 a 4 concluídas** (fundação, Core, configurações, integração Windows, popup e painel). O Windows é a plataforma completa.
- **Fase 7 em parte:** por pedido do usuário, os instaladores são Inno Setup (Windows), `.deb` (Linux) e `.dmg` (macOS arm64 e x64), sem Velopack. A página Sobre já tem versão e licenças.
- **Modo administrador** (Geral → Executar como administrador): tarefa agendada `improve-admin`; ver `docs/decisoes.md`. **Ainda não testado na prática** (criar a tarefa precisa de autorização do usuário).
- **Atualização pelo app** (Sobre → Verificar atualizações), desde a `v0.2.0`; ver `docs/decisoes.md`.
- **Release `v0.1.0` publicada** pelo workflow `.github/workflows/release.yml` (dispara com tag `v*`). CI em `ci.yml`.
- **Landing page** em `docs/index.html` (GitHub Pages a partir de `/docs`); README com prints em `docs/assets/screenshots/`.

## O que falta

1. **Logo:** o usuário não gostou do ícone atual (`docs/assets/improve-app-icon.svg`, gerado por `tools/gen-icons/gen-app-icon.cs`). Foram propostas 4 opções (A–D) numa página privada no claude.ai, mas ele não entendeu a diferença entre elas. Próximo passo: mostrar as opções já aplicadas no app/bandeja, lado a lado, e deixar ele escolher.
2. **Fase 5 (macOS):** permissão de Acessibilidade, foco com `NSRunningApplication`, início com o sistema. O `.dmg` já é gerado, mas as implementações de plataforma do macOS não estão completas. Pergunte ao usuário antes de começar.
3. **Fase 6 (Linux):** X11 com paridade; Wayland em modo reduzido (`improve --trigger`). Pergunte antes.
4. **Fase 7 restante:** verificação de atualização automática em segundo plano, assinatura de código (Windows) e notarização (macOS).

## Como o usuário trabalha

- Comunicação em português do Brasil, respostas curtas; ele acompanha e cobra quando algo demora, então avise o que está fazendo.
- **Commitar cada etapa e nunca incluir `Co-Authored-By` do Claude** nos commits.
- Só gere tag/release quando ele pedir.
- A chave da DeepSeek que ele forneceu só pode ir para `api.deepseek.com`. Nunca registre, commite ou envie chaves para outro lugar; peça para ele rodar comandos com chave quando necessário.

## Armadilhas conhecidas

- **Avalonia 12:** a maioria dos exemplos online é do 11. `RenderTransform` não anima em keyframes (use `TranslateTransform.Y`); `WindowDecorations` substitui `SystemDecorations`.
- **Recursos do app só existem com o controle na árvore:** aplique fontes/recursos em `OnAttachedToLogicalTree`, não no construtor (foi o bug da fonte do `CodeView`).
- **Build travado:** se o usuário estiver com o improve de Debug aberto, `dotnet build`/`dotnet test` falham ao copiar DLLs. Não feche o app dele; use `--artifacts-path` numa pasta temporária.
- **`dotnet test` local** chegou a mostrar "zero testes executados" mesmo com os testes passando ao rodar `tests/Improve.Core.Tests/bin/.../Improve.Core.Tests.exe` direto (124 testes). No CI passa. Não investigado a fundo.
- **Instância única por pasta de configuração:** para testar sem conflitar com o improve do usuário, use `IMPROVE_CONFIG_DIR` apontando para uma pasta própria. `IMPROVE_LOG=1` grava um log só com metadados.
- **Testes E2E no desktop** (atalho global, colar no app de origem) não rodam enquanto o usuário está usando o PC (o Windows bloqueia a troca de foco). Não insista; use o roteiro em `docs/testes-manuais.md`.
- **Textos da interface:** edite as tabelas em `tools/gen-strings/gen-strings.py` e rode o script; não edite os `.resx` à mão. Tokens visuais vêm de `design/tokens.json` via `tools/gen-tokens`.
