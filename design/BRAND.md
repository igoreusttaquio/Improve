# improve

**Selecione. Escolha. Melhore.** O improve é um app de desktop que roda na bandeja: você seleciona um texto em qualquer app, aperta o atalho global e escolhe uma extensão, um prompt de IA criado por você. O resultado substitui a seleção ou aparece como sugestão para aplicar ou copiar.

Este design system descreve a cara do improve. Ele é escrito para quem vai implementar o app em **.NET + Avalonia**: os tokens viram `ResourceDictionary` (um por tema) e as classes de `design/referencia-componentes.css` servem de referência visual exata para os estilos Avalonia.

## Princípios

- **Rápido como um atalho.** O popup aparece instantaneamente, já com a primeira opção selecionada, e tudo funciona pelo teclado. Nada de animações longas: no máximo 120 ms de fade/escala ao abrir.
- **Discreto sobre o app dos outros.** O improve aparece por cima de qualquer coisa, então é pequeno, neutro e translúcido. O verde-limão é a única cor, e só aparece onde há ação.
- **O texto do usuário é sagrado.** Nada é substituído sem que a intenção esteja clara. O modo sugerir mostra o resultado antes, e o modo substituir sempre pode ser desfeito com Ctrl/Cmd+Z no app de origem.

## Voz

Português claro, verbos no imperativo e rótulos curtos: "Aplicar", "Copiar", "Tentar de novo", "Melhorar mensagem". Mensagens de erro dizem a causa e o próximo passo ("Chave da Anthropic inválida. Abra Configurações > Provedores."). Sem exclamações, emojis ou "Ops!".

## Cor

O sistema é neutro com **um acento: verde-limão `accent` (#C5F03C)**. O tema escuro é o padrão; o claro segue o sistema operacional.

- `accent` é **só preenchimento** (botão primário, linha selecionada do popup, interruptor ligado), sempre com `on-accent` por cima. No tema claro ele tem contraste baixo com o fundo, por isso nunca é usado como texto ou ícone solto.
- Quando o acento precisa ser texto ou ícone sobre uma superfície, use `accent-ink` (oliva escuro no claro, limão claro no escuro).
- `accent-soft` é o realce de seleção e o fundo de chips e tiles; o texto por cima é `ink`.
- Superfícies em camadas: `surface-100` (janela) < `surface-200` (cartões, campos) < `surface-300` (hover). Superfícies flutuantes usam `surface-glass` sobre o blur do sistema e `shadow-popup`, a única sombra do sistema.
- Bordas: `border-subtle` para divisórias; `border-strong` para contornos de controle.
- Estados (`success`, `warning`, `danger`) aparecem só em texto e ícones pequenos, nunca como grandes áreas.
- Todo texto tem contraste de pelo menos 4,5:1 nos dois temas.

## Tipografia

**Inter** para a interface e **JetBrains Mono** para código e variáveis de prompt (ambas são Google Fonts, licença OFL: empacote os arquivos dentro do app). Se não estiverem disponíveis, use as fontes do sistema da pilha (`Segoe UI Variable`, `SF Pro`). A escala é pequena e densa, como uma ferramenta: `body` 14px, `label` 13px, `caption` 12px, `title` 17px; `display` 32px só para o wordmark e o onboarding.

## Ícones

- Ícones da interface e das extensões: **Lucide**, 16px (18px nos tiles), stroke 1.75, cor herdada do texto. O usuário escolhe o ícone de cada extensão por um seletor com busca entre os nomes do Lucide. Guarde o nome (`wand-sparkles`) no JSON da extensão, não o SVG.
- No Avalonia, renderize o Lucide como `PathIcon`/`Geometry` a partir dos paths SVG oficiais (stroke, não fill: use `Path` com `Stroke`, `StrokeThickness=1.75`, `StrokeLineCap=Round`, `StrokeJoin=Round` em uma `Viewbox` 24×24).
- Ícone do app e glifos da bandeja: pasta `design/icons/`. A marca é um trecho de texto selecionado (realce limão com as duas alças de seleção) com uma faísca vazada no centro: texto selecionado, melhorado pela IA. Não redesenhe a marca: use os SVGs.

## Espaço e forma

Grade de 4px (`space-1` … `space-8`). Cantos arredondados mas firmes: `radius-md` (10px) para botões e linhas, `radius-lg` para cartões e `radius-popup` (16px) para as superfícies flutuantes. Chips usam `radius-pill`.

## Componentes

- **ActionPopup**: a lista de extensões que abre no cursor.
- **SuggestionPanel**: o resultado em streaming, com Aplicar / Copiar / Refinar / Tentar de novo.
- **Button**: primário, secundário, fantasma e perigo.
- **ExtensionCard**: a linha de extensão na janela de configurações.

A referência visual exata está em `design/referencia-visual.png` e `design/referencia-componentes.css`.

## Mapeamento para Avalonia

- Cada token de cor vira `SolidColorBrush` com a mesma chave (`surface-100` → `Surface100Brush`) em dois `ResourceDictionary` dentro de `ThemeDictionaries` (`Dark`, `Light`), seguindo `RequestedThemeVariant = Default`.
- Espaços e raios viram `Thickness`/`CornerRadius` em recursos globais; estilos de texto viram `Style Selector="TextBlock.label"` etc.
- Janelas flutuantes: `SystemDecorations="None"`, `Topmost="True"`, `ShowInTaskbar="False"`, `Background="Transparent"`, `TransparencyLevelHint="AcrylicBlur, Blur, Transparent"`, com um `Border` interno levando `surface-glass`, `radius-popup` e `shadow-popup` (`BoxShadow`).
