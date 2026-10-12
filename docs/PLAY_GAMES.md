# Google Play Games — configuração (dono do projeto)

O jogo já tem o código: ao abrir o app no Android, o Play Games faz login sozinho (silencioso) e, ao entrar online, o
Unity Authentication usa essa conta. **Enquanto os IDs abaixo não forem configurados, o jogo entra anônimo** (como antes) —
nada quebra, solo nem online. No 1º uso, o perfil ganha o nome do Play Games (se o jogador ainda não salvou um nome).

Código: `Assets/Unice/Scripts/Net/PlayerSignIn.cs` (login único) e `Core/App/ProfileStore.cs` (nome do 1º uso).
Plugin: Google Play Games plugin for Unity **2.3.0** em `Assets/GooglePlayGames/`.

Os nomes de menus do Play Console e do Google Cloud mudam com frequência: quando o nome exato não bater, procure pelo termo
indicado.

## 1. Play Console → Play Games Services

1. No app **Unice** (`com.brunocarvalhs.unice`), procure no menu lateral por **Play Games Services → Configuração**
   (*Setup and management → Configuration*). Crie a configuração: escolha a opção de **jogo que ainda não usa APIs do
   Google** — o Console cria um projeto no Google Cloud junto.
2. Se o Console pedir, configure a **tela de consentimento OAuth** no Google Cloud (link aparece na própria página).
3. **Credencial Android** (*Add credential → Android*). Ela liga o pacote + SHA-1 do certificado. Crie **uma por SHA-1**:
   - **Chave de assinatura do app (Play App Signing)** — é a que assina o que o jogador baixa da Play (inclusive a faixa
     de teste interno). Onde: Play Console → **Testar e lançar → Configuração → Integridade do app → Assinatura de apps**
     (procure por *App integrity* / *App signing*), campo **SHA-1** do "certificado da chave de assinatura do app".
   - **Chave de upload** — a do keystore que o CI usa (`release.yml`). Mesma página, "certificado da chave de upload";
     ou no seu computador: `keytool -list -v -keystore <arquivo>.keystore -alias <alias>` (linha `SHA1:`).
   - (Opcional) **Keystore de debug**, se for instalar builds locais direto pelo cabo:
     `keytool -list -v -keystore ~/.android/debug.keystore -alias androiddebugkey -storepass android`.
4. **Credencial de servidor de jogo** (*Add credential → Game server*). Ela cria (ou pede para criar no Google Cloud) um
   **cliente OAuth do tipo "Aplicativo da Web"**. Anote o **Client ID** (termina em `.apps.googleusercontent.com`) e o
   **Client secret**. É dele que sai o *server auth code* que o Unity Authentication valida.
5. **Testadores** (*Testers*): adicione as contas Google que vão testar. Enquanto a configuração do Play Games não for
   publicada, **só testadores conseguem fazer login**.
6. Depois de testado, publique a configuração do Play Games (procure por *Review and publish* na seção Play Games
   Services). É separado da publicação do app.

## 2. Colar os IDs no projeto (Unity Editor)

1. No Play Console, procure por **Obter recursos** (*Get resources*) — fica nas páginas de **Conquistas** ou **Placares**
   do Play Games Services — e copie o XML da aba **Android**. Ele tem `app_id` e `package_name`.
   Se ainda não houver conquistas/placares, o XML mínimo é (o `app_id` é o número do projeto mostrado na configuração):
   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <resources>
     <string name="app_id" translatable="false">SEU_APP_ID_NUMÉRICO</string>
     <string name="package_name" translatable="false">com.brunocarvalhs.unice</string>
   </resources>
   ```
2. Unity: **Window → Google Play Games → Setup → Android setup...**
   - *Resources Definition*: cole o XML.
   - *Web App Client ID (Optional)* → *Client ID*: o Client ID do passo 1.4 (sem o secret). Para o jogo é obrigatório:
     sem ele não há server auth code e o Unity entra anônimo.
   - Clique em **Setup**.
3. O que muda no repositório (faça commit destes arquivos):
   - `Assets/GooglePlayGames/Resources/PlayGamesSettings.asset` — `mAppId` e `mWebClientId` (o código só liga o Play
     Games quando `mAppId` está preenchido).
   - `Assets/Plugins/Android/GooglePlayGamesManifest.androidlib/AndroidManifest.xml` — `APP_ID` no manifest.
   - `ProjectSettings/GooglePlayGameSettings.txt` — o que foi colado na janela.
   - Se o XML tiver conquistas/placares: a classe de constantes `GPGSIds.cs` (pasta definida na janela).
   App ID e Web Client ID não são segredo. **O Client secret não entra no projeto** — só no Unity Cloud (passo 3).

## 3. Unity Cloud Dashboard → Authentication

1. Em [cloud.unity.com](https://cloud.unity.com), abra o projeto ligado ao Unice e procure por **Player Authentication →
   Identity providers** (*Provedores de identidade*).
2. **Add provider → Google Play Games**: cole o **Client ID** e o **Client secret** do cliente Web (passo 1.4). Salve.
3. Sem isso, o login do Play Games no aparelho funciona, mas o Unity recusa o código e o jogo cai no anônimo (o erro vai
   para o Crashlytics como `play_games`).

## 4. Testar

1. Gere um build pelo CI e envie à **faixa de teste interno** (ou instale um build assinado com uma chave cujo SHA-1 está
   cadastrado no passo 1.3).
2. No aparelho, use uma **conta de testador** (passo 1.5) com o app **Google Play Games** atualizado.
3. Abra o Unice: aparece o aviso "Conectado como …" do Play Games no topo. Em "Minha criança" o nome deve ser o do Play
   Games (só se for 1º uso, ou seja, nenhum nome salvo antes — teste com o app recém-instalado ou com os dados apagados).
4. Crie uma sala online: deve funcionar igual. No `adb logcat`, sem `play_games:` de erro.
5. Repita com uma conta que **não** é testadora, sem o app Play Games, ou recusando o login: o jogo abre, o nome fica
   "Nova", solo e online funcionam (login anônimo).

## 5. Conquistas e ranking

O código já envia tudo; **cada ID vazio é simplesmente não enviado**, então dá para criar no Console aos poucos.
Regras: `Domain/Match/Achievements.cs` (testadas em `AchievementsTests`). Gatilhos: `Core/Gameplay/EventFeed.cs`.
Envio: `Net/PlayGamesProgress.cs` — **único lugar com os IDs**. Nada é enviado no Editor, fora do Android, ou se o login
do Play Games não aconteceu.

### 5.1 Criar no Play Console (Play Games Services → Conquistas / Placares)

Deixe tudo em **rascunho** e teste com contas testadoras antes de publicar: conquista publicada não pode ser apagada, e
tipo, passos e "oculta" na prática não mudam mais. Ícone 512×512 PNG/JPEG. Traduções: pt-BR, en-US, es-419.

| # | Nome pt / en / es | Descrição (pt) | Tipo | Passos | Oculta | XP | Constante em `PlayGamesProgress.cs` |
|---|---|---|---|---|---|---|---|
| A1 | Ninguém me viu / Nobody Saw Me / Nadie me vio | Sobreviva a uma rodada como escondido. | padrão | — | não | 25 | `NobodySawMeId` |
| A2 | Te achei! / Found You! / ¡Te encontré! | Pegue alguém sendo a Unice. | padrão | — | não | 25 | `FoundYouId` |
| A3 | A boneca é minha / Dolly's Keeper / La muñeca es mía | Termine o tempo com a boneca. | padrão | — | não | 50 | `DollsKeeperId` |
| A4 | Devolve a boneca / Give Her Back / Devuélvemela | Como Unice, pegue quem está com a boneca. | padrão | — | não | 50 | `GiveHerBackId` |
| A5 | Batata quente / Hot Potato / Papa caliente | Passe a boneca e seu time vença a rodada. | padrão | — | não | 50 | `HotPotatoId` |
| A6 | Isca / Decoy / Señuelo | Ria de propósito sem a boneca e sobreviva à rodada. | padrão | — | não | 50 | `DecoyId` |
| A7 | Campeão / Champion / Campeón | Termine uma partida em 1º lugar. | padrão | — | não | 50 | `ChampionId` |
| A8 | Entre amigos / Among Friends / Entre amigos | Termine uma partida online com outra pessoa. | padrão | — | não | 50 | `AmongFriendsId` |
| A9 | Rei da sala / Top of the Room / Rey de la sala | Vença uma partida online com 3+ pessoas. | padrão | — | não | 100 | `TopOfTheRoomId` |
| A10 | Sem testemunhas / No Witnesses / Sin testigos | Como Unice, pegue todos — a boneca por último. | padrão | — | **sim** | 100 | `NoWitnessesId` |
| A11 | Veterano / Veteran / Veterano | Termine 25 partidas. | incremental | 25 | não | 100 | `VeteranId` |
| A12 | Caçadora / Huntress / Cazadora | Pegue 50 escondidos sendo a Unice. | incremental | 50 | não | 100 | `HuntressId` |

Total: 750 XP de 1000 (sobram 250 para mapas/temporadas futuras).

**Placar (ranking)**

| # | Nome pt / en / es | Formato | Ordem | Proteção contra adulteração | Limite | Constante |
|---|---|---|---|---|---|---|
| L1 | Vitórias online / Online Wins / Victorias en línea | numérico, 0 casas | maior é melhor | ligada | 0 a 100000 | `OnlineWinsLeaderboardId` |

Conta para o ranking só a vitória (1º lugar) **online**, com **2+ pessoas** no placar final, **3+ rodadas** e **6+ minutos**
no relógio do aparelho. Solo nunca conta. O total fica no aparelho (`unice.online_wins`) e é enviado a cada vitória que conta;
reinstalar o app recomeça do 0 (o ranking mantém o melhor valor até ser superado). Divulgue só as abas "Todos os tempos" e
"Amigos" — com total acumulado, as abas diária/semanal mostram o mesmo número.

### 5.2 Colar os IDs

1. Copie cada ID do Console (coluna ID da conquista/placar, ex. `CgkI...EAI`) — ou de **Obter recursos**, aba Android:
   `<string name="achievement_...">ID</string>`.
2. Cole entre as aspas da constante correspondente em `Assets/Unice/Scripts/Net/PlayGamesProgress.cs`
   (ex. `const string FoundYouId = "CgkI...EAI";`). **Não** use o `GPGSIds.cs` gerado pela janela de setup.
3. Atualize também o XML na janela **Android setup** (seção 2) se quiser, mas o jogo só lê as constantes.

### 5.3 Testar (conta testadora, build da faixa de teste interno)

1. Partida **solo**: como Unice pegue alguém → **A2**; termine a partida em 1º → **A7**. Abra o app Play Games → Conquistas.
   A11 e A12 mostram progresso (1/25, n/50).
2. Ranking: depois de uma partida **solo**, o placar continua vazio. Só após uma partida **online** válida (2 aparelhos com
   contas diferentes, 3+ rodadas, 6+ minutos, vencida) aparece 1 no placar.
3. Com uma conta que não fez login no Play Games: o jogo segue igual, sem erros `play_games` no `adb logcat`.
