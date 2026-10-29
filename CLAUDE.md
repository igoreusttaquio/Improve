# improve

App de desktop (.NET 10 + Avalonia 12) que aplica prompts de IA ao texto selecionado em qualquer app, via atalho global.

## Antes de qualquer tarefa

1. Leia `PLANO.md` inteiro: decisões fechadas, arquitetura, fluxo e fases com critérios de aceite.
2. Leia `design/BRAND.md`: regras visuais. Os valores exatos estão em `design/tokens.json`.
3. Veja em `docs/decisoes.md` (quando existir) o que já foi decidido durante a implementação.

## Design system

- A pasta `design/` é a fonte de verdade do visual para o código. Não invente cores, tamanhos ou raios: use os tokens gerados a partir de `design/tokens.json`.
- A página do design system no claude.ai é privada; só sincronize a partir dela quando o usuário pedir, seguindo a seção 3.1 do `PLANO.md`.
- Referência visual dos componentes: `design/referencia-visual.png` e `design/referencia-componentes.css` (especificação, não código para copiar).

## Regras

- Avalonia 12: confira a documentação atual e as breaking changes antes de usar exemplos, porque a maioria dos exemplos online é do Avalonia 11.
- `Improve.Core` não referencia Avalonia nem APIs de SO.
- Nunca registre em log chaves de API, texto selecionado ou resultado da IA.
- Não fixe IDs de modelo no código.
- Trabalhe uma fase por vez, com commits pequenos, e rode `dotnet test` antes de cada commit.
- Decisão não coberta pelo plano: escolha a opção mais simples, registre em `docs/decisoes.md` e siga.
