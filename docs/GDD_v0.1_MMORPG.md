# GDD v0.1: MMORPG 3D de Mundo Aberto (Etapa 1: Fundação)

Status: documento de design inicial. Nada aqui está implementado ainda. Valores numéricos são propostas configuráveis, a validar em testes.

## 1. Decisões técnicas

| Item | Decisão | Motivo |
|---|---|---|
| Motor | Unity (LTS atual) + C# | Build único para Android e PC, URP para desempenho, grande ecossistema |
| Render | URP com perfis Mobile e PC | Sombras, partículas e LOD ajustáveis por plataforma |
| Rede | Servidor dedicado autoritativo em .NET, protocolo sobre UDP confiável (ex.: LiteNetLib) | Validação de combate, loot, inventário e Gold no servidor |
| Persistência | PostgreSQL (contas, personagens, inventário, log de transações) + Redis (sessão, cache, cooldowns de respawn) | Auditoria e recuperação |
| Dados de jogo | JSON/ScriptableObjects versionados (habilidades, itens, monstros, receitas, tabelas de saque) | Tudo configurável sem recompilar (seção 25 do prompt) |
| Estrutura do mundo | Mundo dividido em zonas/células com streaming; interesse por área (AOI) | Carregamento progressivo e sincronização escalável |

Princípio de rede: o cliente envia intenções (mover, usar habilidade, fabricar); o servidor valida e decide dano, drops, itens, Gold e fabricação.

## 2. Mitologia: Cisma das Oito Regiões (esqueleto)

- **Origem:** o Coração Primordial, uma fonte de energia criadora, sustentava um continente único. Oito Guardiões Primordiais moldaram o mundo, cada um dando forma a uma região.
- **O Cisma:** quando o Coração começou a falhar, os Guardiões divergiram sobre o remédio. Uns queriam selá-lo, outros queriam consumi-lo. O continente se partiu em oito regiões ligadas por rotas, passagens e portais ancestrais.
- **Chefes regionais:** os Guardiões (ou seus avatares) corrompidos pelo conflito, cada um preso ao território que moldou.
- **Minérios e fabricação:** o minério é energia do Coração cristalizada. Quanto mais fundo na progressão, mais pura (cobre, ferro, prata... fragmentos primordiais). Equipamentos ancestrais são forjados com essa energia.
- **Facções:** duas leituras do Cisma, descritas abaixo.
- **Monstros lendários e únicos:** ecos do Coração que ganharam identidade própria; surgem em pontos de ruptura do mundo.
- **As 24 classes:** disciplinas ensinadas por escolas nascidas do Cisma; cada facção herdou 12.

Revelação gradual: diálogos de NPCs, diários e ruínas, missões de região, eventos mundiais e descobertas secretas.

## 3. Facções

### Concórdia do Alvorecer (nome provisório)
Defende selar o Coração e restaurar o equilíbrio. Cultura de ordem, aliança e tradição. Capital inicial: **Alvorada** (Vale do Despertar, lado leste).

### Legião do Crepúsculo (nome provisório)
Defende dominar a energia do Coração para sobreviver ao colapso. Cultura de pragmatismo, pactos e poder. Capital inicial: **Cinzeiro** (Vale do Despertar, lado oeste).

Regra de equilíbrio: nenhuma facção recebe bônus permanente de atributo. Recursos, missões, equipamentos temáticos e territórios disputáveis são equivalentes. Recompensas de PvP escalam para não favorecer a facção mais populosa (bônus de "azarão" configurável).

### Divisão proposta das 24 classes (12 e 12)

| Função | Concórdia do Alvorecer | Legião do Crepúsculo |
|---|---|---|
| Tanque / proteção | Guerreiro, Guardião, Paladino | Cavaleiro da Morte, Berserker (espec. tanque) |
| Dano corpo a corpo | Monge, Lanceiro | Assassino, Lâmina Sombria |
| Dano à distância | Arqueiro, Engenheiro | Atirador, Domador de Feras |
| Dano mágico / controle | Mago, Mago Rúnico | Bruxo, Necromante, Ilusionista, Invocador |
| Cura e suporte | Sacerdote, Bardo, Druida | Clérigo, Alquimista |

Total: 12 + 12. Pontos a revisar na etapa de balanceamento: a Legião tem mais magia e invocação, a Concórdia mais tanque e suporte; cada lado precisa de ferramentas comparáveis de sobrevivência, cura e controle. A distribuição é proposta e pode ser trocada antes de implementar.

## 4. Regiões e chefes (resumo)

| # | Região | Níveis | Chefe | Material característico (proposta) |
|---|---|---|---|---|
| I | Vale do Despertar | 1–7 | Colosso da Raiz | Cobre |
| II | Floresta das Brumas | 6–13 | Matriarca do Véu | Ferro, ervas |
| III | Serra de Ferro | 12–20 | Rei da Forja Abandonada | Ferro, prata, hematita |
| IV | Deserto das Cinzas | 19–27 | Dragão das Cinzas | Obsidiana, cristais ígneos |
| V | Pântano dos Sussurros | 26–34 | Senhora do Pântano | Minério negro |
| VI | Tundra dos Antigos | 33–41 | Titã do Inverno | Minério glacial |
| VII | Terras do Abismo | 40–48 | Arauto do Abismo | Minério abissal |
| VIII | Coroa Celestial | 47–55 | Arconte Primordial | Minério astral, fragmentos primordiais |

Chefes regionais: respawn a cada 60 min (configurável). Lendários e únicos: intervalos e condições próprios.

## 5. Raridades e regras de saque (valores iniciais configuráveis)

| Raridade | Fonte principal | Observações |
|---|---|---|
| Comum | Monstros comuns/incomuns, fabricação inicial | Base de materiais e economia |
| Raro | Monstros comuns (baixa chance), raros, Aprendiz | |
| Épico | Elites, campeões, raros (chance controlada), Avançado/Especialista | |
| Lendário | Chefes regionais, lendários, Especialista/Mestre | |
| Único | Chefes (chance baixa) ou fabricação especial (Mestre) | Efeitos com recarga e condições; não garantidos |

Regras: toda taxa de drop vive em tabelas de dados por criatura; toda fabricação exige nível de profissão, receita e materiais; log de auditoria em todo evento de item/Gold; limite de recompensas por encontro e por conta; contribuição mínima para elegibilidade de loot; anti-monopólio em lendários e únicos.

## 6. Progressão e economia (diretrizes)

- Nível máximo 55; curva de XP com metas em todos os níveis; XP vem de monstros, missões, exploração e masmorras.
- Habilidades 1–5 gratuitas; 6 = 10.000 Gold; 7 = 20.000; 8 = 30.000; 9 = 40.000 (valores fixos do prompt).
- Economia: tabela de fontes e sorvedouros de Gold (reparo, taxas de leilão, fabricação, serviços) monitorada por métricas.

## 7. Plano da vertical slice (Etapa 1 → 2)

Meta: protótipo jogável antes de expandir.
1. Projeto Unity base, câmera e controles PC (WASD + mouse) e Android (analógico virtual + botões).
2. Personagem com HP/mana, atributos e nível.
3. Sistema de habilidades orientado a dados (carregado de JSON), com 3 classes de teste (Guerreiro, Mago, Arqueiro) e suas 5 primeiras habilidades.
4. Região I parcial com monstros comuns, um chefe (Colosso da Raiz) e loot simples.
5. Inventário básico e testes automatizados do sistema de habilidades e de loot.

Só depois: catálogo completo das 216 habilidades (20 campos cada, 9 classes por rodada), servidor autoritativo e demais etapas.

## 8. Pendências para confirmar

- Confirmar Unity como motor (ou trocar para Unreal/Godot).
- Aprovar ou ajustar a divisão 12/12 e os nomes das facções.
- Definir se o prototipo inicial será offline (local) antes do servidor, como sugerido.
