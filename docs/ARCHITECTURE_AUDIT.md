# Clean Architecture Review Report — Unice

Estado do código em `Assets/Unice/Scripts/` na branch `develop` (revisado em 2026-10-02, depois da reorganização).
A versão anterior deste documento (2026-10-01) era um **plano**; o plano foi executado e este relatório descreve o resultado.
O histórico do que mudou está no fim ("O que foi feito desde a auditoria anterior").

## Summary

- **Overall Assessment: Good.** As regras do jogo vivem num assembly sem Unity (`Unice.Domain`, `noEngineReferences: true`),
  a regra de dependência é verificada pelo compilador, e cada classe tem uma responsabilidade, em pastas por assunto.
- **87 arquivos de produção (~7.500 linhas), 10 arquivos de teste, 123 testes Edit Mode passando.**
  Nenhum arquivo de código passa de ~400 linhas; os maiores são a tabela de textos (`Localization.Strings.cs`, só dados)
  e `Game.cs` (regras, metade é comentário).
- Nomes descritivos em inglês e comentários `/// <summary>` em todo o código (convenção em `SPEC.md` → Code Style).
- **Risco que continua:** UI, áudio e rede (`RoomSession`/`RoomSync`/`NgoBackend`) não têm teste automatizado — só manual.

## Dependency Rule Analysis

| Regra | Como é garantida | Estado |
|---|---|---|
| O domínio não conhece Unity | `Unice.Domain.asmdef` com `noEngineReferences: true` e nenhuma referência | ✅ compilador |
| O domínio não conhece os adapters (`App`, `Ui`, `WorldView`…) | `Unice.Domain` não referencia `Unice.Core` | ✅ compilador |
| Adapters dependem do domínio, nunca o contrário | `Unice.Core` → `Unice.Domain`; `Unice.Net` → `Unice.Core` + `Unice.Domain` | ✅ |
| O jogo compila sem os pacotes de rede/voz | Portas `INetBackend`/`IVoiceChat` no domínio; implementações em `Unice.Net`, registradas via `NetRegistry`/`VoiceRegistry` | ✅ |

A antiga violação #1 (`Game.HostStep` chamava `App.HumansNow()` estático) foi resolvida: hoje é um delegate
injetado em `MatchHost.Step(..., Func<List<Game.Human>> humansNow)`, coberto por `MatchHostTests`.

## Layer Structure Review

### Domain (Entities + Use Cases) — `Assets/Unice/Scripts/Domain/` — **Good**

| Pasta | Conteúdo | Responsabilidade |
|---|---|---|
| `Match/` | `Game` (+ `Game.Snapshot`), `MatchHost`, `Character`, `Rules`, `Kit`, `GameEvent`, `RoundResult`, `DollState`, `Phase`, `PlayerAction` | Estado e regras da partida. `Game` = regras (port 1:1 da versão web); `MatchHost` = o que só o host faz (passo fixo, comandos dos outros jogadores, assumir como host); `Game.Snapshot` = formato de bytes host→convidados |
| `Match/Bots/` | `HiderBot`, `UniceBot`, `BotBrain`, `BotGoal`, `BotPath` | IA dos bots, separada das regras; o estado de IA fica em `BotBrain`, não no `Character` |
| `Maps/` | `Map`, `Navigation`, `MapParts`, `MapDefinition`, `MapHouse`/`MapQuintal`/`MapPraca` | Geometria, colisão e linha de visão (`Map`); busca de caminho (`Navigation`); dados de cada mapa |
| `Online/` | `INetBackend`, `IVoiceChat`, `Presence`/`PeerInfo`, `PresenceCodec`, `HostElection` | Portas de rede/voz, o que cada jogador envia, a regra de quem é o host (função pura, testada) |
| `Common/` | `Point`, `Geometry`, `GameRandom`, `TextFormat` | Utilitários pequenos, um assunto por classe |

**Use Cases:** continuam dentro do domínio (métodos públicos de `Game` e `MatchHost`). Uma camada separada de
`XyzUseCase` seria abstração com um consumidor só — não vale enquanto houver um único orquestrador.

### Interface Adapters — `Assets/Unice/Scripts/Core/` (assembly `Unice.Core`) — **Good**

| Pasta | Classes | Responsabilidade |
|---|---|---|
| `App/` | `App`, `Perspective`, `ProfileStore`, `SystemBars` | `App` (MonoBehaviour) só monta as peças e roda o loop (passo fixo 20/s + frame) e expõe a API que a UI usa |
| `Gameplay/` | `PlayerControl`, `EventFeed` | Input e ações deste jogador; eventos da partida → banners/sons/efeitos/analytics |
| `Online/` | `RoomSession`, `RoomSync` | Ciclo de vida da sala (criar/entrar/sair/solo/lobby); sincronização (eleição, snapshots, presença) |
| `World/` | `WorldView`, `MapScenery`, `CharacterView`, `KidSprites`, `RippleEffects`, `FogView`, `CameraRig`, `Art`, `ArtManifest` | Tudo o que é desenhado no mundo |
| `Audio/` | `Sfx`, `ToneSynth`, `Soundscape`, `Heartbeat` | Tocar, sintetizar, o que o jogador ouve, batimento |
| `Localization/` | `Localization`, `Localization.Strings` | Lógica e tabela pt/en/es |
| `Analytics/` | `Metrics` | Google Analytics + Crashlytics (ver `docs/ANALYTICS.md`) |
| `Ui/` | `Ui`, `UiKit`, `UiPart`, `SettingToggles`, `KeyboardLift`, `CharacterPortrait` | Troca de telas e o que várias telas compartilham |
| `Ui/Screens/` | `HomeMenu`, `KidCustomizer`, `KitOptionPicker`, `LobbyScreen`, `MatchOptions`, `ResultScreen`, `PauseMenu` | Uma classe por tela/painel |
| `Ui/InGame/` | `Hud`, `MatchCard`, `RoleCard`, `PlayerListPanel`, `MiniMap`, `Messages`, `WorldLabels`, `SoundCues`, `ScreenEffects`, `TouchControls`, `Joystick`, `ActionIcons`, `ActionHints` | HUD e camadas sobre o jogo, uma responsabilidade cada |

Regra de design mantida desde o plano: as telas falam com o jogo por uma superfície única em `App`
(`EnterRoom`, `UseNearbySpot`, `roomCode`, …), que repassa para `RoomSession`/`PlayerControl`. Isso deixa as telas
independentes de como o resto está dividido.

### Frameworks & Drivers — `Assets/Unice/Scripts/Net/` (assembly `Unice.Net`) — **Good**

`NgoBackend : INetBackend` (Netcode for GameObjects + Unity Multiplayer Services, autoridade distribuída),
`NetPlayer : NetworkBehaviour` (envia presença e snapshots, valida o remetente e o tamanho), `VivoxVoice : IVoiceChat`
(voz por proximidade). Firebase entra pelo `Metrics` (Core). Unity Input System, UI Toolkit e URP 2D completam a camada.

## Test Coverage

- **Atual:** 123 testes Edit Mode, todos passando. `com.unity.testtools.codecoverage` instalado; o workflow `tests.yml`
  gera o relatório de cobertura como artefato do PR.
  - `Domain/Tests`: `GameTests`, `GameAiTests`, `MatchHostTests`, `SnapshotTests`, `HostElectionTests`, `MapTests`,
    `KitTests`, `CommonTests`, `VoiceRuleTests`.
  - `Core/Tests`: `ProfileStoreTests`.
- **Natureza dos testes de `Game`/bots:** de caracterização — fixam o comportamento atual (paridade com a versão web).
  Eles pegaram a reorganização inteira (IA extraída, `GameEvent`/`RoundResult` com campos nomeados, `MatchHost`)
  sem mudança de comportamento.
- **Meta:** 85% no assembly `Unice.Domain`. O número exato deve ser lido no artefato de cobertura do CI.
- **Sem teste automatizado (verificação manual):** UI Toolkit, áudio, render e a camada de rede.
  Roteiro manual mínimo: partida solo completa (Home→Lobby→Hide→Hunt→Result→Final); sala online com dois aparelhos
  (criar, entrar por código, jogar uma rodada, fechar o app do host no meio → o outro assume).

## Recommendations

1. **Alta prioridade — teste manual do build atual.** A reorganização foi grande; os testes cobrem regras, bots e
   snapshot, mas não UI, áudio e rede. Fazer o roteiro manual acima antes do próximo release. Os dois aparelhos
   precisam do mesmo build: o formato do snapshot mudou (`Game.SnapshotVersion = 5`).
2. **Média prioridade — teste do caminho de troca de host.** `RoomSync.ElectHost` + `MatchHost.TakeOver` é o fluxo
   mais sensível e só é verificado à mão. Um `INetBackend` falso (lista de `PeerInfo` controlada pelo teste) permitiria
   testar a troca de host em Edit Mode sem rede.
3. **Média prioridade — acompanhar a cobertura do `Unice.Domain`** no artefato do `tests.yml` e fechar o que faltar
   para 85% (provavelmente `Navigation` em casos de borda e os mapas novos).
4. **Longo prazo / só se mudar algo:** extrair Use Cases explícitos de `Game` só se surgir um segundo orquestrador
   (ex.: modo de jogo diferente). Dividir `Game.cs` mais só se ele voltar a crescer — hoje é o port das regras,
   e mantê-lo junto facilita comparar com a versão web.

**Próxima fatia concreta:** o roteiro manual (recomendação 1). Depois, o `INetBackend` falso para cobrir a troca de host.

---

## O que foi feito desde a auditoria anterior

O plano de 2026-10-01 (slices 0–5 de testes + split de `App`/`Ui` em 3 fatias) foi executado e ampliado:

| Item do plano | Resultado |
|---|---|
| Slice 0 — assembly `Unice.Domain` sem Unity + assembly de testes + pacote de cobertura | Feito |
| Slices 1–5 — testes de `Util`, `Kit`, `Map`, `Game`, `Snapshot` | Feito (hoje `CommonTests`, `KitTests`, `MapTests`, `GameTests`/`GameAiTests`, `SnapshotTests`) |
| Violação #1 — `App.HumansNow()` estático dentro de `Game` | Resolvida (delegate injetado, hoje em `MatchHost.Step`) |
| Fatia 1 — `ProfileStore` | Feito |
| Fatia 2 — `RoomSession` + `PickHost` puro | Feito: `RoomSession` + `RoomSync`; a regra virou `HostElection` (com testes) |
| Fatia 3 — `Lobby` | Absorvida por `RoomSession.LobbyPlayers`/`HumansForNextRound`/`StartMatch` |
| "Não quebrar `Ui.cs` agora" | Revisto a pedido do time (manutenção): `Ui` virou uma classe por tela/painel em `Ui/Screens` e `Ui/InGame` |

Além do plano:
- **Nomes e comentários:** ~500 tipos/membros renomeados com Roslyn para inglês descritivo (`ph`→`phase`,
  `Ent`→`Character`, `Furn`→`Furniture`, `Loc.T`→`Localization.Get`…), variáveis locais e parâmetros revisados à mão,
  `/// <summary>` em todas as classes, números mágicos viraram constantes, strings de estado viraram enums.
- **Domínio:** IA dos bots extraída para `Match/Bots`; `Map` dividido em `Map` + `Navigation`; `Util` dividido em
  `GameRandom`, `Geometry`, `TextFormat`; `GameEvent` e `RoundResult` com campos nomeados (antes `a..e` e `int[4]`);
  `Wire` → `PresenceCodec`; parte do host do `Game` → `MatchHost`.
- **Core:** `App` dividido em `PlayerControl`, `Perspective`, `RoomSession`, `RoomSync`, `EventFeed`, `Soundscape`,
  `Heartbeat`, `CameraRig`, `SystemBars`; `WorldView`, `Sfx`, `Art` divididos por responsabilidade.
- **Telemetria:** Google Analytics (GA4) + Crashlytics via Firebase (`Metrics`), com erros tratados enviados como
  não-fatais.
