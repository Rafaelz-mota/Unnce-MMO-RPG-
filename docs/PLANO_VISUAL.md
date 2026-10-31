# Evolução visual: diagnóstico, plano e o que depende de modelos externos

> Escrito a partir da leitura do repositório. **Nada aqui foi medido em celular nem comparado com a imagem de referência
> da versão do Editor** (a imagem que você citou não veio anexada nesta mensagem; usei o print anterior do Necromante no
> Editor e o código). Onde digo "funciona", é o que os testes automáticos (149, no CI) e a compilação cobrem.

## 1. Diagnóstico da versão atual
**Estrutura:** o projeto não tem cena, prefab, material nem textura salvos. Tudo é montado por código quando você
aperta Play (`GameBootstrap`, `RuntimeInitializeOnLoadMethod`). `Assets/Editor/CiBuild.cs` cria uma cena vazia só para o APK.
Não há `ProjectSettings` completo no repositório (só `ProjectVersion.txt`): o pipeline de renderização (Built-in ou URP)
é o do seu projeto local, e o código evita depender dele (`Prims` copia o material do primitivo e só troca a cor).

| Área | Situação | Como sei |
|---|---|---|
| Combate, 216 habilidades, status, alvo, travar mira | **Funcional** no núcleo | testes automáticos |
| Itens, equipar, loja (216 itens), NPC mercador por proximidade, save | **Funcional** no núcleo | testes automáticos |
| Região I: monstros, chefe, respawn, bonecos de treino | **Funcional** no núcleo | testes automáticos |
| Camada Unity (HUD, câmera, entrada, mundo) | Compila contra stubs; você já abriu sem erro | CI + seu relato; qualidade não avaliada |
| Personagem, monstros, NPC, árvores, ruínas, arena | **Provisório**: formas simples (cubos, esferas, cilindros) | código |
| Armas e armaduras visíveis | Representação provisória por forma; a lógica de equipar é real | código |
| Efeitos de habilidade | Provisório (primitivas), mas únicos por habilidade | código + testes do estilo |
| Animação | Balanço por código (andar, golpe); sem Animator real | código |
| Android / APK | **Nunca testado** | nada foi executado |

## 2. Arquivos envolvidos
- Montagem do mundo e personagens: `Game/GameBootstrap.cs`, `WorldView.cs`, `TerrainDecor.cs`, `ModelBuilder.cs`,
  `EntityView.cs`, `EquipmentVisuals.cs`, `ProceduralAnim.cs`, `Prims.cs`, `SkillVfx.cs`, `Fx.cs`.
- Dados de aparência: `Resources/GameData/presentation.json` (classes e itens).
- Entrada de modelos prontos: `Resources/Models/*`, `docs/MODELOS.md`.
- Validação: `Assets/Editor/ModelValidator.cs` (novo, nunca executado) e `Tests/EditMode/PresentationTests.cs`.
- Cena: nenhuma. **Nada foi apagado nem substituído**; as entradas novas só entram se o arquivo existir.

## 3. Melhorias possíveis sem modelos externos
Feitas agora (commit desta etapa): entradas para modelos de vegetação, construções, chefes, NPCs e equipamentos;
perfil leve para celular; validador de orçamento; documentação.
Próximas, ainda sem modelos externos (uma por vez, cada uma com fallback):
1. Aldeia medieval gerada por código (casas com telhado de duas águas, torre, poço, cercas, ponte) ao redor do mercador.
2. Árvores e pedras com malha gerada por código e cores por vértice (bem melhores que esferas empilhadas).
3. Céu em degradê, luz solar e névoa mais cuidadas; chão com textura gerada (ruído) em vez de cor lisa.
4. Mais variação nos bonecos (idade, cabelo, rosto) e animação de idle/ataque por classe.
Limite: sem artista e sem modelagem, isso **não** chega a "realista"; melhora o protótipo.

## 4. O que depende de modelos 3D (ou texturas) externos
Humanoide realista rigado (24 classes ou um corpo base + equipamentos modulares), animações (idle, andar, correr, ataque,
dano, morte), vegetação detalhada, arquitetura medieval, monstros e chefes exclusivos (o Colosso da Raiz etc.),
equipamentos modelados (armas, elmos, peitorais por raridade/tier), texturas PBR, áudio. Todos exigem **licença que permita
uso comercial e redistribuição no APK**, registrada em `docs/ASSETS.md` antes de entrar.

## 5. Plano de execução
| Etapa | O que | Depende de | Estado |
|---|---|---|---|
| 0 | Entradas por id, validador, perfil Android, docs | nada | **feito** (compilado no CI, não testado no Editor) |
| 1 | Aldeia e vegetação geradas por código | nada | próximo |
| 2 | Céu, luz, névoa, chão texturizado | nada | |
| 3 | Piloto: 1 classe com modelo humanoide + Animator | computador, Unity Editor, modelo licenciado | |
| 4 | Chefes e monstros da Região I (Colosso da Raiz primeiro) | modelos | |
| 5 | Equipamentos por tier (armas, elmos, peitorais) | modelos | |
| 6 | LOD, atlas, texturas ASTC, medição de FPS e tamanho do APK em celular real | celular + computador | |

**Rollback:** cada etapa é um commit pequeno; o estado anterior a esta evolução está marcado com a tag
`antes-da-evolucao-visual`. Apagar um prefab de `Resources/Models` devolve o boneco de código sem mexer em código.
