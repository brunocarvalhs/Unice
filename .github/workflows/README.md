# GitHub Actions - Pipeline do projeto

Este diretório reúne os workflows de CI/CD do projeto Unity.

## Workflows

Fluxo: `validate` → `Edit Mode tests` → `build` (→ `publish` no release). Cada etapa só roda se a anterior passar.

### `version.yml`

- dispara em push para `main`, `master` e `develop`
- gera tags no formato `vX.Y.Z`
- pode ser executado manualmente com `workflow_dispatch`

### `build-targets.yml`

- reutilizável (`workflow_call`): lê `.github/build-targets.yml` (`targets.<grupo>.<plataforma>: true|false`, grupos `mobile`, `web`, `desktop`, `console`) e expõe quais alvos estão habilitados
- chamado por `debug.yml` e `release.yml`; alvo desabilitado = jobs de build/publish dele pulados
- alvos: `android`, `webgl`, `ios`, `windows`, `macos`, `linux`, `playstation`, `xbox`, `nintendo-switch` — por enquanto só `android` e `webgl` têm jobs; habilitar outro faz a pipeline falhar até os jobs `build-<alvo>` → `publish-<alvo>` existirem

### `tests.yml`

- `validate` (segundos, sem Unity): `actionlint` nos workflows e checagem de `.meta` faltando/órfão em `Assets/`
- `Edit Mode tests`: testes Unity em EditMode (`Assets/Unice/Scripts/Domain/Tests` e `Core/Tests`)
- reutilizável (`workflow_call`): chamado por `debug.yml` e `release.yml`; também pode ser rodado sozinho pela aba Actions
- publica o check **Edit Mode test results** no commit/PR, mais os artefatos `unity-test-results` e `unity-coverage` (relatório de `Unice.Domain` e `Unice.Core`)
- **gate de cobertura:** o job falha se a cobertura de linhas do `Unice.Domain` ficar abaixo de 85% (lida de `CodeCoverage/Report/Summary.json`); `Unice.Core` aparece no relatório mas não entra no gate

### `web.yml`

- versão web: build WebGL → publica no GitHub Pages (`build-webgl` → `publish-webgl`)
- chamada pelo `release.yml` depois dos testes, **em paralelo** ao AAB, quando `targets.web.webgl` está `true`; também roda sozinha pela aba Actions
- mesmo commit do app = mesmo protocolo de rede: navegador e celular jogam na mesma sala (o Relay da Unity liga WebSocket com UDP)
- exige uma vez: **Settings > Pages > Source = GitHub Actions**
- atenção: são duas execuções da Unity ao mesmo tempo; na licença Unity Personal isso pode bater no limite de ativação (exit code 137)

### `debug.yml`

- roda em pull requests para `develop`/`main`/`master` (e manualmente): `tests` → build — não roda em push, para não duplicar a run do PR
- gera APK de debug para Android (se `targets.mobile.android` estiver habilitado em `.github/build-targets.yml`)
- cria artefato com o APK compilado
- útil para testes e download fácil em dispositivos

### `release.yml`

- roda em push para `master`/`main` (e manualmente): `tests` → build do AAB → `publish` (envio ao Google Play em job separado, dá para rodar de novo sem rebuild)
- gera build de release para Android (AAB, `versionCode` pela versão semântica do game-ci)
- envia para a faixa de **teste interno** do Google Play como **rascunho** — a promoção para teste fechado/produção é feita à mão no Play Console
- o primeiro envio de um app novo precisa ser feito à mão no Console (limitação da API do Play)
- exige signing keystore e secrets do Google Play
- só builda/publica as plataformas ligadas em `.github/build-targets.yml` (ver `build-targets.yml`)
- com `targets.web.webgl: true`, dispara a pipeline `web.yml` em paralelo ao build do Android (ver abaixo)

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
