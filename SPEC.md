# Spec: Unice (projeto)

## Objective
Jogo mobile multiplayer de "esconde-esconde" (host-autoritativo): até 8 jogadores entram numa sala, metade se esconde e um deles é sorteado "Unice" (o caçador) na fase de caça. Fluxo: Home → Lobby → Hide → Hunt → Result → Final, repetindo por `rounds` rodadas. Port 1:1 da versão web — mesmas regras, mesmo ritmo (`Game.cs` é comentado como "straight port of the web version so both play the same").

Sucesso = paridade de regras com a versão web, build Android estável via CI, e baixa latência percebida no sync de posição/estado entre host e peers.

## Tech Stack
- Unity **6000.6.3f1**, C#, Universal Render Pipeline 17.6.0
- Netcode for GameObjects 2.13.3 + Unity Services Multiplayer 2.3.3 (transporte/relay)
- Firebase (Crashlytics, Analytics, App Check) — integração em andamento (ver `git status`: dependências Android ainda não commitadas)
- Unity Input System 1.20.0
- Plataforma alvo: Android (prioridade); demais plataformas não configuradas no CI hoje

## Commands
Não há CLI local de build/test (projeto Unity puro, sem Node/Make). Fluxos reais:

- **Build debug (CI, push/PR em develop/main/master):** `.github/workflows/debug.yml` → `game-ci/unity-builder@v4`, `targetPlatform: Android`, `customParameters: -quit -batchmode -logFile ...`
- **Validação de PR:** `.github/workflows/pulls.yml`
- **Release (Google Play):** `.github/workflows/release.yml`
- **Versionamento (tags):** `.github/workflows/version.yml`
- **Local:** abrir o projeto no Unity Editor `6000.6.3f1`, abrir a cena principal, Play ou `File > Build Settings` para Android.
- **Testes:** nenhum automatizado hoje — ver Testing Strategy.

## Project Structure
```
Assets/Unice/Scripts/Domain/  → regras puras, sem UnityEngine (assembly Unice.Domain, testado em Edit Mode)
  Match/    → Game (regras e partida; Game.Snapshot = estado enviado host→convidados), Character, Rules, Kit,
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
Assets/Unice/Scripts/Net/     → implementações Unity Services: NgoBackend (Netcode), VivoxVoice, NetPlayer
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
- **Sempre fazer:** manter paridade com a versão web ao tocar em `Game.cs`/`Rules`; validar build Android (CI) antes de merge; manter convenção de nomes compactos do Core.
- **Perguntar antes:** mudar versão do Unity ou de pacotes em `Packages/manifest.json`; alterar `ProjectSettings/` (ex.: `GvhProjectSettings.xml`, `ProjectSettings.asset`) fora do escopo pedido; mudar workflows de CI/CD; adicionar nova plataforma de build; mudar o backend de rede (`NgoBackend`/Unity Services Multiplayer).
- **Nunca fazer:** commitar segredos/chaves do Firebase ou assinatura Android; editar `Library/`, `Temp/`, pastas geradas pelo Unity; remover testes (quando existirem) sem aprovação; quebrar o "straight port" das regras sem alinhar com a versão web primeiro.

## Success Criteria
- Build Android gerado com sucesso pelos workflows `debug.yml`/`pulls.yml`/`release.yml`.
- Fluxo completo de partida (Home→Lobby→Hide→Hunt→Result→Final) funcional em host e peers via `NgoBackend`.
- Integração Firebase (Crashlytics/Analytics/App Check) ativa sem bloquear build Android.
- Nenhuma regressão de paridade de regras em relação à versão web.

## Open Questions
1. O trabalho não commitado (Firebase Android deps, `app_BackUpThisFolder_ButDontShipItWithYourGame/`) faz parte do escopo deste spec ou é um módulo/spec separado?
2. Vale investir em testes automatizados para `Game.cs`/`Rules` (lógica pura, fácil de testar) agora ou isso é deliberadamente fora de escopo por ora?
3. Há plano para iOS, ou Android continua sendo a única plataforma alvo?
