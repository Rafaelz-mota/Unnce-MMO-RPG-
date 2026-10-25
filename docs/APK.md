# Gerar o APK de teste (Android)

O GitHub compila o jogo na Unity e entrega um **APK** que você baixa e instala no celular. Ninguém precisa instalar a
Unity no próprio aparelho.

> **Aviso honesto:** este caminho **nunca foi executado**. Nem o script de build, nem o workflow, nem a camada Unity do
> jogo rodaram em uma Unity de verdade. É esperado que as primeiras tentativas falhem. O workflow publica o log numa
> branch chamada `android-report`, então o erro pode ser lido e corrigido sem você copiar texto.
> O APK é de **teste** (assinado com a chave de debug da Unity). Não serve para a Play Store.

## O que você precisa

1. Uma **conta Unity gratuita** (a que você já tem). **Mantenha a verificação em duas etapas ligada.** O arquivo de
   licença é gerado com você logado no Unity Hub, onde a verificação funciona normalmente. O GameCI também usa e-mail e
   senha como segredos; **não verifiquei** se esse login automático funciona com a verificação ligada. Se a build falhar
   com erro de login, essa é a causa provável, e a decisão sobre o que fazer é sua (não recomendo desligar a
   verificação). A senha só deve ir para os Secrets do GitHub, nunca para o chat. Prefira uma senha só de letras e
   números, porque símbolos já causaram problema nesse tipo de login.
2. **Um computador (Windows, Mac ou Linux) por alguns minutos**, uma única vez, para gerar o arquivo de licença.
   Não dá para fazer isso pelo celular: desde 2024 a Unity não ativa mais licença Personal pelo site, só pelo Unity Hub.
3. Acesso às configurações do repositório no GitHub (você é o dono).

## Existe um jeito oficial sem computador? (pesquisado em 25/10/2026)

**Não encontrei nenhum.** O que a pesquisa mostrou:

- A documentação da Unity diz que a ativação manual **não funciona** com Unity Personal e que a ativação Personal é
  feita pelo Unity Hub. Não há processo oficial de licença Personal para CI sem o Hub.
- O **Unity Build Automation** (build na nuvem da Unity) não aparece entre os recursos listados do plano Personal. Para
  2026 a Unity anuncia 100 minutos gratuitos de build para **Mac**; não confirmei nada para Android.
- Existem **ações de terceiros** (por exemplo `buildalon/activate-unity-license`) que entram na conta Unity com e-mail e
  senha dentro do GitHub, sem computador e sem arquivo `.ulf`. Um projeto público as usa em 2026. Problemas: a
  documentação delas não trata de verificação em duas etapas (quem usa costuma recomendar uma conta sem ela), a senha
  passa por código de terceiros, uma ação parecida que aceitava a chave do autenticador foi arquivada em janeiro de 2026,
  e um relato de junho de 2026 no fórum da Unity registra falhas de licença nesse tipo de login.
  **Não adotei esse caminho**, justamente por empurrar para contas sem verificação em duas etapas.

### Caminho B (experimental): Unity CLI oficial, login pelo celular

A Unity publicou uma CLI oficial (`unity`, em beta). O que **foi testado** num runner do GitHub (25/10/2026):

- `unity install 6000.0.34f1 -m android` instala o Editor com Android, **sem login**, em cerca de 13 minutos.
- `unity auth login` imprime um link de login que dá para abrir no navegador do celular (a verificação em duas etapas
  acontece lá, sem senha no GitHub).
- `unity build` sem licença falha em "No valid Unity Editor license", como esperado.

O que **não foi testado**: ativar a licença Personal (`unity license activate --personal`) depois do login, compilar o
projeto e gerar o APK. Por isso este caminho **não está pronto**, e a licença Personal por esse meio pode não funcionar
em CI. O caminho A (passos abaixo, em um computador) segue como o documentado.

Conclusão: **o passo 1 abaixo, em um computador, é obrigatório** para gerar o APK. Ele é feito uma vez. Enquanto ele não
acontece, o desenvolvimento continua sem Unity: a lógica do jogo é compilada e testada pelo GitHub (veja `ARQUITETURA.md`).

## Passo 1: gerar o arquivo de licença (no computador)

1. Instale o **Unity Hub** (unity.com/download) e entre com a conta Unity.
2. Em **Preferences > Licenses**, clique em **Add** e escolha a licença **gratuita (Personal)**. Conclua a ativação.
   Se a licença aparecer mas o arquivo não, clique em **Add** de novo mesmo assim.
3. Localize o arquivo `Unity_lic.ulf` (as pastas costumam ser ocultas):
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - Mac: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`
4. Abra o arquivo em um editor de texto e **copie todo o conteúdo**.

Se o arquivo não for criado, instale qualquer versão do Editor pelo Hub e abra um projeto vazio uma vez (tentativa;
a documentação do GameCI só diz que o arquivo pode não ser gerado e remete à comunidade deles).

## Passo 2: criar os 3 segredos no GitHub

No repositório: **Settings > Secrets and variables > Actions > New repository secret**. Crie:

| Nome | Valor |
|---|---|
| `UNITY_LICENSE` | O conteúdo completo do `Unity_lic.ulf` |
| `UNITY_EMAIL` | O e-mail da conta Unity |
| `UNITY_PASSWORD` | A senha da conta Unity |

Os segredos ficam ocultos nos logs. Não os envie a ninguém, nem a mim.

## Passo 3: rodar a build

1. No repositório, abra **Actions** e escolha **Android - gerar APK de teste**.
2. Toque em **Run workflow** e confirme. No celular, se o botão não aparecer, use "Versão para computador" do navegador.
3. A primeira execução leva de **20 a 60 minutos** (baixa a imagem da Unity e compila o jogo). As próximas são mais rápidas.

## Passo 4: instalar no celular

1. Quando a execução terminar com sucesso, abra-a e vá até **Artifacts**. Baixe `mmorpg-teste-apk` (vem num `.zip`).
2. Descompacte com o app de arquivos e abra `mmorpg-teste.apk`.
3. O Android pedirá para permitir **instalar apps desta fonte**. Permita só para o navegador ou app de arquivos usado.
4. Abra **MMORPG Teste**. O jogo se cria sozinho; o joystick fica no canto inferior esquerdo e as habilidades à direita.
   Os controles de toque estão descritos em `EXECUTAR.md`.

## Se falhar

Me avise com "a build falhou". O workflow publicou o log na branch `android-report` (arquivos `erros.log` e `final.log`),
e eu leio de lá. Causas prováveis, em ordem:

| Sintoma | Provável causa |
|---|---|
| Falha logo no primeiro passo, "Faltam os segredos" | Passo 2 incompleto |
| Erro de licença ou de login | `.ulf` copiado incompleto, senha com símbolos, conta com verificação em duas etapas, ou limite de ativações |
| Erro `CS....` de compilação | Diferença entre a Unity real e o que o jogo assume. Esperado na primeira vez |
| "Package ... not found" | Nome de pacote ou de versão incorreto em `client/Packages/manifest.json` |
| Imagem da Unity não encontrada | A versão `6000.0.34f1` do workflow não existe nas imagens do GameCI; trocar por outra |
| Instala mas fecha ao abrir | Erro de execução no jogo; é preciso ler o log do aparelho (veja abaixo) |

Para ler o log do aparelho é preciso um computador com `adb logcat`. Sem computador, a tela do jogo mostra a mensagem de
falha de inicialização quando o erro é de dados.

## O que você vai ver

O Vale do Despertar montado com **formas simples** (cápsulas, blocos), monstros, o chefe Colosso da Raiz, barra de vida,
joystick e botões de habilidade, inventário e menu. Não há modelos finais, animações nem som.
