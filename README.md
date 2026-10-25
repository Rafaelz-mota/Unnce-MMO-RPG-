# MMORPG 3D de Mundo Aberto (nome provisório)

MMORPG 3D para Android e PC com duas facções rivais, 24 classes, 8 regiões e nível máximo 55.

**Status:** Etapa 1, vertical slice **offline** da Região I (Vale do Despertar). Isto **não** é um MMORPG online:
não há servidor, rede nem multiplayer ainda.

| O que | Situação |
|---|---|
| Núcleo do jogo (dados, atributos, combate, monstros, chefe, recompensas, inventário, progressão, save) | Implementado e testado: 113 testes automáticos passando no CI |
| Camada Unity (controles de PC e toque, câmera, mundo procedural, HUD) | Escrita e **compilada contra stubs** no CI; **nunca aberta nem executada em um Editor Unity** |
| Servidor, rede, multiplayer, PvP em zonas | Não existe |

Detalhes, limitações e pendências em [docs/ARQUITETURA.md](docs/ARQUITETURA.md).

## Escopo do projeto

- 2 facções (12 classes cada), 24 classes e 216 habilidades
- 8 regiões, 8 chefes regionais (respawn de 60 min), monstros lendários e únicos no mundo aberto
- 5 raridades de equipamento, conjuntos, mineração, Crafter, montarias
- Masmorras, PvP, leilão e trocas, servidor autoritativo

## Estrutura do repositório

```
docs/                                 GDD, como executar, arquitetura
client/Assets/Scripts/Core/           Núcleo em C# puro, sem Unity (também compilado em .NET no CI e reutilizável no servidor)
client/Assets/Scripts/Game/           Camada Unity: entrada, câmera, views, HUD
client/Assets/Resources/GameData/     Dados de balanceamento em JSON (a fonte única, não fica no código)
client/Assets/Tests/EditMode/         Testes (NUnit / Unity Test Runner)
ci/                                   Projetos .NET que compilam o núcleo e rodam os testes fora da Unity
tools/validate_data.py                Validador independente dos JSON
.github/workflows/core-ci.yml         CI: valida dados, compila, testa
```

Não existe pasta `server/` nem `data/` na raiz: os dados ficam em `client/Assets/Resources/GameData`.

## Documentação

- [Como executar e testar](docs/EXECUTAR.md)
- [Arquitetura, limitações e pendências](docs/ARQUITETURA.md)
- [GDD v0.1](docs/GDD_v0.1_MMORPG.md)

## Roadmap

1. Fundação técnica e vertical slice (em andamento: núcleo pronto, falta validar a camada Unity em um Editor)
2. Combate e classes
3. Mundo e regiões
4. Monstros e chefes
5. Equipamentos e profissões
6. Montarias, missões e masmorras
7. Multiplayer e economia
8. Otimização e testes

## Licença

A definir.
