using System.Collections.Generic;
using Mmorpg.Core.Data;

namespace Mmorpg.Core.Stats
{
    /// <summary>
    /// Calcula atributos a partir dos dados. Fórmulas (todos os números vêm de attributes.json,
    /// classes.json, items.json, sets.json e combat.json):
    ///   primário  = base da classe + porNível * (nível-1) + itens + bônus de conjunto
    ///   derivado  = base + porNível * (nível-1) + Σ(porPrimário * primário) + itens + bônus de conjunto
    ///   monstro   = escala por nível (combat.monsterScaling) * multiplicadores de categoria e do monstro
    /// </summary>
    public static class StatCalculator
    {
        public static StatSheet ComputePlayer(GameData d, ClassDef cls, int level,
            IEnumerable<ItemDef> equipment, IEnumerable<SetBonusDef> setBonuses)
        {
            var sheet = new StatSheet();
            int steps = level - 1;

            foreach (string p in d.PrimaryStatIds)
            {
                double b = 0, pl = 0;
                cls.PrimaryBase.TryGetValue(p, out b);
                cls.PrimaryPerLevel.TryGetValue(p, out pl);
                sheet.Set(p, b + pl * steps);
            }

            // Bônus planos (itens e conjuntos) entram antes dos derivados para que
            // atributos primários de itens também alimentem os derivados.
            var flat = new StatSheet();
            if (equipment != null)
                foreach (ItemDef item in equipment)
                    foreach (var kv in item.Stats) flat.Add(kv.Key, kv.Value);
            if (setBonuses != null)
                foreach (SetBonusDef bonus in setBonuses)
                    foreach (var kv in bonus.Stats) flat.Add(kv.Key, kv.Value);

            foreach (string p in d.PrimaryStatIds) sheet.Add(p, flat.Get(p));

            foreach (DerivedStatDef def in d.Attributes.Derived)
            {
                double v = def.Base + def.PerLevel * steps;
                foreach (var kv in def.PerPrimary) v += kv.Value * sheet.Get(kv.Key);
                v += flat.Get(def.Id);
                sheet.Set(def.Id, v);
            }
            return sheet;
        }

        public static StatSheet ComputeMonster(GameData d, MonsterDef m)
        {
            MonsterCategoryDef cat = d.MonsterCategories[m.CategoryId];
            MonsterScalingConfig ms = d.Combat.MonsterScaling;
            var sheet = new StatSheet();
            double attack = (ms.AttackBase + ms.AttackPerLevel * m.Level) * cat.DamageMultiplier * m.DamageMultiplier;
            sheet.Set("maxHp", (ms.HpBase + ms.HpPerLevel * m.Level) * cat.HpMultiplier * m.HpMultiplier);
            sheet.Set("maxMana", 0);
            sheet.Set("physAttack", attack);
            sheet.Set("magicAttack", attack);
            sheet.Set("physDefense", ms.DefenseBase + ms.DefensePerLevel * m.Level);
            sheet.Set("magicResist", ms.ResistBase + ms.ResistPerLevel * m.Level);
            sheet.Set("critChance", ms.CritChance);
            sheet.Set("critMultiplier", ms.CritMultiplier);
            sheet.Set("moveSpeed", m.MoveSpeed);
            sheet.Set("hpRegen", 0);
            sheet.Set("manaRegen", 0);
            return sheet;
        }

        /// <summary>Aplica statMods de efeitos ativos: (valor + Σadd) * Πmult. Devolve uma nova ficha.</summary>
        public static StatSheet ApplyMods(StatSheet baseSheet, IEnumerable<StatusDef> activeDefs)
        {
            var add = new Dictionary<string, double>();
            var mult = new Dictionary<string, double>();
            bool any = false;
            foreach (StatusDef def in activeDefs)
            {
                foreach (var kv in def.StatMods)
                {
                    any = true;
                    double a, m;
                    add.TryGetValue(kv.Key, out a);
                    if (!mult.TryGetValue(kv.Key, out m)) m = 1.0;
                    add[kv.Key] = a + kv.Value.Add;
                    mult[kv.Key] = m * kv.Value.Mult;
                }
            }
            if (!any) return baseSheet;
            StatSheet result = baseSheet.Clone();
            foreach (var kv in add) result.Set(kv.Key, (baseSheet.Get(kv.Key) + kv.Value) * mult[kv.Key]);
            foreach (var kv in mult) if (!add.ContainsKey(kv.Key)) result.Set(kv.Key, baseSheet.Get(kv.Key) * kv.Value);
            return result;
        }
    }
}
