# Visual: estado real, pipeline e o que falta

> **Aviso honesto:** nenhum modelo 3D real, animação de personagem ou efeito artístico está no projeto. Tudo o que
> aparece na tela é feito por código com formas simples (cubos, esferas, cilindros) e é **provisório**. O CI só prova
> que o código **compila contra stubs**; a aparência e o desempenho no Android **dependem de abrir o jogo**.

## Legenda
- **Implementado:** escrito e no repositório.
- **Compilado no CI:** a camada Unity compila contra stubs (0 erros). Não prova que roda.
- **Testado no Editor:** alguém abriu o jogo na Unity e viu. Só vale o que o dono do projeto relatar.

| Item | Implementado | Compilado no CI | Testado no Editor |
|---|---|---|---|
| Catálogo de aparência por dados (`presentation.json`, 24 classes e 17 itens) | sim | sim (e 124 testes no núcleo) | não |
| Silhueta por classe (build, ombros, capa, cabeça, costas, mão secundária) | sim | sim | não |
| Equipamento visível (arma e armadura mudam ao equipar/desequipar) | sim | sim | não |
| Caminho para modelo real (prefab em `Resources`, por classe e por item) | sim | sim | não |
| Animação: andar, golpe, piscar ao dano, morte (procedural) | sim | sim | o dono relatou que abriu sem erro; qualidade não avaliada |
| Efeitos de habilidade por elemento (primitivas) | sim | sim | não |
| Golpe corpo a corpo com rastro em arco e impacto (Golpe Poderoso, etc.) | sim | sim | não |
| Limite de efeitos vivos (`SkillVfx.MaxLive`) | sim (sem pooling) | sim | não |
| Modelo humanoide 3D real do Guerreiro | **não** | n/a | n/a |
| Animações reais (idle, correr, atacar, morrer) | **não** | n/a | n/a |

## Como funciona (para quem for trocar os visuais)
- `Resources/GameData/presentation.json` liga cada **classe** (id real de `classes.json`) e cada **item** equipável a
  uma aparência. Validado por testes (`PresentationTests`): toda classe precisa de entrada, silhuetas não podem ser só
  cor diferente, valores inválidos viram erro.
- `ModelBuilder` monta o corpo a partir dessa entrada. `EquipmentVisuals` lê `PlayerCharacter.Equipped` (a fonte da
  verdade, que já calcula os atributos) e só troca a aparência; **não altera atributos, inventário nem save**.
- `SkillVfx`/`Fx` só **leem eventos** do núcleo (`SkillUsed`, `StatusApplied`...). Nunca decidem dano, alcance ou cura.
- Sem prefab, o jogo usa primitivas. Com prefab, o campo `prefab` (caminho dentro de `Resources`, sem extensão) passa a
  ser instanciado no lugar. As animações procedurais **não** substituem as de um modelo real: um modelo rigado precisaria
  de um Animator Controller (ainda não existe no projeto).

## O que é preciso para o Guerreiro ficar com cara de MMORPG (etapa que exige computador)
1. Um **modelo humanoide rigado** (FBX ou prefab) com licença que permita uso comercial e redistribuição no APK
   (por exemplo CC0 ou licença equivalente). **Verifique e registre a licença antes de adicionar**; não baixei nenhum.
2. Animações: idle, andar/correr, ataque, reação a dano e morte (do mesmo pacote ou compatíveis com o rig).
3. Colocar em `client/Assets/Resources/Models/...`, preencher `prefab` do Guerreiro em `presentation.json` e criar o
   Animator Controller no Unity Editor. Medir o tamanho do APK e o desempenho em um celular real.
4. Registrar origem, licença, restrições, tamanho e compatibilidade com Android de **cada** asset em `docs/ASSETS.md`
   (arquivo ainda não criado; crie quando o primeiro asset entrar).

## Limites conhecidos
- Cada primitiva usa um material próprio (corpos) ou compartilhado por cor (cenário); há risco de muitas chamadas de
  desenho no celular. Sem pooling de efeitos: apenas um teto de efeitos vivos.
- A imagem de referência das 24 classes não foi anexada; as silhuetas seguem só o texto de `classes.json`.
- NPCs de missão, multiplayer e as outras 7 regiões **não** existem.
