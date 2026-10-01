# Clean Architecture Review Report — Unice

Auditoria do estado real do código em `Assets/Unice/Scripts/Core/` e `Assets/Unice/Scripts/Net/` (branch `develop`, arquivos lidos do disco em 2026-10-01). Esta é a fase de **plano**, nada em `Assets/` foi alterado.

## Summary

- **Overall Assessment: Good** — melhor do que o pedido fazia parecer. O código já separa, por convenção, lógica pura de C# de código dependente de `UnityEngine`. Não é um "projeto desorganizado que precisa de Clean Architecture do zero"; é um projeto com domínio quase limpo que precisa de **1 fronteira enforced por compilador + 1 fix pontual + uma suíte de testes que não existe ainda**.
- 7 dos 14 arquivos de `Core/` (mais `Net.cs`) já têm **zero** `using UnityEngine` — ou seja, já são domínio puro, testável em Edit Mode sem cena: `Ent.cs`, `Game.cs`, `Kit.cs`, `Map.cs`, `Net.cs` (a interface), `Snapshot.cs`, `Util.cs`.
- A violação real da regra de dependência é **uma linha**, não um padrão disseminado.
- Cobertura de testes hoje: **0%** (nenhuma pasta de testes, nenhum asmdef de teste; `com.unity.test-framework` está instalado mas não usado; `com.unity.testtools.codecoverage` não está instalado).

## Dependency Rule Analysis

| # | Arquivo:linha | O quê | Por que importa |
|---|---|---|---|
| 1 | `Core/Game.cs:462` — `else StartRound(App.HumansNow());` dentro de `HostStep` | Domínio (`Game`, autoritativo do host) chama estaticamente `App.HumansNow()`, método de um `MonoBehaviour` (camada externa) | Único ponto em que o domínio sabe que `App` existe. Impede testar `Game.HostStep` isoladamente sem subir a cena/`App`, e impede trocar quem fornece a lista de humanos (ex.: em teste) sem mockar um `MonoBehaviour` estático. |
| 2 | `Unice.Core.asmdef` | Um único assembly contém tanto os 7 arquivos de domínio puro quanto os adapters Unity (`App.cs`, `Ui.cs`, `WorldView.cs`, `FogView.cs`, `Art.cs`, `Sfx.cs`) | A separação domínio/adapter que já existe é só convenção. Nada impede um futuro PR de adicionar `using UnityEngine;` dentro de `Game.cs` e ninguém notar no code review. A fronteira não é verificada pelo compilador. |
| 3 | Ausência de testes | — | Não é uma violação da regra de dependência, mas é o risco maior apontado no próprio SPEC.md ("nunca fazer: quebrar o straight port das regras sem alinhar com a versão web") — hoje isso só é verificado manualmente. |

Nenhuma outra chamada de `Game.cs`, `Ent.cs`, `Map.cs`, `Snapshot.cs`, `Util.cs`, `Kit.cs` ou `Net.cs` para `App`/`Ui`/`WorldView`/`Art`/`Sfx` foi encontrada (confirmado por grep em todo `Core/Game.cs`).

## Layer Structure Review

### Domain (Entities) — **Good**, só falta isolar
`Ent.cs`, `Game.cs` (incl. `Rules`, `Presence`, `GameEvent`, `PeerInfo`, `DollState`), `Kit.cs`, `Map.cs`, `Snapshot.cs`, `Util.cs` (`U`, `P`). Zero `UnityEngine`. `Game` é `sealed partial class` — autoritativo no host, espelhado nos peers, com `SimUpdate`/`HostStep`/`DoAction`/`NewMatch`/`StartRound`/`EndRound`/`TakeOver` como pontos de entrada. `Ent` é um POCO de estado de round. `Map` expõe geometria/pathfinding (`CanStand`, `WalkLine`, `FindPath`, `RectDist`, `SightLine`, `NearestNode`) sem dependência externa.

### Use Cases — dobrado dentro do Domain hoje, e está certo assim
Não existe uma camada de Use Cases separada, e **não deveria existir ainda**: os métodos públicos de `Game` (`NewMatch`, `StartRound`, `HostStep`, `DoAction`, `TakeOver`) já são a fronteira de orquestração, com uma única implementação e uma única responsabilidade (rodar a partida). Extrair classes `XyzUseCase` agora seria interface/abstração para um único consumidor — o próprio anti-padrão que o `SPEC.md` já rejeita ("sem interfaces para tipos com uma única implementação"). Revisitar só se `Game.cs` crescer bem além de ~500 linhas ou ganhar um segundo orquestrador.

### Interface Adapters (MonoBehaviours/ScriptableObjects) — **Needs Improvement (SRP, não Clean Arch)**
`App.cs` (`MonoBehaviour`, 543 linhas): input (`ReadKeys`), ciclo de vida (`Awake`/`Update`/`OnApplicationFocus`/`OnApplicationPause`), elenção de host (`ElectHost`), snapshot pull/publish, roteamento de eventos para `Ui`/`Sfx` (`HandleEvents`/`OnEvent`), persistência de perfil (`SaveProfile`), entrada/saída de sala (`EnterRoom`/`LeaveRoom`). Clássico "MonoBehaviour-deus" — aceitável para um adapter, mas é a maior concentração de responsabilidades do projeto (SRP). Não bloqueia a meta de cobertura (ver seção Test Coverage) porque não é lógica de regra; é só glue.
`Ui.cs` (841 linhas), `WorldView.cs`, `FogView.cs`, `Art.cs`, `Sfx.cs`: consomem estado do domínio e traduzem para UI Toolkit (`Ui.Doll : VisualElement`)/render/áudio. Dependem do domínio, nunca o contrário (confirmado — nenhuma chamada reversa encontrada). Isso é exatamente a direção correta da Dependency Rule.

### Frameworks & Drivers — **Good**
`Net.cs` define `INetBackend` (porta) + `NetRegistry.Factory` (service locator, populado só se o assembly `Unice.Net` compilar — ou seja, o jogo roda solo mesmo sem os pacotes de multiplayer: DIP bem aplicado, com motivo real, não especulativo). `NgoBackend : INetBackend` (123 linhas) é a implementação concreta sobre Netcode for GameObjects. `NetPlayer : NetworkBehaviour` é o objeto de rede do Netcode. Unity Services Multiplayer, Firebase (em integração) e o próprio Unity entram aqui.

## Test Coverage

- **Atual:** 0% — nenhum teste, nenhum asmdef de teste, `com.unity.testtools.codecoverage` não instalado.
- **Meta:** 85% nos arquivos de domínio listados acima (depois de isolados em assembly próprio). **Não** nos adapters (`App`, `Ui`, `WorldView`, `FogView`, `Art`, `Sfx`): forçar 85% ali significaria testes PlayMode caros, acoplados a cena/input, de baixo sinal por linha coberta — custo alto, confiança baixa. Se sobrar tempo, testes de fumaça pontuais nos adapters são bem-vindos, mas não contam para a meta.
- **Gap:** 100 pontos percentuais no domínio, hoje zero infraestrutura de teste.

### Plano de rollout (fatiado, TDD red→green→refactor por fatia)

| Slice | Alvo | Por quê este tamanho/ordem |
|---|---|---|
| 0 — infra, sem mudança de comportamento | Criar `Assets/Unice/Scripts/Domain/Unice.Domain.asmdef` (sem referência a `UnityEngine.CoreModule`) e mover para lá os 7 arquivos puros; criar `Unice.Domain.Tests.asmdef` (Edit Mode, referencia `com.unity.test-framework` + `Unice.Domain`); instalar `com.unity.testtools.codecoverage`. Corrigir só os `using`/namespaces que quebrarem na mudança de assembly. | Fronteira passa a ser verificada pelo compilador. Zero risco de regra de jogo porque nenhuma linha de lógica muda. Verificação: projeto compila, fluxo manual Home→Final continua igual. |
| 1 — primeiro TDD real | `Util.cs` (`U`): `CleanName`, `CleanCode`, `Mmss`, `Clamp`, `Dist`. Para `Rand`/`Pick`/`Shuffle`/`RandInt`/`OfflineCode` (não-determinísticos, `rng` estático): testar invariantes (`Pick` retorna elemento da lista, `Shuffle` preserva o multiset, `RandInt(n)` está em `[0,n)`), não valores exatos. | Zero dependências, zero efeito colateral, maior razão valor/esforço do projeto. Prova o loop de TDD ponta a ponta rapidinho. |
| 2 | `Kit.cs` (pack/clean/random de cosméticos) | Pequeno, puro, provavelmente já tem bugs de borda não cobertos (valores inválidos de kit). |
| 3 | `Map.cs`: `RectDist`, `CanStand`, `WalkLine`, `SightLine`, `NearestNode`, `FindPath` | Geometria pura que toda a IA (`Game.cs`) depende — vale caracterizar antes de qualquer refactor futuro no pathfinding. |
| 4 — maior payoff | `Game`/`Rules`: `NewMatch`, `StartRound`, `DoAction`, `SimUpdate`, `EndRound`, `HostStep`, `TakeOver` | O núcleo comportamental do "straight port". Escrever testes de caracterização sobre o comportamento ATUAL antes de tocar na violação #1 (`App.HumansNow()`), para garantir que o fix não mude nada visível. |
| 5 | `Snapshot.cs`: round-trip serialize/deserialize | Fecha a cobertura do domínio. |

## Recommendations

1. **Alta prioridade:** Slice 0 (asmdefs) + corrigir a violação #1 — trocar `App.HumansNow()` estático por um delegate injetado em `Game` (ex.: `Func<List<Human>> humansNow` setado por `App` uma vez), com teste de caracterização cobrindo `HostStep` antes do fix para garantir comportamento idêntico.
2. **Média prioridade:** instalar `com.unity.testtools.codecoverage`, configurar relatório por assembly, acompanhar `Unice.Domain` até 85% fatia por fatia (tabela acima).
3. **Longo prazo / só se mudar algo:** considerar quebrar `App.cs` por responsabilidade (input / ciclo de vida / rede / roteamento de evento) — só vale a pena se `App.cs` continuar crescendo ou começar a causar bugs por acoplamento, não por estética de SRP. Extrair Use Cases explícitos de `Game.cs` — só se ganhar um segundo orquestrador ou crescer muito além do tamanho atual.

**Próxima fatia concreta a executar:** Slice 0 (criar os dois asmdefs, mover os 7 arquivos, instalar o pacote de coverage — sem mudar nenhuma linha de lógica) imediatamente seguido do Slice 1 (`U.CleanName`/`CleanCode`/`Mmss`), porque não depende de nenhum outro slice e prova o loop de TDD mais rápido que qualquer outro ponto de entrada.

---

## App/Ui Split Plan — plano, ainda não executado

Pedido do usuário: reduzir granularidade de `App.cs` (543 linhas) e `Ui.cs` (841 linhas), que já eram a recomendação de longo prazo #3 acima. Antes de propor qualquer split, `Ui.cs` foi lido por completo (não só por amostragem) para confirmar se o tamanho é SRP real ou só estética.

### Veredito `Ui.cs`: não vale a pena quebrar agora

- 841 linhas, 2 classes (`Ui` e `Doll : VisualElement`), mas fisicamente organizadas em 7 regiões bem marcadas por comentário: menu principal, sala de espera, resultado/final, pausa, HUD, mensagens/cues/texto de mundo, touch. Cada região tem seus próprios campos privados e métodos `Build*`/`Render*`/`Update*`; não há emaranhado real entre regiões além de infraestrutura compartilhada (`app`, `G`, os `VisualElement`s de layout, os helpers estáticos `E/L/B/Show/Txt/Ig`).
- É o mesmo padrão de `Game.cs`: grande porque faz o trabalho de um único adapter (traduzir estado do domínio para UI Toolkit) para várias telas, não porque responsabilidades estejam misturadas. Quebrar em ~6 classes significaria injetar `app`/`G`/`root`/os helpers compartilhados em cada uma, por zero ganho de testabilidade — `Ui` é cola de UI Toolkit, fora da meta de 85% de domínio, e só testável via PlayMode de qualquer forma, split ou não.
- **Achado real que muda o plano de `App.cs`:** `Ui.cs` chama diretamente ~20 membros de `App` que eu tinha planejado extrair — `SaveProfile`, `SetKit`, `EnterRoom`, `LeaveRoom`, `FindHost`, `SetNet`, `LobbyHumans`, `StartMatch`, `ToggleReady`, `NewMatchLobby`, `TryPass`, `DoAct`, `TogglePause`, mais os campos `myName`, `kit`, `rdy`, `code`, `netText`, `netErr`, `Online`, `NetAvailable`, `busy`, `me`. Isso não invalida o plano de `App.cs`, mas fixa uma regra de design obrigatória (ver abaixo).
- **Revisitar só se:** uma tela específica precisar de teste unitário por mérito próprio (improvável, é renderização) ou se duas pessoas começarem a dar conflito de merge recorrente em regiões diferentes do mesmo arquivo (não observado).

### Regra de design para o split de `App.cs`, por causa do que `Ui.cs` revelou

Como `Ui.cs` chama esses membros pelo nome, qualquer extração só é segura se `App` continuar expondo a mesma superfície pública como *passthroughs* de uma linha para as novas classes. Se em vez disso eu reescrevesse `Ui.cs` para chamar as novas classes diretamente, estaria tocando o único arquivo que acabamos de decidir não tocar, trocando um move mecânico de baixo risco por um de risco de quebrar UI. Então: a lógica/estado migram para classes C# simples; `App` fica com métodos de uma linha delegando.

### Fatia 1 — `ProfileStore` (menor risco, primeira)

- **O que muda:** `SaveProfile()`, `SetKit(int[])` e os campos `myName`/`kit` migram para `class ProfileStore { public string Name; public int[] Kit; public void Save(); public void SetKit(int[] k); }`. Ainda referencia `UnityEngine.PlayerPrefs`, então fica em `Unice.Core` (adapter), não em `Unice.Domain`.
- `App` mantém `myName`/`kit` como propriedades delegando para `profile.Name`/`profile.Kit`, e `SaveProfile()`/`SetKit()` como passthroughs — `Ui.cs` não muda uma linha.
- **MonoBehaviour/cena:** nenhuma referência direta — `ProfileStore` é um campo simples instanciado em `App.Awake()`, nada em `.unity`/`.prefab` referencia esses nomes.
- **Verificação:** 1 teste de caracterização Edit Mode (`PlayerPrefs` funciona em Edit Mode, não precisa de PlayMode) checando que `Save()` grava as chaves esperadas (`unice.name`, `unice.kit` compactado) e que `SetKit` passa por `Kit.Clean`; `PlayerPrefs.DeleteKey` no teardown para não vazar no perfil real da máquina.

### Fatia 2 — `RoomSession` (maior payoff, maior risco — depois da fatia 1 provar o padrão)

- **O que muda:** `EnterRoom`, `LeaveRoom`, `RoomLost`, `PullSnap`, `Publish`, `ResetRoomState`, `SetNet`, `FindHost`, `ElectHost`, e os campos de rede (`net`, `code`, `netText`, `netErr`, `lastApplied`, `lastS`, `lastEv`, `aSeq`, `aType`, `aArg`, `joinT`).
- **Achado bônus ao ler `FindHost`:** a regra de seleção de host em si (quem ganha entre candidatos `PeerInfo`) não depende de `UnityEngine` além do timestamp "agora". Vale extrair como função pura — ex. `Game.PickHost(IReadOnlyList<PeerInfo> peers, float now)` dentro de `Unice.Domain` (ao lado de `PeerInfo`, já em `Game.cs`) — com `FindHost()` virando `Pick(net.Peers, Time.realtimeSinceStartup)`. Isso é extração de lógica de domínio de verdade (regra de eleição de host testável), não só redução de arquivo — tratar como sub-fatia 2a.
- `App` mantém `FindHost() => room.FindHost();`, `SetNet(...) => room.SetNet(...);`, `EnterRoom(...) => room.EnterRoom(...);` etc. como passthroughs — `Ui.cs`/qualquer outro chamador de `app.FindHost()`/`app.code` não muda.
- **MonoBehaviour/cena:** `EnterRoom`/`LeaveRoom` são `async void` chamados por delegates C# construídos em `Ui.BuildHome()`/`BuildWait()` (botões `createBtn`, `joinBtn`, `leave2`, `pauseLeave`, botão de resultado "Sair"), não por UnityEvent cabeado no Inspector — mover o corpo não toca nenhum asset de cena/prefab.
- **Verificação:** `FindHost`/o novo `PickHost` puro ganham teste unitário real (lista de `PeerInfo` fake, assert de quem ganha por `lastSnapEp`/tie-break de join-time) — é a única regra de jogo de verdade nesta fatia. `EnterRoom`/`LeaveRoom`/`PullSnap`/`Publish` dependem de um `INetBackend`/`NetworkManager` vivo e não são testáveis unitariamente sem um fake backend (fora de escopo aqui) — verificação **manual**: duas instâncias do Editor (ou Editor + build), criar sala, entrar com código, cair conexão, confirmar que a reeleição de host e o texto de reconexão continuam idênticos antes/depois do move. Avisar o usuário explicitamente antes do merge: esta é a única parte do plano sem rede de segurança automatizada.

### Fatia 3 — `Lobby` (por último, depende da `RoomSession` da fatia 2)

- **O que muda:** `LobbyHumans()`, `HumansNow()` (já injetado como delegate em `Game.HostStep` desde o fix da violação #1), `StartMatch`, `NewMatchLobby`, `ToggleReady`, e o campo `rdy`.
- `App` mantém passthroughs; `Ui.cs` (`RenderWait`, callback do `actBtn`, `SyncScreens`) não muda.
- **MonoBehaviour/cena:** nenhuma — mesmo padrão, chamado só por delegates de botão do `Ui`.
- **Verificação:** `LobbyHumans()` é moldagem de dados (`net.Peers`/bots → lista), baixo valor para teste isolado — checagem manual (lobby local com 1 humano + N bots mostra contagem certa) é suficiente. `StartMatch`/`ToggleReady`/`NewMatchLobby` são one-liners para `Game`, já cobertos indiretamente pelos testes de caracterização de `Game` (Slice 4 do plano de testes de domínio) — não precisa de teste novo aqui.

### Ordem de execução e dependências

1. **Fatia 1 `ProfileStore`** — zero acoplamento com o resto, prova o padrão "App mantém passthrough, `Ui.cs` intocado" com baixo custo.
2. **Fatia 2 `RoomSession`** (+ sub-fatia 2a `Game.PickHost` puro) — maior ganho e maior risco; só depois do padrão provado, com a parte sem teste automatizado avisada explicitamente antes do merge.
3. **Fatia 3 `Lobby`** — menor pedaço restante, depende de `RoomSession` existir (chama `room.FindHost()`/`net.Peers`).

Cada fatia é um commit/PR isolado. Nenhuma delas toca `Ui.cs`, `WorldView.cs`, `FogView.cs`, `Art.cs`, `Sfx.cs` ou qualquer asset `.unity`/`.prefab` — só `App.cs` (encolhe) e os arquivos novos `ProfileStore.cs`, `RoomSession.cs`, `Lobby.cs` em `Scripts/Core/`.

**Nenhum split foi executado ainda — este documento é o plano para aprovação.**
