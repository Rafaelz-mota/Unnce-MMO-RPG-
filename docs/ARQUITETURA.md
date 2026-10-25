# Arquitetura, limitações e pendências (vertical slice)

Este documento descreve **o que existe hoje**, como as peças se separam para permitir multiplayer no futuro e o que
ainda não está pronto. Foi escrito a partir do código e dos resultados do CI, não do plano.

## 1. Camadas

| Camada | Pasta | Depende de Unity? | Como é verificada |
|---|---|---|---|
| Dados | `client/Assets/Resources/GameData/*.json` | Não | Validador C# (`GameDataValidator`) nos testes e no início do jogo; validador independente `tools/validate_data.py` no CI |
| Núcleo | `client/Assets/Scripts/Core` | **Não** (`noEngineReferences: true`) | Compilado em .NET puro no CI; 117 testes |
| Camada Unity | `client/Assets/Scripts/Game` | Sim | Compilada contra stubs mínimos da UnityEngine no CI (pega erros de sintaxe e de uso da API do núcleo). **Nunca executada em um Editor.** |

O núcleo não conhece rede, tela, câmera nem arquivos de cena. Por isso o mesmo código pode rodar dentro de um servidor
.NET no futuro, sem copiar regras.

## 2. Fluxo de um comando (e onde entra o servidor)

```
Teclado / toque  ->  Game (PlayerInput, CameraRig)  ->  GameSession  ->  WorldSimulation / CombatWorld / PlayerCharacter
                                                                         |
HUD, efeitos, log  <-  Game (views)  <-  eventos (GameEvent)  <----------+
```

`GameSession` é a **única porta de entrada** dos comandos do jogador (mover, selecionar alvo, usar habilidade,
aprender habilidade, equipar, salvar). Quem decide o resultado é o núcleo: a camada Unity só traduz entrada em chamadas
e desenha os eventos devolvidos.

No jogo online, esses comandos viram **mensagens do cliente para o servidor**. O servidor roda a mesma simulação
(`WorldSimulation`) e é a fonte da verdade. O cliente passa a mostrar o estado recebido, sem decidir dano, ouro, itens
ou progressão.

> **Hoje tudo roda no mesmo processo, na máquina do jogador.** Nada impede um cliente modificado de burlar as regras
> neste protótipo. Isso é aceitável apenas porque ele é offline.

### O que o servidor precisará revalidar (já existe no núcleo, falta o servidor)

| Área | Validação já implementada no núcleo | Teste |
|---|---|---|
| Uso de habilidade | Habilidade desconhecida, não aprendida, conjurador morto, alvo inválido, fora de alcance, recarga, recarga global, recurso insuficiente, já conjurando, atordoado, enraizado (mobilidade), silenciado | `CombatTests` |
| Movimento | Vetor limitado a comprimento 1 (não dá para correr mais rápido enviando vetor maior); passo de simulação limitado a 0,25 s | `VerticalSliceTests` |
| Controle de grupo | Duração máxima por tipo, imunidade temporária após o controle, resistência de chefe (imune a atordoamento, lentidão pela metade) | `CombatTests` |
| Inventário e ouro | Adicionar devolve o resto exato; remover é atômico; ouro nunca negativo e limitado a um teto | `StatsItemsTests`, `CharacterTests` |
| Equipamento | Respeita nível, classe e slot | `StatsItemsTests` |
| Habilidades pagas | Cobra o custo exato do JSON, confere nível e ouro, recusa repetição | `CharacterTests`, `VerticalSliceTests` |
| Recompensas | Só recebe quem contribuiu o mínimo configurado (dano e cura); jogador muito acima do nível do monstro recebe XP reduzido sem ouro nem itens; limite de itens por raridade por morte; inventário cheio perde o item e avisa | `RewardTests` |
| Reaparecimento | Monstros pelo tempo da zona; chefe regional 3600 s contados da morte | `WorldSimulationTests` |
| Trilha de auditoria | O abate registra quem recebeu o quê (em memória) | `RewardTests` |

## 3. Os JSON são configuração, não segurança

Os valores de balanceamento ficam nos JSON, não no código, para poder ajustar sem recompilar. Em um cliente, qualquer
jogador pode editá-los. Por isso:

- O validador protege contra **erro de quem edita** (chave digitada errada, id repetido, referência a item
  inexistente, violação das regras do projeto como mudar o custo de uma habilidade paga ou o respawn do chefe).
- Ele **não** protege contra um jogador mal-intencionado. No online, o **servidor carrega os próprios dados** e o
  cliente nunca é fonte de regra.
- O save local (`*.character.json`) é recusado quando o estado é impossível, mas é um arquivo editável: mudar o ouro
  para outro valor plausível não é detectado. Não é anti-cheat.

## 4. O que está implementado e verificado

Verificado pelo CI no commit `8cb16f2`: dados válidos (0 erros), núcleo compila sem avisos, **117 de 117 testes**
passam, camada Unity compila contra stubs.

| Requisito do pedido | Situação |
|---|---|
| Estrutura de pastas e JSON de habilidades, itens, monstros, atributos, raridades e drops, com leitura e validação | Pronto. 13 arquivos de dados; erros listados sem lançar exceção solta |
| Controles e câmera para PC e Android | Lógica (zona morta, joystick, movimento relativo à câmera, layout de toque para telas comuns) testada no núcleo. A camada Unity que a usa **não foi executada** |
| Combate e habilidades configuráveis por dados | Pronto e testado: dano, cura, escudo, dano no tempo, avanço (dash), controles com limite, recarga, custo, tempo de conjuração com interrupção por movimento, área com limite de alvos |
| Estrutura para 24 classes e 216 habilidades | As 24 classes existem (12 por facção) e todas podem ser criadas e iniciar uma sessão. O validador conta habilidades e as regras de slot (1 a 5 gratuitas; 6 a 9 por 10.000, 20.000, 30.000 e 40.000 de ouro) |
| Região I parcial | Mapa com limites, obstáculos, ponto de nascimento do jogador e 7 zonas de monstros |
| Monstros, recompensas e Colosso da Raiz | 6 monstros (3 comuns, 1 incomum, 1 raro, 1 de elite) e o chefe regional, com 2 fases, ataque sinalizado antes de acertar, resistência a controle, tabela de saque e reaparecimento de 60 minutos |
| Atributos, equipamentos, inventário e progressão | Atributos derivados, nível 1 a 55, equipar com regras, bônus de conjunto, inventário, ouro, XP, save e carga |
| Testes, tratamento de erros, documentação | 117 testes; erros de dados e de save viram relatórios; este documento e `EXECUTAR.md` |

## 5. Limitações conhecidas

- **A camada Unity nunca rodou em um Editor.** O CI só prova que ela compila contra stubs. Pode haver erros de execução,
  diferenças de versão da Unity ou problemas de visual que só aparecem ao abrir o projeto.
- **O projeto Unity nunca foi aberto direto da pasta `client/`.** Existem `Packages/manifest.json` e
  `ProjectSettings/ProjectVersion.txt` (versão `6000.0.34f1`, escolhida para o build automático), mas os demais arquivos
  de `ProjectSettings/` não estão versionados e a Unity os cria na primeira abertura. O caminho mais seguro continua
  sendo criar um projeto novo e copiar as pastas (passo a passo em `EXECUTAR.md`). É preciso configurar
  *Active Input Handling* como *Both*.
- **Android não foi testado** (nem toque real, nem desempenho). Há um build automático de APK
  (`.github/workflows/android-apk.yml`, `client/Assets/Editor/CiBuild.cs`, guia em `APK.md`) que **nunca foi executado**:
  exige uma licença Unity Personal gerada em um computador.
- **O visual é provisório:** o mundo é montado com formas primitivas; não há modelos, animações, efeitos nem áudio
  finais. Os campos de animação, VFX e SFX nos dados são identificadores de apresentação sem assets.
- **Sem servidor e sem multiplayer:** não existem rede, persistência no servidor, sincronização, reconexão, ferramentas
  de administração nem monitoramento.
- **Sem anti-cheat** no cliente (ver seção 3).

## 6. Conteúdo ainda não desenvolvido

| Item | Estado |
|---|---|
| Habilidades de classe | 28 de 216 (guerreiro, mago, sacerdote, arqueiro, assassino e bruxo, parciais). 18 classes sem nenhuma habilidade |
| Tipos de efeito ausentes | Invocações (e seu limite) e grupo/party. Já existem: buffs de atributo (status `war_cry`, só 1 exemplo), tabela de ameaça, controles e provocação (`taunt`, só contra monstros; 1 habilidade: Desafio do Campeão) |
| Regiões | 1 de 8 implementada; chefes das outras 7 não existem |
| Monstros | Categorias lendário e único existem nos dados mas sem criaturas; modificadores (Ígneo, Glacial etc.) não existem; surgimento no mundo aberto por evento não existe |
| Equipamentos | 24 itens e 1 conjunto; sem itens lendários e únicos fabricáveis |
| Sistemas | Mineração, Crafter, montarias, missões e NPCs, masmorras, leilão e trocas, PvP em zonas (a regra de hostilidade entre facções existe no combate, desligada por padrão) |
| Economia | Sem sorvedouros de ouro além dos custos das habilidades pagas; sem simulação de inflação |
| Balanceamento | Valores são ponto de partida; não houve teste de equilíbrio entre classes nem de PvP |

## 7. Próximos passos técnicos sugeridos

1. Abrir o projeto em um Editor Unity, seguir `EXECUTAR.md`, jogar a Região I e corrigir o que aparecer (é o maior
   risco aberto). Enviar o texto completo de qualquer erro do Console.
2. Versionar `Packages/manifest.json` e `ProjectSettings/` depois que o projeto abrir sem erros, para o clone abrir direto.
3. Adicionar os tipos de efeito ausentes (invocação com limite e grupo) antes de escrever mais habilidades,
   já que várias classes (invocador, necromante, domador de feras, bardo) dependem deles.
4. Criar `server/` com um host .NET que reutilize o núcleo e receba os comandos de `GameSession` como mensagens.
5. Completar as habilidades classe a classe (9 por vez), com testes por habilidade.
