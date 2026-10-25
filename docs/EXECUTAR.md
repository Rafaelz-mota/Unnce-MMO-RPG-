# Como executar e testar

> **Aviso honesto:** o núcleo do jogo (`client/Assets/Scripts/Core`) é verificado automaticamente (117 testes no CI).
> A camada Unity (`client/Assets/Scripts/Game`) **ainda não foi aberta nem executada em um Editor Unity**; só é
> compilada no CI contra stubs. Se algo falhar ao abrir na Unity, veja "Solução de problemas" no fim.

## 1. Rodar os testes (sem Unity)

Precisa do **.NET SDK 8** e do Python 3.

```bash
# 1) validar os JSON de balanceamento (sem dependências)
python3 -I tools/validate_data.py client/Assets/Resources/GameData

# 2) compilar o núcleo e rodar os 117 testes
export MMORPG_DATA_DIR="$PWD/client/Assets/Resources/GameData"   # PowerShell: $env:MMORPG_DATA_DIR = "$PWD/client/Assets/Resources/GameData"
dotnet test ci/tests/Mmorpg.Core.Tests.csproj -c Release

# 3) compilar a camada Unity contra stubs (detecta erros de sintaxe/tipos; NÃO prova que roda na Unity)
dotnet build ci/game-check/Mmorpg.Game.Check.csproj -c Release
```

O mesmo roda automaticamente no GitHub a cada push (`.github/workflows/core-ci.yml`).
Existe também `tools/harness/run-tests.ps1`, usado quando o ambiente de desenvolvimento não tinha `dotnet` nem Unity
(compila com o Roslyn do PowerShell 7 e roda os mesmos testes em um mini-NUnit). Em uma máquina normal, prefira
`dotnet test` ou o **Unity Test Runner** (Window > General > Test Runner > EditMode).

## 2. Abrir e jogar na Unity

A pasta `client/` contém só `Assets/`. Os arquivos `ProjectSettings/` e `Packages/` **não estão versionados**,
então o caminho mais seguro é partir de um projeto novo:

1. No Unity Hub, instale o **Unity 6 (6000.x LTS)** e crie um projeto **3D** (Built-in ou URP) vazio. Feche o Editor.
2. Copie a pasta `client/Assets/Scripts`, `client/Assets/Resources` e `client/Assets/Tests` do repositório para a pasta
   `Assets/` do projeto novo (substituindo, se houver).
3. Abra o projeto. Em **Window > Package Manager > "+" > Add package by name**, adicione
   `com.unity.nuget.newtonsoft-json` (o núcleo usa Newtonsoft.Json para ler os dados).
4. Em **Edit > Project Settings > Player > Other Settings > Active Input Handling**, escolha **Both** (ou
   *Input Manager (Old)*) e reinicie o Editor quando ele pedir. Os templates novos vêm com o Input System novo, e a
   entrada do jogo usa o Input Manager clássico.
5. Abra qualquer cena (pode ser a vazia) e aperte **Play**. O jogo se cria sozinho (`GameBootstrap`); não é preciso
   montar objetos na cena.

Se aparecer um erro vermelho de compilação no Console, copie o texto completo: a causa mais provável é diferença de
versão da Unity em relação ao que foi escrito (ver "Solução de problemas").

### Controles

| Ação | PC | Android (toque) |
|---|---|---|
| Mover | W A S D ou setas (relativo à câmera) | Joystick virtual (zona inferior esquerda da tela) |
| Girar a câmera | Botão direito do mouse + arrastar | Arrastar um dedo livre |
| Zoom | Roda do mouse | Pinça com dois dedos |
| Habilidades 1 a 9 | Teclas 1 a 9 ou clique na barra | Botões à direita |
| Selecionar alvo | Clique no monstro, **Tab** (próximo) | Toque no monstro, botão **Alvo** |
| Inventário / equipamento | **B** ou **I** | Botão **Bolsa** |
| Menu (escala da UI, sensibilidade, salvar, sair) | **Esc** | Botão **Menu** |
| Simular a interface de toque no PC | **F2** | (automático em dispositivos de toque) |

As habilidades 6 a 9 são **pagas** (10.000, 20.000, 30.000 e 40.000 de ouro) e só podem ser compradas **clicando ou
tocando** na habilidade (a tecla numérica apenas avisa, para evitar compra acidental).

### Onde fica o save

Um arquivo JSON por personagem em `Application.persistentDataPath/saves/<nome>.character.json`. O jogo escreve o
caminho exato no **Console** ao iniciar (linha "Saves em: ..."). Para recomeçar, apague o arquivo. Saves corrompidos ou com estado **impossível** (item ou classe inexistente, nível
fora do limite, habilidade acima do nível, ouro negativo ou acima do teto, XP incompatível com o nível) são **recusados**:
o arquivo é preservado e o jogo pede um novo personagem com outro nome. Isto é validação de consistência, **não
anti-cheat**: o save é um arquivo local editável, e alterar para um valor plausível (por exemplo, mais ouro dentro do
teto) não é detectado. No jogo online, o progresso fica no servidor.

### Build para Android

Não foi testado. Para gerar um APK de teste sem instalar a Unity, veja [APK.md](APK.md) (build automático no GitHub).
Pontos de atenção para um build manual: *Platform = Android*, `Scripting Backend = IL2CPP` e `ARM64`, e manter
**Active Input Handling = Both**. Teclado na tela para o campo de nome em IMGUI não foi validado; o nome padrão
("Aventureiro") funciona sem digitar.

## 3. Ajustar o balanceamento (sem mexer em código)

Todos os valores de jogo ficam em `client/Assets/Resources/GameData/*.json`:

| Arquivo | Conteúdo |
|---|---|
| `rules.json` | Regras do projeto: nível máximo 55, 24 classes, 12 por facção, 9 habilidades por classe, custos 10k/20k/30k/40k, respawn do chefe (3600 s), slots de equipamento, capacidade do inventário |
| `attributes.json`, `combat.json`, `progression.json` | Atributos, fórmulas e limites de combate, curva de XP e ouro |
| `rarities.json`, `statuses.json` | 5 raridades; efeitos de status (atordoamento, lentidão, escudo, dano no tempo...) |
| `classes.json`, `skills.json` | 24 classes e as habilidades (um objeto por habilidade: custo, recarga, alcance, área, efeitos) |
| `items.json`, `sets.json`, `loot_tables.json` | Itens, conjuntos e tabelas de saque (chances, limites por raridade) |
| `monsters.json`, `regions.json` | Monstros e chefe (fases, resistências), regiões, zonas de nascimento e obstáculos |

Ao iniciar, o jogo **valida tudo**: campo desconhecido (erro de digitação em chave de balanceamento), id repetido,
referência a item/habilidade inexistente, versão de esquema errada e violação das regras do projeto (por exemplo,
alterar o custo de uma habilidade paga ou o respawn do chefe) viram **erros listados em uma tela**, nunca uma exceção
solta. Para validar sem abrir a Unity há um validador independente em Python (`tools/validate_data.py`) que replica as
regras principais do validador C#; os testes automáticos validam os dados com o validador C# oficial.

> **Importante:** estes JSON são **configuração**, não segurança. Em um cliente, qualquer jogador pode editá-los.
> No jogo online, o **servidor** precisa carregar os próprios dados e validar combate, inventário, ouro, recompensas,
> fabricação e progressão. O cliente só envia intenções.

## 4. Solução de problemas

| Sintoma | Causa provável |
|---|---|
| "Entrada desativada" na tela | *Active Input Handling* está só em Input System (novo). Escolha **Both**. |
| Erro CS0246 sobre `Newtonsoft` | Falta o pacote `com.unity.nuget.newtonsoft-json` (passo 3). |
| Tela "Os dados de jogo são inválidos" | Um JSON foi editado com erro; a tela lista arquivo e campo. Corrija e use **Tentar novamente**. |
| Tela cinza/azul sem terreno | O jogo não iniciou; veja o Console. Confirme que `Assets/Resources/GameData` foi copiado. |
| Personagem "não pôde ser carregado" | Save inválido ou adulterado. O arquivo é mantido; crie outro personagem. |
| Erros de compilação em `Mmorpg.Game` | O código foi escrito sem acesso a um Editor. Envie a mensagem completa para corrigir. |
