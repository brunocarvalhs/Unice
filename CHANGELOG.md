# Changelog

Todas as alterações relevantes do projeto serão documentadas neste arquivo.

## [Unreleased]

### Adicionado
- Google Analytics (GA4) e Crashlytics via Firebase: eventos de partida, sala e ações do jogador; erros tratados enviados como não-fatais (catálogo em `docs/ANALYTICS.md`).
- Configuração do Claude Code para o time: plugin oficial da Unity habilitado em `.claude/settings.json` e skills do projeto em `.claude/skills/` (`unity-code`, `unice-test-design`, `unice-run-tests`).
- Testes Edit Mode da regra de eleição de host (`HostElectionTests`) e do passo do host (`MatchHostTests`).

### Alterado
- Código reorganizado para manutenção: nomes descritivos em inglês, comentários em todas as classes, uma responsabilidade por classe e pastas por assunto (ver `SPEC.md` → Code Style e Project Structure, e `docs/ARCHITECTURE_AUDIT.md`).
- A IA dos bots, a busca de caminho e a parte do host saíram de `Game`/`Map` para classes próprias (`Bots/`, `Navigation`, `MatchHost`).
- **Formato do snapshot de rede na versão 5:** aparelhos com builds anteriores não jogam na mesma sala que este.

### Segurança e Google Play
- Release vai para a faixa de **teste interno como rascunho** (antes ia direto para produção) e gera AAB de fato (`androidExportType`).
- Ações do GitHub fixadas por SHA em todos os workflows.
- Removidos o ID de publicidade (manifesto próprio), o Unity IAP e o Firebase App Check, que não eram usados.
- Botão "Silenciar"/"Ouvir" para outros jogadores no chat de voz. **Formato da presença na versão 2** (builds anteriores não jogam com este).
- Checklist de privacidade e do Play Console em `docs/SECURITY.md`.

### Corrigido
- Campo do código da sala: agora aceita só letras maiúsculas e números enquanto se digita, e sobe acima do teclado do celular para o texto ficar visível.

### CI/CD e documentação
- Adicionado fluxo de versionamento com tags automáticas.
- Adicionado pipeline de build de APK de debug para Android.
- Adicionado pipeline de release para Google Play.
- Adicionado workflow de validação para pull requests.
- Documentação inicial do projeto e da estrutura de CI/CD.
- Documentação atualizada para a nova estrutura: `SPEC.md` (regras do jogo, convenções de código, testes), `README.md`, `docs/ARCHITECTURE_AUDIT.md` e o novo `docs/ANALYTICS.md`.

## [1.0.0] - 2026-10-01

- Setup inicial dos workflows Unity para Android no GitHub Actions.
- Ajuste da pipeline de versionamento de tags.
- Criação da documentação base do projeto.
