# Analytics e Crashlytics — Unice

O jogo envia eventos para o **Google Analytics (GA4)** e erros para o **Crashlytics**, ambos via Firebase.
Todo o envio passa por `Assets/Unice/Scripts/Core/Analytics/Metrics.cs`.

## Como funciona

- **Coleta desligada no Editor.** Só builds em aparelho enviam dados.
- **Fila até o Firebase iniciar.** Eventos e erros registrados antes da inicialização ficam guardados e são enviados
  quando o Firebase fica pronto. Se o Firebase não iniciar, tudo vira no-op — o jogo nunca depende dele.
- **Breadcrumbs.** Cada evento do GA também vira uma linha no log do Crashlytics (ex.: `round_start round=2 role=hider`),
  então um relatório de crash mostra o que o jogador fez antes. A chave customizada `phase` mostra a fase atual.
- **Crashes:** exceções C# não tratadas são reportadas como **fatais**.
- **Erros tratados:** os `catch` do jogo chamam `Metrics.Error(error, "onde")` e o erro aparece como **não-fatal**,
  com o local (`room`, `net`, `voice`, `snapshot`, `event <Tipo>`). Erros esperados de sala (código errado, sala cheia —
  `NetException`) não são reportados como erro do jogo.

## Regras para adicionar eventos

- `Metrics.Log("nome_do_evento", ("chave", valor), ...)`.
- Nomes e chaves em **snake_case** — só letras, números e `_` (sem ponto nem espaço), até 40 caracteres. O GA4 rejeita
  o resto em silêncio.
- Valores: `bool` vira 1/0; `int`/`long`/`float`/`double` vão como número; o resto como texto.
- Ao adicionar ou mudar um evento, atualize a tabela abaixo.

## Propriedade de usuário

| Propriedade | Valores | Quando muda |
|---|---|---|
| `game_lang` | `pt`, `en`, `es` | Ao iniciar e quando o jogador troca o idioma no jogo |

## Eventos

| Evento | Quando | Parâmetros | Onde no código |
|---|---|---|---|
| `screen_view` | A fase muda, ou a página do menu inicial muda | `screen_name` / `screen_class` = `home`, `lobby`, `hide`, `hunt`, `result`, `final`; páginas da Home: `home` (principal), `home_online`, `home_how_to_play`, `home_my_kid` | `App.Step`, `HomeMenu.ShowPage` |
| `solo_start` | Começa uma partida solo | — | `RoomSession.PlaySolo` |
| `room_create` | Sala criada com sucesso | `voice` (Vivox disponível, 1/0) | `RoomSession.EnterRoom` |
| `room_join` | Entrou numa sala | `voice` | `RoomSession.EnterRoom` |
| `room_error` | Falha ao criar/entrar | `mode` (`create`/`join`), `error` (tipo da exceção, ou `no_host`) | `RoomSession.EnterRoom` |
| `room_lost` | A conexão caiu de vez | `phase` em que estava | `RoomSession.OnRoomLost` |
| `room_leave` | Saiu da sala **ou da partida solo** | `reason` (`button` = botão sair/voltar ao início na sala de espera, pausa ou resultado; `back` = voltar do Android na sala de espera; `lost` = a conexão caiu, junto com `room_lost`; `timeout` = o host não respondeu ao entrar, junto com `room_error` `no_host`), `phase` em que estava, `round` (0 = antes da 1ª rodada), `online`, `lobby_wait_s` (s na sala de espera; 0 se saiu de outra fase) | `RoomSession.LeaveRoom` |
| `match_start` | O host inicia a partida | `humans`, `bots`, `rounds`, `map`, `online`, `hide_time`, `round_time` (tempo de caça, s), `lobby_wait_s` (s desde que o host entrou na sala de espera) | `RoomSession.StartMatch` |
| `round_start` | Começa uma rodada (em cada aparelho) | `round`, `rounds`, `role` (`unice`/`holder`/`hider`/`spectator`), `online`, `map` | `EventFeed.OnRoundStart` |
| `round_end` | Fim da rodada | `round`, `unice_won`, `role` (`unice`/`hider`/`spectator`), `won`, `caught`, `online`, `duration_s` (s do início da rodada, esconder + caça, relógio do aparelho; 0 = não viu o início) | `EventFeed.OnRoundEnd` |
| `catch` | O jogador pegou ou foi pego | `side` (`caught`/`catcher`), `with_doll`, `time_left` (s), `room` (id do cômodo) | `EventFeed.OnCatch` |
| `match_end` | Placar final | `rounds`, `score`, `place` (1 = venceu), `players` (com bots), `online`, `duration_s` (s desde a 1ª rodada, relógio do aparelho), `humans_peak` (mais humanos numa rodada da partida) | `EventFeed.OnMatchEnd` |
| `first_match_end` | A 1ª partida terminada nesta instalação (uma vez só; `PlayerPrefs` `unice.first_match_end`) | `online`, `place`, `duration_s` | `EventFeed.OnMatchEnd` |
| `host_changed` | O host saiu e este aparelho assumiu a partida (só o novo host loga: 1 evento por troca) | `phase`, `round`, `humans` (que restaram) | `EventFeed.OnNewHost` |
| `achievement_unlock` | O jogador cumpriu uma conquista padrão do Play Games (A1–A10; as incrementais não). Pode repetir — o Play Games só desbloqueia uma vez; para contar desbloqueios use usuários únicos por `id` | `id` (nome em `Achievement`, ex. `FoundYou`) | `EventFeed.Report` |
| `player_action` | O jogador usa uma ação | `type` (`hide`/`exit`/`open`/`pass`/`laugh`), `spot` (tipo do móvel, só em `hide`) | `PlayerControl` |
| `pause` | Abriu o menu de pausa | `online` | `App.TogglePause` |
| `player_muted` | Silenciou ou voltou a ouvir outro jogador | `muted` (1 = silenciou) | `App.TogglePlayerMuted` |

## Quando a voz voltar

A voz está desligada no M1 (`VoiceRegistry.Enabled = false`). Ao religar, adicionar `mic_permission` (`granted` 1/0)
quando o Android responder ao pedido do microfone, e `voice_connect` (`ok`, `error`) ao entrar no canal.

## Como verificar

- Build de desenvolvimento no aparelho Android +
  `adb shell setprop debug.firebase.analytics.app <package>` → console do Firebase, **DebugView**.
- Crashlytics: provocar um erro num build, **reabrir o app** (o relatório sobe na abertura seguinte) e conferir o console
  depois de alguns minutos.
