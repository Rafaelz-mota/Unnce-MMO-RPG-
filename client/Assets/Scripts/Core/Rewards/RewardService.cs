using System;
using System.Collections.Generic;
using System.Linq;
using Mmorpg.Core.Characters;
using Mmorpg.Core.Combat;
using Mmorpg.Core.Common;
using Mmorpg.Core.Data;
using Mmorpg.Core.Events;
using Mmorpg.Core.Progression;

namespace Mmorpg.Core.Rewards
{
    /// <summary>Registro de auditoria de uma recompensa concedida (quem, o quê, de qual monstro, quando).</summary>
    public sealed class AuditEntry
    {
        public double Time;
        public string Kind;     // xp | gold | item | item_skipped
        public string Player;
        public string MonsterId;
        public string ItemId;
        public long Amount;
        public string Note;

        public override string ToString()
        {
            return "[" + Time.ToString("0.0") + "] " + Kind + " " + Player + " <- " + MonsterId
                + (ItemId != null ? " " + ItemId : "") + " x" + Amount + (Note != null ? " (" + Note + ")" : "");
        }
    }

    /// <summary>
    /// Distribui XP, ouro e itens quando um monstro morre. Regras (todas lidas dos JSON):
    ///  - só recebe quem contribuiu pelo menos 'minShare' do total (dano + cura ponderada), podendo ser sobrescrito por tabela;
    ///  - jogador muito acima do nível do monstro (lootCutoffLevels) recebe só o XP reduzido, sem ouro nem itens;
    ///  - cada tabela limita itens por jogador (maxItemsPerPlayer) e itens por raridade em toda a morte (rarityCapsPerKill);
    ///    quem mais contribuiu é servido primeiro;
    ///  - item que não cabe no inventário NÃO cai no chão nesta versão: é perdido e registrado (limitação conhecida).
    /// No jogo online esta lógica rodará no servidor; o cliente só recebe os eventos.
    /// </summary>
    public sealed class RewardService
    {
        private const int MaxAuditEntries = 5000;

        private readonly CombatWorld _world;
        private readonly IRng _rng;
        private readonly List<AuditEntry> _audit = new List<AuditEntry>();

        public RewardService(CombatWorld world, IRng rng)
        {
            if (world == null) throw new ArgumentNullException("world");
            _world = world;
            _rng = rng ?? new SystemRng();
            world.MonsterKilled += OnMonsterKilled;
        }

        public IReadOnlyList<AuditEntry> Audit { get { return _audit; } }

        private void Log(string kind, Combatant player, MonsterDef m, string itemId, long amount, string note)
        {
            if (_audit.Count >= MaxAuditEntries) _audit.RemoveAt(0);
            _audit.Add(new AuditEntry { Time = _world.Time, Kind = kind, Player = player.Name, MonsterId = m.Id, ItemId = itemId, Amount = amount, Note = note });
        }

        private void OnMonsterKilled(Combatant monster, Combatant killer)
        {
            GameData d = _world.Data;
            MonsterDef def = monster.MonsterDef;
            LootTableDef table = d.LootTables[def.LootTableId];

            // Participantes ainda presentes no mundo, do maior para o menor contribuidor.
            var contributors = new List<KeyValuePair<Combatant, double>>();
            double total = 0;
            foreach (KeyValuePair<int, double> kv in monster.Contribution)
            {
                Combatant p = _world.Get(kv.Key);
                if (p == null || !p.IsPlayer || kv.Value <= 0) continue;
                contributors.Add(new KeyValuePair<Combatant, double>(p, kv.Value));
                total += kv.Value;
            }
            if (total <= 0) return;
            contributors.Sort((a, b) => b.Value.CompareTo(a.Value));

            double minShare = table.MinContributionShare ?? d.Progression.Contribution.MinShare;
            var rarityCounts = new Dictionary<string, int>();

            foreach (KeyValuePair<Combatant, double> entry in contributors)
            {
                if (entry.Value / total < minShare) continue;
                Combatant player = entry.Key;
                GrantXp(player, monster, def);
                if (LevelCurve.IsAboveLootCutoff(d, player.Level, def.Level))
                {
                    Log("item_skipped", player, def, null, 0, "nível acima do corte de saque");
                    continue;
                }
                GrantGold(player, monster, def, table);
                GrantItems(player, monster, def, table, rarityCounts);
            }
        }

        private void GrantXp(Combatant player, Combatant monster, MonsterDef def)
        {
            PlayerCharacter pc = player.Character;
            long xp = LevelCurve.MonsterXp(_world.Data, def, pc.Level);
            int levels = pc.AddXp(xp);
            _world.Emit(new GameEvent { Type = GameEventType.XpGained, SourceId = monster.Id, TargetId = player.Id, Amount = xp });
            Log("xp", player, def, null, xp, null);
            if (levels > 0)
            {
                _world.RefreshPlayerStats(player, _world.Data.Progression.HealFullOnLevelUp);
                _world.Emit(new GameEvent { Type = GameEventType.LevelUp, TargetId = player.Id, Count = levels, Amount = pc.Level, Text = pc.Name });
            }
        }

        private void GrantGold(Combatant player, Combatant monster, MonsterDef def, LootTableDef table)
        {
            if (table.GoldMax <= 0) return;
            double roll = table.GoldMin + _rng.NextDouble() * (table.GoldMax - table.GoldMin + 1);
            long gold = (long)Math.Floor(Math.Min(roll, table.GoldMax) * _world.Data.Progression.GoldMultiplier);
            long added = player.Character.AddGold(gold);
            if (added <= 0) return;
            _world.Emit(new GameEvent { Type = GameEventType.GoldGained, SourceId = monster.Id, TargetId = player.Id, Amount = added });
            Log("gold", player, def, null, added, null);
        }

        private void GrantItems(Combatant player, Combatant monster, MonsterDef def, LootTableDef table, Dictionary<string, int> rarityCounts)
        {
            GameData d = _world.Data;
            int dropped = 0;
            foreach (LootEntryDef e in table.Entries)
            {
                if (table.MaxItemsPerPlayer > 0 && dropped >= table.MaxItemsPerPlayer) break;
                if (_rng.NextDouble() >= e.Chance) continue;
                int qty = _rng.NextInt(e.Min, e.Max + 1);
                ItemDef item = d.Items[e.ItemId];

                int cap;
                if (table.RarityCapsPerKill.TryGetValue(item.Rarity, out cap))
                {
                    int used;
                    rarityCounts.TryGetValue(item.Rarity, out used);
                    if (used + qty > cap)
                    {
                        Log("item_skipped", player, def, item.Id, qty, "teto de raridade por morte");
                        continue;
                    }
                }
                if (!player.Character.Inventory.CanAdd(item.Id, qty))
                {
                    _world.Emit(new GameEvent { Type = GameEventType.InventoryFull, TargetId = player.Id, ItemId = item.Id, Count = qty });
                    Log("item_skipped", player, def, item.Id, qty, "inventário cheio");
                    continue;
                }
                player.Character.Inventory.Add(item.Id, qty);
                int prev;
                rarityCounts.TryGetValue(item.Rarity, out prev);
                rarityCounts[item.Rarity] = prev + qty;
                dropped++;
                _world.Emit(new GameEvent { Type = GameEventType.ItemLooted, SourceId = monster.Id, TargetId = player.Id, ItemId = item.Id, Count = qty });
                Log("item", player, def, item.Id, qty, null);
            }
        }
    }
}
