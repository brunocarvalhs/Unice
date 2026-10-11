# Spec: Unice (projeto)

## Objective
Jogo mobile multiplayer de "esconde-esconde" (host-autoritativo): até 8 jogadores (humanos e bots) entram numa sala; um deles é sorteado "Unice" (a caçadora) e os outros se escondem, com uma boneca amaldiçoada que dá risadinhas e entrega quem a carrega. Fluxo: Home → Lobby → Hide → Hunt → Result → Final, repetindo por `totalRounds` rodadas. Port 1:1 da versão web — mesmas regras, mesmo ritmo (`Domain/Match/Game.cs`).

### Regras principais
Valores em `Domain/Match/Rules.cs` (o host pode mudar tempo de esconder, de caça e intervalo da risadinha na sala de espera).
- **Rodada:** fase de esconder (30 s, a Unice espera do lado de fora de olhos fechados) e fase de caça (120 s, a Unice entra pela porta da frente).
- **Boneca:** sorteada para um escondido no início da rodada. Na fase de caça, enquanto quem a carrega está escondido, ela dá uma risadinha a cada 10 s e a Unice ouve onde foi. Quem a carrega pode passá-la para outro escondido a até 60 px (espera de 5 s entre passes).
- **Rir de propósito (R):** qualquer escondido pode rir, **com ou sem a boneca** — mas nem sempre: só durante a rodada, nunca a Unice nem quem já foi pego, e com 5 s de espera entre uma risada e outra. A Unice ouve a risada igual à risadinha da boneca (entrega a posição); se quem riu está com a boneca, o contador da próxima risadinha recomeça.
- **Captura:** a Unice pega quem ela encosta (30 px) ou abre o esconderijo onde a pessoa está. Pegou quem tinha a boneca → a Unice vence a rodada (+3). Pegou outra pessoa → ela sai da rodada (Unice +1) e a Unice fica 2,5 s parada.
- **Tempo esgotado:** o time vence — cada escondido +2, quem terminou com a boneca +1.
- **Próxima Unice:** quem foi pego com a boneca; se o tempo acabou, quem foi Unice menos vezes. Com 2+ pessoas na sala, só pessoas são sorteadas (bots não).

Sucesso = paridade de regras com a versão web, build Android estável via CI, e baixa latência percebida no sync de posição/estado entre host e peers.

## Tech Stack
- Unity **6000.6.3f1**, C#, Universal Render Pipeline 17.6.0
- Netcode for GameObjects 2.13.3 + Unity Services Multiplayer 2.3.3 (transporte/relay)
- Firebase 13.17: Analytics (GA4) e Crashlytics integrados via `Core/Analytics/Metrics.cs` (catálogo de eventos em `docs/ANALYTICS.md`); App Check instalado, não usado no código
- Vivox (Unity Services) para voz por proximidade — opcional, o jogo funciona sem
- Google Play Games plugin for Unity 2.3.0 (`Assets/GooglePlayGames`): login automático no Android, com login anônimo de reserva
- Unity Input System 1.20.0
- Plataforma alvo: Android (prioridade); demais plataformas não configuradas no CI hoje

## Commands
Não há CLI local de build/test (projeto Unity puro, sem Node/Make). Fluxos reais:

- **Build debug (CI, PR para develop/main/master):** `.github/workflows/debug.yml` → `game-ci/unity-builder@v4`, `targetPlatform: Android`, `customParameters: -quit -batchmode -logFile ...`
- **Testes no CI:** `.github/workflows/tests.yml` (chamado por `debug.yml` e `release.yml` antes do build)
- **Release (Google Play):** `.github/workflows/release.yml`
- **Versionamento (tags):** `.github/workflows/version.yml`
- **Local:** abrir o projeto no Unity Editor `6000.6.3f1`, abrir a cena principal, Play ou `File > Build Settings` para Android.
- **Testes:** `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml` (ou Test Runner no Editor). No CI, rodam no `tests.yml` com cobertura — ver Testing Strategy.

## Project Structure
```
Assets/Unice/Scripts/Domain/  → regras puras, sem UnityEngine (assembly Unice.Domain, testado em Edit Mode)
  Match/    → Game (regras e partida; Game.Snapshot = estado enviado host→convidados), MatchHost (passo do host,
              comandos dos outros jogadores, assumir como host), Character, Rules, Kit,
              GameEvent, RoundResult, DollState, Phase, PlayerAction
    Bots/   → HiderBot, UniceBot (IA dos bots), BotBrain (memória do bot), BotGoal, BotPath (andar pelo caminho)
  Maps/     → Map (geometria, colisão, linha de visão), Navigation (busca de caminho), MapParts, definições dos mapas
  Online/   → portas de rede/voz (INetBackend, IVoiceChat), Presence/PeerInfo, PresenceCodec, HostElection
  Common/   → Point, Geometry, GameRandom, TextFormat
  Tests/    → testes Edit Mode do domínio
Assets/Unice/Scripts/Core/    → adapters Unity (assembly Unice.Core)
  App/          → App (monta as peças e roda o loop), Perspective (o que o jogador vê), ProfileStore, SystemBars
  Gameplay/     → PlayerControl (input e ações do jogador), EventFeed (eventos → banners/sons/efeitos)
  Online/       → RoomSession (criar/entrar/sair, lobby), RoomSync (eleição de host, snapshots, presença)
  World/        → WorldView, MapScenery, CharacterView, KidSprites, RippleEffects, FogView, CameraRig, Art, ArtManifest
  Audio/        → Sfx (tocar), ToneSynth (sintetizar), Soundscape (sons ouvidos), Heartbeat
  Localization/ → Localization (lógica) e Localization.Strings (tabela pt/en/es)
  Analytics/    → Metrics (Google Analytics + Crashlytics via Firebase)
  Ui/           → Ui (troca de telas), UiKit/UiPart (base), SettingToggles, KeyboardLift, CharacterPortrait
    Screens/    → HomeMenu, KidCustomizer, KitOptionPicker, LobbyScreen, MatchOptions, ResultScreen, PauseMenu
    InGame/     → Hud, MatchCard, RoleCard, PlayerListPanel, MiniMap, Messages, WorldLabels, SoundCues,
                  ScreenEffects, TouchControls, Joystick, ActionIcons, ActionHints
  Tests/        → testes Edit Mode do Core
Assets/Unice/Scripts/Net/     → implementações Unity Services: NgoBackend (Netcode), VivoxVoice, NetPlayer,
                                PlayerSignIn (login único: Google Play Games ou anônimo — docs/PLAY_GAMES.md)
Assets/Unice/Scripts/Editor/  → utilitários de editor (incl. Net/Editor)
Assets/Unice/Resources/       → UI (USS/uxml), Art, Fonts, config de rede
ProjectSettings/, Packages/   → configuração Unity e dependências
.github/workflows/            → CI/CD (debug, pulls, release, version) — doc em .github/workflows/README.md
```

## Code Style
Exemplo real do estilo adotado (`Domain/Match/Rules.cs`):
```csharp
/// <summary>The tuning numbers of the game: times in seconds, distances and speeds in house pixels. Same as the web version.</summary>
public static class Rules
{
    /// <summary>The doll giggles this often while its holder is hidden.</summary>
    public const float GiggleInterval = 10;
    /// <summary>The Unice catches whoever it gets this close to.</summary>
    public const float CatchRange = 30;
}
```
Convenções (o objetivo é que qualquer engenheiro entenda o código sem ajuda):
- **Nomes descritivos em inglês** para classes, métodos, campos, parâmetros e variáveis (`timeLeft`, `hidingSpot`, `PassTarget`). Nada de abreviações (`tl`, `ph`, `r`, `e`); exceções aceitas: `x`/`y`, `dt`, `i`/`j` em laços, `id`, `me` (o personagem deste aparelho) e `ax, ay, bx, by` para pontos A e B.
- **Uma responsabilidade por classe**, em pastas por assunto (ver Project Structure). Quando uma classe começa a fazer duas coisas, ela é dividida — ex.: `Hud` só monta `MatchCard`, `RoleCard`, `PlayerListPanel`.
- **Comentários `/// <summary>`** em toda classe e em todo membro cujo propósito não seja óbvio pelo nome; explicam o *porquê* e as unidades (segundos, house pixels).
- **Números mágicos viram constantes nomeadas** (`const float StunTime = 2.5f`), com comentário quando a unidade ou o motivo não é óbvio.
- **Strings de estado viram enums** (`Phase`, `GameEventType`, `HiderMode`, `BannerStyle`, `RippleKind`, `UiScreen`).
- `sealed class` para tipos que não serão herdados; `partial` só quando a divisão em arquivos não pode virar classes (ex.: `Game` e `Game.Snapshot`).
- Sem regions, sem interfaces com uma implementação só (exceto as portas `INetBackend`/`IVoiceChat`, que existem para o jogo compilar sem os pacotes de rede).
- **Nunca renomear** os campos de `ArtManifest.cs` (o `JsonUtility` casa pelo nome com `manifest.json`), as chaves de `PlayerPrefs` (`unice.name`, `unice.kit`, `unice.lang`) nem as chaves de `Localization.Strings`.
- Mudou o formato de bytes do snapshot ou da presença? Suba `Game.SnapshotVersion` / `PresenceCodec.Version`.

## Testing Strategy
- Testes Edit Mode (NUnit, Unity Test Framework) em `Domain/Tests` (regras, IA dos bots, snapshot, eleição de host, mapas) e `Core/Tests` (perfil).
- Rodar: `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml` (ou o Test Runner no Editor).
- Os testes do `Game` são de caracterização: fixam o comportamento atual (paridade com a versão web). Se um falhar depois de uma mudança em `Game.cs`, a regra mudou — confirme se era a intenção.
- UI, rede e áudio não têm teste automatizado: verificar manualmente (Home→Lobby→Hide→Hunt→Result→Final; sala online com dois aparelhos).

## Boundaries
- **Sempre fazer:** manter paridade com a versão web ao tocar em `Game.cs`/`Rules`/`Bots`; validar build Android (CI) antes de merge; seguir as convenções de Code Style (nomes descritivos, uma responsabilidade por classe, comentários); rodar os testes Edit Mode; atualizar `docs/ANALYTICS.md` ao mudar eventos e `docs/ARCHITECTURE_AUDIT.md`/Project Structure ao mudar a estrutura.
- **Sempre fazer (segurança):** manter as ações do GitHub fixadas por SHA; nunca enviar para a faixa de produção pelo CI; conferir as permissões do AAB depois de mudar pacotes (`docs/SECURITY.md`).
- **Perguntar antes:** mudar versão do Unity ou de pacotes em `Packages/manifest.json`; alterar `ProjectSettings/` (ex.: `GvhProjectSettings.xml`, `ProjectSettings.asset`) fora do escopo pedido; mudar workflows de CI/CD; adicionar nova plataforma de build; mudar o backend de rede (`NgoBackend`/Unity Services Multiplayer).
- **Nunca fazer:** commitar segredos/chaves do Firebase ou assinatura Android; editar `Library/`, `Temp/`, pastas geradas pelo Unity; remover testes sem aprovação; quebrar o "straight port" das regras sem alinhar com a versão web primeiro; mudar o formato do snapshot/presença sem subir a versão (`Game.SnapshotVersion` / `PresenceCodec.Version`).

## Success Criteria
- Build Android gerado com sucesso pelos workflows `debug.yml`/`release.yml`.
- Fluxo completo de partida (Home→Lobby→Hide→Hunt→Result→Final) funcional em host e peers via `NgoBackend`.
- Analytics e Crashlytics enviando dados de builds em aparelho, sem bloquear o jogo se o Firebase falhar.
- Testes Edit Mode passando e cobertura do `Unice.Domain` ≥ 85%.
- Nenhuma regressão de paridade de regras em relação à versão web.

## Open Questions
1. Há plano para iOS, ou Android continua sendo a única plataforma alvo?
2. O Firebase App Check (instalado) deve ser ativado?
3. Vale investir num `INetBackend` falso para testar a troca de host em Edit Mode (hoje só manual)? Ver `docs/ARCHITECTURE_AUDIT.md`.
