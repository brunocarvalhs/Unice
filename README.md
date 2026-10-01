# Unice

Unice é um projeto Unity para Android, com pipeline de CI/CD configurada para automatizar build, versionamento e validações em pull requests.

## Requisitos

- Unity 6000.6.3f1
- GitHub Actions
- Android SDK configurado no ambiente de build
- Secrets do GitHub para Unity e Play Store

## Fluxo de CI/CD

### Version
Workflow: `.github/workflows/version.yml`

- dispara em push para `main`, `master` e `develop`
- cria uma tag de versão seguindo o padrão `vX.Y.Z`
- pode ser disparado manualmente via `workflow_dispatch`

### Debug
Workflow: `.github/workflows/debug.yml`

- gera APK de debug para Android
- publica o artefato no GitHub Actions
- útil para testes e distribuição interna simples

### Release
Workflow: `.github/workflows/release.yml`

- gera AAB de release
- assina o build com a keystore configurada
- envia o pacote para o Google Play
- pode ser disparado por tag ou manualmente

### Pulls
Workflow: `.github/workflows/pulls.yml`

- executa testes de EditMode do Unity
- publica resultados e artefatos de coverage
- serve como validação obrigatória em PRs

## Secrets necessários

Configure os secrets abaixo no GitHub:

- `UNITY_LICENSE`
- `UNITY_EMAIL`
- `UNITY_PASSWORD`
- `UNITY_SERIAL`
- `ANDROID_KEYSTORE_NAME`
- `ANDROID_KEYSTORE_BASE64`
- `ANDROID_KEYSTORE_PASS`
- `ANDROID_KEYALIAS_NAME`
- `ANDROID_KEYALIAS_PASS`
- `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON`
- `GOOGLE_PLAY_PACKAGE_NAME`

## Estrutura relevante

- `.github/workflows/` — pipelines do GitHub Actions
- `Assets/` — conteúdo do projeto Unity
- `ProjectSettings/` — configurações do projeto

## Como usar

1. Faça o push do projeto para o GitHub.
2. Configure os secrets listados acima.
3. Rode o workflow `Version` para criar tags.
4. Use `Debug` para gerar APK de teste.
5. Use `Release` para publicar versões de produção.

## Observações

Esta documentação acompanha a configuração atual da pipeline e deve ser atualizada sempre que novos workflows ou requisitos de build forem adicionados.
