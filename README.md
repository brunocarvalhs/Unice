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
- `Assets/Unice/Scripts/` — código-fonte principal do projeto
  - `Core/` — lógica central do jogo
  - `Net/` — networking e multiplayer
  - `Editor/` — utilitários do editor
- `ProjectSettings/` — configurações do Unity e do projeto
- `Packages/` — dependências do projeto Unity
- `.github/workflows/` — pipelines de CI/CD do GitHub Actions
- `README.md` — visão geral do jogo e do projeto
- `CHANGELOG.md` — histórico de alterações

## Estrutura interna dos scripts

A lógica do jogo está organizada em camadas:

- `Core/` — gameplay, regras, entidades, UI, mapa e sistema principal
- `Net/` — multiplayer e infraestrutura de rede
- `Editor/` — scripts voltados para o editor Unity e setup do projeto

Principais arquivos do núcleo:
- `App.cs`
- `Game.cs`
- `Map.cs`
- `Ui.cs`
- `Ent.cs`
- `WorldView.cs`
- `Net.cs`
- `Util.cs`
- `Snapshot.cs`

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

- manter a compatibilidade com a versão do Unity adotada
- validar gameplay e UI antes de builds de release
- usar o workflow de debug para testes rápidos
- usar o workflow de PR para validar alterações antes do merge
- usar release para empacotar e publicar a versão final

## Observações

Este README foca no projeto como jogo Unity e no contexto técnico geral do repositório. A documentação específica da pipeline fica em [.github/workflows/README.md](.github/workflows/README.md).
