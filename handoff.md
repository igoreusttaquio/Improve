# Handoff

Estado do projeto para quem for continuar. Leia antes o `CLAUDE.md`, o `PLANO.md`, o `design/BRAND.md` e o `docs/decisoes.md`; este arquivo só resume onde o trabalho parou.

## Onde estamos

- **Fases 0 a 4 concluídas** (fundação, Core, configurações, integração Windows, popup e painel). O Windows é a plataforma completa.
- **Fase 7 em parte:** por pedido do usuário, os instaladores são Inno Setup (Windows), `.deb` (Linux) e `.dmg` (macOS arm64 e x64), sem Velopack. A página Sobre tem versão, licenças e o botão de atualização.
- **Releases publicadas** pelo workflow `.github/workflows/release.yml` (dispara com tag `v*`; CI em `ci.yml`):
  - `v0.1.0`: primeira versão.
  - `v0.2.0`: atualização pelo app (Sobre → Verificar atualizações) e correção da lista de licenças.
  - `v0.3.0`: modo administrador (Geral → Executar como administrador).
- **Licença MIT** (`LICENSE`), adotada para a inscrição na SignPath Foundation.
- **Landing page** em `docs/index.html` (GitHub Pages a partir de `/docs`); README com prints em `docs/assets/screenshots/`.

## Em andamento

1. **Teste do modo administrador (v0.3.0):** o usuário vai instalar e testar. Não foi testado na prática pelo agente: criar a tarefa agendada `improve-admin` com privilégio máximo foi bloqueado pelo classificador de segurança (persistência), e o terminal do agente roda elevado, o que mascara o problema. Roteiro: ligar a opção → confirmar o UAC → o improve reabre elevado com as configurações abertas → atalho funciona no VS, VS Code e Confluir → reabrir pelo menu Iniciar não pede confirmação. Detalhes de implementação em `docs/decisoes.md`.
2. **Assinatura de código pela SignPath Foundation (grátis para open source):** o usuário enviou a inscrição e aguarda aprovação (manual, dias a semanas). Quando aprovarem, ele encaminha as instruções; então:
   - integrar a assinatura no `release.yml` (instalador e executáveis do Windows), com os segredos que a SignPath fornecer;
   - adicionar a menção exigida na página de download (README, landing page e notas da release): "Code signing provided by SignPath.io, certificate by SignPath Foundation";
   - trocar o modo administrador por `uiAccess=true` no manifesto, que exige executável assinado e instalação em pasta protegida: passar o Inno Setup para instalação por máquina em `Program Files` (`PrivilegesRequired=admin`), migrar as instalações por usuário e remover o modo administrador (apagar a tarefa `improve-admin` na migração).

## O que falta

1. **Logo:** o usuário não gostou do ícone atual (`docs/assets/improve-app-icon.svg`, gerado por `tools/gen-icons/gen-app-icon.cs`). Foram propostas 4 opções (A–D) numa página privada no claude.ai, mas ele não entendeu a diferença entre elas. Próximo passo: mostrar as opções já aplicadas no app/bandeja, lado a lado, e deixar ele escolher.
2. **Fase 5 (macOS):** permissão de Acessibilidade, foco com `NSRunningApplication`, início com o sistema. O `.dmg` já é gerado, mas as implementações de plataforma do macOS não estão completas. Pergunte ao usuário antes de começar.
3. **Fase 6 (Linux):** X11 com paridade; Wayland em modo reduzido (`improve --trigger`). Pergunte antes.
4. **Fase 7 restante:** verificação de atualização em segundo plano e notarização (macOS). A assinatura do Windows está no item "Em andamento".

## Como o usuário trabalha

- Comunicação em português do Brasil, respostas curtas; ele acompanha e cobra quando algo demora, então avise o que está fazendo.
- **Commitar cada etapa e nunca incluir `Co-Authored-By` do Claude** nos commits.
- Só gere tag/release quando ele pedir.
- A chave da DeepSeek que ele forneceu só pode ir para `api.deepseek.com`. Nunca registre, commite ou envie chaves para outro lugar; peça para ele rodar comandos com chave quando necessário.
- O certificado e-CNPJ A1 dele não serve para assinar código (não tem o uso "Code Signing").

## Armadilhas conhecidas

- **Avalonia 12:** a maioria dos exemplos online é do 11. `RenderTransform` não anima em keyframes (use `TranslateTransform.Y`); `WindowDecorations` substitui `SystemDecorations`.
- **Recursos do app só existem com o controle na árvore:** aplique fontes/recursos em `OnAttachedToLogicalTree`, não no construtor (foi o bug da fonte do `CodeView`).
- **Apps elevados:** no PC do usuário, Visual Studio, VS Code, SSMS e Confluir rodam como administrador. Um improve comum não recebe o atalho nem copia a seleção neles (UIPI). No debug pelo Visual Studio funciona porque o improve herda a elevação; teste sempre pelo instalador.
- **Limite da API do GitHub:** 60 consultas por hora por IP sem autenticação, e a rede do usuário esgota isso. O atualizador cai para o redirecionamento de `github.com/.../releases/latest`. Para conferir uma release sem a API, faça `HEAD` em `releases/download/<tag>/<arquivo>`. O `gh` não está instalado.
- **Build travado:** se o usuário estiver com o improve de Debug aberto, `dotnet build`/`dotnet test` falham ao copiar DLLs. Não feche o app dele; use `--artifacts-path` numa pasta temporária.
- **`dotnet test` local** mostra "zero testes executados" mesmo com os testes passando ao rodar o executável de testes direto (`<artifacts>/bin/Improve.Core.Tests/debug/Improve.Core.Tests.exe`, 134 testes). No CI passa. Não investigado a fundo.
- **Instância única por pasta de configuração:** para testar sem conflitar com o improve do usuário, use `IMPROVE_CONFIG_DIR` apontando para uma pasta própria. `IMPROVE_LOG=1` grava um log só com metadados. A tarefa `improve-admin` ignora `IMPROVE_CONFIG_DIR` (sempre usa a pasta padrão).
- **Testes E2E no desktop** (atalho global, colar no app de origem) não rodam enquanto o usuário está usando o PC (o Windows bloqueia a troca de foco). Não insista; use o roteiro em `docs/testes-manuais.md`.
- **Textos da interface:** edite as tabelas em `tools/gen-strings/gen-strings.py` e rode o script; não edite os `.resx` à mão. Tokens visuais vêm de `design/tokens.json` via `tools/gen-tokens`.
