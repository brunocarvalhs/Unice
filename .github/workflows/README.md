# GitHub Actions - Pipeline do projeto

Este diretório reúne os workflows de CI/CD do projeto Unity.

## Workflows

### `version.yml`

- dispara em push para `main`, `master` e `develop`
- gera tags no formato `vX.Y.Z`
- pode ser executado manualmente com `workflow_dispatch`

### `debug.yml`

- gera APK de debug para Android
- cria artefato com o APK compilado
- útil para testes e download fácil em dispositivos

### `release.yml`

- gera build de release para Android
- produz AAB e envia para o Google Play
- exige signing keystore e secrets do Google Play

### `pulls.yml`

- roda em pull requests
- executa os testes do Unity em EditMode (`Assets/Unice/Scripts/Domain/Tests` e `Core/Tests`)
- publica artefatos de resultado e coverage (a meta é ≥ 85% no assembly `Unice.Domain`, ver `docs/ARCHITECTURE_AUDIT.md`)

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
