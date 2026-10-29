# Testes manuais

Roteiro para o que não é coberto por `dotnet test`. Atualizado a cada fase.

## Fase 0 — Fundação

Rodar: `dotnet run --project src/Improve.App`

1. O app não abre janela; aparece o glifo do improve na bandeja (claro em barra escura, escuro em barra clara).
2. Menu da bandeja → "Abrir configurações": abre a janela com o tile do ícone `wand-sparkles`, o wordmark "improve" em Inter e o slogan.
3. Trocar o tema do SO (Windows: Configurações > Personalização > Cores > Modo do app) com a janela aberta: fundo, texto e tile alternam entre os tokens escuros e claros sem reiniciar.
4. Trocar o modo do Windows (barra de tarefas): o glifo da bandeja troca de claro para escuro.
5. Menu da bandeja → "Sair": o processo termina.
