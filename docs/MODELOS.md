# Como colocar modelos 3D prontos no jogo

> **Estado honesto:** o código desta entrada está escrito e compila no CI contra stubs, mas **nunca foi testado com um
> modelo real na Unity**. Nenhum modelo está no projeto. O primeiro modelo que você colocar é também o primeiro teste.

## Resumo
Sem alterar código nem JSON: **salve o prefab com o id do personagem na pasta certa** e o jogo o usa no lugar do
boneco de formas simples. Se o arquivo não existir, continua o boneco provisório.

| O quê | Onde (dentro de `client/Assets/Resources/`) | Nome do arquivo |
|---|---|---|
| Classe jogável | `Models/Classes/` | `<id da classe>.prefab` (ex.: `guerreiro.prefab`, `mago_runico.prefab`) |
| Monstro | `Models/Monsters/` | `<id do monstro>.prefab` (ex.: `lobo_selvagem.prefab`, `boneco_de_treino.prefab`) |
| NPC | `Models/Npcs/` | `<id do npc>.prefab` (ex.: `mercador_orvalho.prefab`) |

Os ids estão em `classes.json`, `monsters.json` e nas regiões (`regions.json`, lista `npcs`).
Alternativa para classes: no `presentation.json`, o campo `prefab` da classe aceita outro caminho dentro de `Resources`
(sem extensão); ele tem prioridade sobre a pasta.

| Vegetação / pedras | `Models/Props/` | `tree_0.prefab`, `tree_1.prefab`, `tree_2.prefab`, `rock.prefab` |
| Construções | `Models/Buildings/` | `ruin.prefab` (obstáculo "ruin"), `camp.prefab` (nascimento), `arena.prefab` (arena do chefe) |
| Equipamento | `Models/Items/` | `<id do item>.prefab` (ex.: `loja_sword_t3.prefab`); vale para armas, elmos e peitorais |

Chefes usam a pasta de monstros: o prefab do Colosso da Raiz é `Models/Monsters/colosso_da_raiz.prefab`.
Árvores e pedras reais entram no lugar de **todas** as árvores/pedras do mapa que usam aquela variante; a colisão
continua sendo o círculo (`radius`) do obstáculo em `regions.json`, então modele a base do tronco com esse raio.
Pernas e botas equipadas só aparecem em modelos rigados (a armadura "calça/bota" do boneco de código não tem
equivalente por prefab ainda).

## Orçamento para Android (usado pelo validador)
| Pasta | Triângulos (máx.) | Altura esperada |
|---|---|---|
| Classes / Npcs | 15 000 | 1,5 a 2,6 m (classes) |
| Monsters | 20 000 | 0,3 a 12 m |
| Props | 6 000 | 0,2 a 30 m |
| Buildings | 30 000 | 1 a 60 m |
| Items | 3 000 | 0,05 a 3 m |

- Malha acima de 5 000 triângulos deve ter **LODGroup** (LOD0 / LOD1 a ~50% / LOD2 a ~25%).
- **Materiais PBR:** URP/Lit (ou Standard no Built-in), 1 a 3 por modelo, com atlas. Mapas: Base (albedo), Normal,
  Metallic/Smoothness juntos. Texturas até 2048 px (1024 para props e itens), compressão **ASTC** no Android.
- Para conferir tudo isso no Editor: menu **Mmorpg > Validar modelos (Resources/Models)**. Ele só lê e imprime avisos no Console.

## Regras do modelo
- **Escala:** 1 unidade = 1 metro. Humanoide com cerca de **1,8 a 2,2 m**, pés em `y = 0`, olhando para `+Z`.
  Para ajustar o tamanho por classe use `height` no `presentation.json`; por monstro, `scale` em `monsters.json`.
- **Origem:** no chão, entre os pés.
- **Material:** um material simples (URP/Lit) por modelo, de preferência com atlas de textura, para poucos draw calls
  no celular. Meta: até ~10 mil triângulos por personagem.

## Encaixes de equipamento (só classes)
Crie objetos vazios (filhos do esqueleto) com estes nomes exatos. O jogo prende armas e armaduras neles:

| Nome | Para quê |
|---|---|
| `hand_r` | arma da mão direita |
| `hand_l` | arma/escudo da mão esquerda (arcos) |
| `head` | elmo/capuz equipado |
| `chest` | armadura de peito |

Sem eles, o corpo aparece, mas armas e armaduras equipadas não ficam visíveis.

## Animação (opcional)
Se o prefab tiver um **Animator** com controlador, o jogo para de balançar o corpo por código e passa a controlar o
Animator com estes parâmetros (todos opcionais; use só os que o seu controlador tiver):

| Parâmetro | Tipo | Quando |
|---|---|---|
| `Speed` | float | velocidade horizontal real (m/s); use para idle/andar/correr |
| `Attack` | trigger | ao usar uma habilidade |
| `Hit` | trigger | ao receber dano |
| `Dead` | bool | `true` ao morrer, `false` ao renascer |

Se o controlador **não** tiver `Dead`, o jogo deita o corpo por código ao morrer, como no boneco provisório.

## Passo a passo (exige o Unity Editor, em computador)
1. Escolha um modelo humanoide rigado com **licença que permita uso comercial e redistribuição no APK** (CC0 ou
   equivalente). **Verifique e registre a licença antes de adicionar.** Eu não baixei nenhum.
2. Importe o FBX/glTF, monte o prefab com o Animator e os encaixes acima.
3. Salve em `Resources/Models/Classes/<id>.prefab`, aperte Play e veja.
4. Registre origem, licença, tamanho e restrições de cada asset em `docs/ASSETS.md` (crie o arquivo no primeiro asset).
5. Meça tamanho do APK e FPS em um celular real.

## Limites conhecidos
- Humanoides compatíveis com o rig do Mixamo/Unity Humanoid funcionam melhor; o jogo não faz retarget de animações.
- Efeitos das habilidades continuam sendo formas de código; só o corpo é trocado.
- Um prefab de monstro não tem encaixes; a barra de vida e o nome ficam a ~2 m de altura, ajustável por `scale`.
