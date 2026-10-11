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

Próximo passo (outra tarefa): conquistas e placares, a partir de `PlayGamesPlatform.Instance` em `PlayerSignIn.cs`.
