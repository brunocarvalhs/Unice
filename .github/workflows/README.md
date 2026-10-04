# GitHub Actions - Pipeline do projeto

Este diretório reúne os workflows de CI/CD do projeto Unity.

## Workflows

Fluxo: `validate` → `Edit Mode tests` → `build` (→ `publish` no release). Cada etapa só roda se a anterior passar.

### `version.yml`

- dispara em push para `main`, `master` e `develop`
- gera tags no formato `vX.Y.Z`
- pode ser executado manualmente com `workflow_dispatch`

### `config.yml`

- reutilizável (`workflow_call`): lê `.github/pipeline-config.yml` (`platforms.<nome>: true|false`) e expõe quais plataformas estão ligadas
- chamado por `debug.yml` e `release.yml`; plataforma desligada = job de build/publish dela pulado
- por enquanto só `android` tem jobs; ligar `ios`, `pc` ou `console` faz a pipeline falhar até os jobs `build-<plataforma>` → `publish-<plataforma>` existirem

### `tests.yml`

- `validate` (segundos, sem Unity): `actionlint` nos workflows e checagem de `.meta` faltando/órfão em `Assets/`
- `Edit Mode tests`: testes Unity em EditMode (`Assets/Unice/Scripts/Domain/Tests` e `Core/Tests`)
- reutilizável (`workflow_call`): chamado por `debug.yml` e `release.yml`; também pode ser rodado sozinho pela aba Actions
- publica o check **Edit Mode test results** no commit/PR, mais os artefatos `unity-test-results` e `unity-coverage` (a meta é ≥ 85% no assembly `Unice.Domain`, ver `docs/ARCHITECTURE_AUDIT.md`)

### `debug.yml`

- roda em push para `develop` e em pull requests: `tests` → build
- gera APK de debug para Android (se `platforms.android` estiver ligado em `.github/pipeline-config.yml`)
- cria artefato com o APK compilado
- útil para testes e download fácil em dispositivos

### `release.yml`

- roda em push para `master`/`main` (e manualmente): `tests` → build do AAB → `publish` (envio ao Google Play em job separado, dá para rodar de novo sem rebuild)
- gera build de release para Android (AAB, `versionCode` pela versão semântica do game-ci)
- envia para a faixa de **teste interno** do Google Play como **rascunho** — a promoção para teste fechado/produção é feita à mão no Play Console
- o primeiro envio de um app novo precisa ser feito à mão no Console (limitação da API do Play)
- exige signing keystore e secrets do Google Play
- só builda/publica as plataformas ligadas em `.github/pipeline-config.yml` (ver `config.yml`)

## Segurança

- Todas as ações estão fixadas por SHA de commit (comentário com a versão ao lado). Para atualizar uma ação, troque o SHA pelo da nova versão.
- Mais detalhes em `docs/SECURITY.md`.

## Secrets necessários

Configure estes secrets no repositório do GitHub:

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

## Observações

Este README é específico da pipeline e fica no diretório de workflows do GitHub, conforme a separação pedida para manter o README principal focado no jogo.
