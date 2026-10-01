# Unice

Unice é um jogo desenvolvido em Unity, com foco em experiência mobile e build para Android.

## Visão geral

Este projeto reúne a lógica do jogo, os assets do Unity e a estrutura de configuração da build Android.

## Requisitos para desenvolvimento

- Unity 6000.6.3f1
- Android SDK configurado
- Git instalado
- Visual Studio / VS Code para edição de scripts

## Estrutura principal

- `Assets/` — conteúdo do jogo, prefabs, scenes, scripts e recursos
- `ProjectSettings/` — configurações do projeto Unity
- `.github/workflows/` — workflows de CI/CD para build, testes e releases

## Como executar localmente

1. Abra o projeto no Unity.
2. Verifique a versão do editor: `6000.6.3f1`.
3. Abra a cena principal do jogo.
4. Faça o build para Android pela interface do Unity ou pela pipeline do GitHub Actions.

## Build Android

A geração da build Android está automatizada em GitHub Actions. A documentação detalhada da pipeline está em:

- `.github/workflows/README.md`

## Desenvolvimento

- mantenha o projeto compatível com a versão do Unity adotada
- valide alterações de gameplay e UI antes de gerar builds de release
- use a pipeline para criar APKs de debug e versões de produção

## Observações

Este README descreve o jogo e o projeto Unity em si. A documentação da pipeline fica no diretório de workflows do GitHub.
