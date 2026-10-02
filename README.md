# Unice

Unice é o projeto de um jogo Unity com foco em experiência mobile, lógica de gameplay, UI e build para Android.

## Visão geral

Este repositório reúne:
- a lógica principal do jogo em C#
- a estrutura de assets e cenas do Unity
- a configuração do editor e build do projeto
- a automação de CI/CD para gerar APKs e validar pull requests

O projeto está organizado em módulos e camadas para separar:
- gameplay e lógica central
- networking/multiplayer
- utilitários do editor
- integração com workflows de build e release

## Requisitos para desenvolvimento

- Unity 6000.6.3f1
- Android SDK configurado
- Git instalado
- Visual Studio ou VS Code para edição de scripts

## Estrutura principal do repositório

- `Assets/` — conteúdo do jogo, scripts, cenas, recursos e assets
- `Assets/Unice/Scripts/` — código-fonte do jogo (detalhes abaixo)
- `ProjectSettings/` — configurações do Unity e do projeto
- `Packages/` — dependências do projeto Unity
- `.github/workflows/` — pipelines de CI/CD do GitHub Actions
- `docs/` — arquitetura (`ARCHITECTURE_AUDIT.md`) e analytics (`ANALYTICS.md`)
- `SPEC.md` — especificação do projeto: objetivo, regras, estrutura, convenções de código, testes e limites
- `CHANGELOG.md` — histórico de alterações

## Estrutura interna dos scripts

O código é dividido em três assemblies, do mais puro para o mais ligado ao Unity:

- `Domain/` (`Unice.Domain`, sem nenhuma dependência do Unity) — regras do jogo, testáveis em Edit Mode
  - `Match/` — a partida: `Game` (regras), `MatchHost` (o que só o host faz), `Character`, `Rules`…
  - `Match/Bots/` — IA dos bots (`HiderBot`, `UniceBot`)
  - `Maps/` — mapas, colisão, linha de visão (`Map`) e busca de caminho (`Navigation`)
  - `Online/` — o que trafega na rede (`Presence`, `PresenceCodec`) e quem é o host (`HostElection`)
  - `Common/` — utilitários (`Geometry`, `GameRandom`, `TextFormat`)
- `Core/` (`Unice.Core`) — a parte Unity: `App` (loop principal), jogador, sala online, mundo, áudio, UI, idiomas e analytics
- `Net/` (`Unice.Net`) — multiplayer (Netcode for GameObjects + Unity Multiplayer Services) e voz (Vivox)
- `Editor/` — utilitários do editor (menu **Unice > Configurar projeto**)

Pontos de entrada para quem chega no código:
- `Core/App/App.cs` — cria todas as peças e roda o loop (passo fixo 20×/s + render a cada frame)
- `Domain/Match/Game.cs` — as regras da partida
- `Core/Ui/Ui.cs` — troca de telas; cada tela está em `Core/Ui/Screens` e `Core/Ui/InGame`

A estrutura completa, com o papel de cada pasta, está em `SPEC.md` → Project Structure.

## Testes

- Testes Edit Mode em `Assets/Unice/Scripts/Domain/Tests` e `Assets/Unice/Scripts/Core/Tests`.
- No Editor: **Window > General > Test Runner > EditMode > Run All**.
- Pela linha de comando:
  `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml`
- Também rodam automaticamente em todo pull request (`pulls.yml`), com relatório de cobertura.

## Claude Code (assistente de IA)

O repositório traz configuração para o [Claude Code](https://claude.com/claude-code), compartilhada pelo time:

- `.claude/settings.json` habilita o **plugin oficial da Unity** (`unity@claude-plugins-official`): skills da Unity para
  UI Toolkit, Vivox, Multiplayer Services, sprites, URP, build, localização e outros. Ao abrir o projeto, o Claude Code
  pede para instalar.
- `.claude/skills/` — skills deste projeto:
  - `unity-code` — convenções de código do Unice (resumo do `SPEC.md`) e APIs modernas do Unity 6;
  - `unice-test-design` — como desenhar e escrever testes aqui (o que vai em Edit Mode, testes de caracterização, o que é manual);
  - `unice-run-tests` — como compilar e rodar os testes pela linha de comando.
- `.claude/agents/unity-clean-architect.md` — agente para revisões de arquitetura.

Partes das skills `unity-code` e `unice-test-design` são adaptadas de
[nowsprinting/unity-coding-skills](https://github.com/nowsprinting/unity-coding-skills) (domínio público, Unlicense).

## Como executar localmente

1. Abra o projeto no Unity usando a versão `6000.6.3f1`.
2. Abra a cena principal do jogo.
3. Verifique se as configurações do Android estão corretas no projeto.
4. Execute em editor ou gere um build para Android.

## Build Android

O projeto conta com workflows automatizados para:
- gerar APK de debug
- validar PRs
- criar tags de versão
- publicar release para a Google Play

A documentação detalhada da pipeline está em:

- `.github/workflows/README.md`

## Fluxo de desenvolvimento recomendado

- seguir as convenções de código do `SPEC.md` (nomes descritivos em inglês, uma responsabilidade por classe, comentários)
- rodar os testes Edit Mode antes de abrir o PR
- manter a compatibilidade com a versão do Unity adotada
- validar gameplay e UI antes de builds de release
- usar o workflow de debug para testes rápidos
- usar o workflow de PR para validar alterações antes do merge
- usar release para empacotar e publicar a versão final

## Observações

Este README foca no projeto como jogo Unity e no contexto técnico geral do repositório. A documentação específica da pipeline fica em [.github/workflows/README.md](.github/workflows/README.md).
