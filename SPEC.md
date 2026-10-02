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
  Match/    → Game (regras; .Ai = bots; .Snapshot = estado serializado host→peers), Rules, GameEvent, Ent, Kit
  Maps/     → Map (geometria, linha de visão), MapParts (Room/Door/Furn), definições de cada mapa
  Online/   → portas de rede/voz (INetBackend, IVoiceChat), Presence/PeerInfo, HostElection
  Common/   → Util (U, P)
  Tests/    → testes Edit Mode do domínio
Assets/Unice/Scripts/Core/    → adapters Unity (assembly Unice.Core)
  App/          → App (MonoBehaviour: monta as peças e roda o loop), Perspective (o que o jogador vê), ProfileStore, SystemBars
  Gameplay/     → PlayerControl (input e ações do jogador), EventFeed (eventos → banners/sons/efeitos)
  Online/       → RoomSession (criar/entrar/sair de sala, eleição de host, presença e snapshots)
  World/        → WorldView, FogView, CameraRig, Art
  Audio/        → Sfx (síntese), Soundscape (o que o jogador ouve)
  Localization/ → Loc (lógica) e Loc.Strings (tabela pt/en/es)
  Analytics/    → Metrics (Google Analytics + Crashlytics via Firebase)
  Ui/           → Ui (troca de telas e o que é compartilhado), UiKit/UiPart (base), Doll
    Screens/    → HomeMenu, KidCustomizer, LobbyScreen, ResultScreen, PauseMenu
    InGame/     → Hud, Overlay (mensagens e textos no mundo), Controls (toque e dicas de teclas)
  Tests/        → testes Edit Mode do Core
Assets/Unice/Scripts/Net/     → implementações Unity Services: NgoBackend (Netcode), VivoxVoice, NetPlayer
Assets/Unice/Scripts/Editor/  → utilitários de editor (incl. Net/Editor)
Assets/Unice/Resources/       → UI (USS/uxml), Art, Fonts, config de rede
ProjectSettings/, Packages/   → configuração Unity e dependências
.github/workflows/            → CI/CD (debug, pulls, release, version) — doc em .github/workflows/README.md
```

## Code Style
Exemplo real do estilo adotado (`Game.cs`):
```csharp
public static class Rules
{
    public const float RoundTime = 120, HideTime = 30, ResultTime = 9, GiggleEvery = 10, PassCd = 5, PassR = 60, CatchR = 30, Stun = 2.5f;
    public const int MaxPlayers = 8;
    public static readonly string[] BotNames = { "Bia", "Tito", "Maru", "Zeca", "Nina", "Otto", "Lia", "Caio" };
}
```
Convenções observadas:
- Nomes curtos e densos (`ph`, `ep`, `r`, `tl`, `hb`) — estilo compacto, consistente em todo o Core; não introduzir nomes verbosos isolados.
- Campos/const agrupados numa linha quando relacionados (ex.: `Rules`).
- `sealed class`/`sealed partial class` para tipos que não serão herdados.
- Comentário de bloco (`/// <summary>`) só nos tipos/classes principais explicando o "porquê" (ex.: "host autoritativo, demais mirrorам"), não em cada membro.
- Sem uso de regions, sem over-abstraction (sem interfaces para tipos com uma única implementação, exceto `INetBackend` que já tem 2+ implementações reais: NGO e stub).

## Testing Strategy
**Estado atual: sem testes automatizados** (`com.unity.test-framework` está no manifest mas não há pasta de testes no repo).

- Framework disponível: Unity Test Framework 1.8.0 (Edit Mode / Play Mode), já como dependência.
- Lacuna conhecida: lógica de `Game.cs`/`Rules` é pura (sem dependência de `MonoBehaviour`) e testável em Edit Mode sem precisar rodar a cena — candidata natural a primeiros testes caso o time decida investir.
- Até decisão em contrário, verificação é manual: rodar no Editor, validar fluxo Home→Lobby→Hide→Hunt→Result, e builds Android via workflow `pulls.yml` antes de merge.

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
