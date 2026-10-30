using System;
using Mmorpg.Core.Common;
using Mmorpg.Core.Data;
using Mmorpg.Core.Events;

namespace Mmorpg.Core.Combat
{
    /// <summary>Dano, cura, escudos, ameaça e contribuição. Todos os coeficientes vêm de combat.json e progression.json.</summary>
    public sealed partial class CombatWorld
    {
        /// <summary>Soma ameaça de 'player' na lista do monstro. O monstro passa a combater quem tiver mais ameaça.</summary>
        internal void AddThreat(Combatant monster, Combatant player, double amount)
        {
            if (monster == null || player == null || monster.Dead || !monster.IsMonster || amount <= 0) return;
            double cur;
            monster.Threat.TryGetValue(player.Id, out cur);
            monster.Threat[player.Id] = cur + amount;
        }

        /// <summary>Dano de habilidade: escala, variação, crítico, área, PvP, mitigação, escudos.</summary>
        internal double DealSkillDamage(Combatant caster, Combatant target, SkillDef skill, SkillEffectDef effect, double aoeMultiplier)
        {
            double raw = DamageFormula.Scale(effect, caster.Stats);
            double variance = Data.Combat.DamageVariance;
            if (variance > 0) raw *= 1.0 + variance * (Rng.NextDouble() * 2.0 - 1.0);
            if (caster.IsMonster) raw *= caster.PhaseDamageMultiplier;
            raw *= aoeMultiplier;
            if (effect.ExecuteBelowHpPercent > 0 && target.HpPercent < effect.ExecuteBelowHpPercent) raw *= effect.ExecuteMultiplier;
            if (effect.BonusIfStatusId != null && HasStatus(target, effect.BonusIfStatusId)) raw *= effect.BonusMultiplier;
            if (caster.IsPlayer && target.IsPlayer) raw *= Data.Combat.PvpDamageMultiplier * skill.PvpMultiplier;

            bool crit = Rng.NextDouble() < DamageFormula.CritChance(Data, caster.Stats);
            if (crit) raw *= Math.Max(1.0, caster.Stats.Get("critMultiplier"));

            double dealt = ApplyDamage(caster, caster.Id, caster.Level, caster.IsPlayer, target, raw, effect.DamageType, skill.Id, crit);

            if (effect.Leech > 0 && dealt > 0) HealTarget(caster, caster, dealt * effect.Leech, skill.Id);

            if (caster.ResourceId != null && dealt > 0)
            {
                ResourceDef res = Data.Resources[caster.ResourceId];
                if (res.GainOnDealDamage > 0)
                {
                    caster.Resource += res.GainOnDealDamage;
                    caster.LastResourceGain = Time;
                    ClampResource(caster);
                }
            }
            return dealt;
        }

        /// <summary>
        /// Aplica dano já calculado (antes da mitigação). Mitigação, escudos, ameaça, contribuição e morte.
        /// 'source' pode ser nulo (fonte já saiu do mundo); o nível da fonte é guardado no efeito.
        /// Devolve o dano total depois da mitigação (inclui o que o escudo absorveu).
        /// </summary>
        internal double ApplyDamage(Combatant source, int sourceId, int sourceLevel, bool sourceIsPlayer, Combatant target,
            double raw, DamageType type, string skillId, bool crit)
        {
            if (target == null || target.Dead || target.Invulnerable) return 0;
            double mitigation = DamageFormula.Mitigation(Data, target.Stats, sourceLevel, type);
            double amount = Math.Max(1.0, raw * (1.0 - mitigation));

            double left = amount;
            double absorbed = 0;
            StatusInstance[] statuses = target.Statuses.ToArray();
            for (int i = 0; i < statuses.Length && left > 0; i++)
            {
                StatusInstance s = statuses[i];
                if (s.Def.Kind != StatusKind.Shield) continue;
                double soak = Math.Min(s.ShieldRemaining, left);
                s.ShieldRemaining -= soak;
                left -= soak;
                absorbed += soak;
                if (s.ShieldRemaining <= 1e-9) RemoveStatus(target, s);
            }
            if (absorbed > 0)
                Emit(new GameEvent { Type = GameEventType.Absorbed, SourceId = sourceId, TargetId = target.Id, SkillId = skillId, Amount = absorbed });

            double hpBefore = target.Hp;
            target.Hp = Math.Max(0, target.Hp - left);
            if (left > 0)
                Emit(new GameEvent { Type = GameEventType.Damage, SourceId = sourceId, TargetId = target.Id, SkillId = skillId, Amount = hpBefore - target.Hp, Crit = crit, Position = target.Position });

            target.LastCombatTime = Time;
            if (source != null) source.LastCombatTime = Time;

            if (target.IsMonster && sourceIsPlayer && source != null)
            {
                AddThreat(target, source, amount * Data.Combat.Threat.DamageMultiplier);
                double c;
                target.Contribution.TryGetValue(source.Id, out c);
                target.Contribution[source.Id] = c + amount;
            }

            if (target.ResourceId != null && left > 0)
            {
                ResourceDef res = Data.Resources[target.ResourceId];
                if (res.GainOnTakeDamagePerPercent > 0 && target.MaxHp > 0)
                {
                    target.Resource += (hpBefore - target.Hp) / target.MaxHp * 100.0 * res.GainOnTakeDamagePerPercent;
                    target.LastResourceGain = Time;
                    ClampResource(target);
                }
            }

            if (target.Hp <= 0) Kill(target, sourceId);
            return amount;
        }

        internal static bool HasStatus(Combatant c, string statusId)
        {
            for (int i = 0; i < c.Statuses.Count; i++) if (c.Statuses[i].Def.Id == statusId) return true;
            return false;
        }

        /// <summary>Remove status nocivos (aliado) ou benéficos (dispel, inimigo). count 0 = todos.</summary>
        internal int Cleanse(Combatant caster, Combatant target, bool dispel, int count)
        {
            if (target == null || target.Dead) return 0;
            int removed = 0;
            StatusInstance[] copy = target.Statuses.ToArray();
            for (int i = 0; i < copy.Length; i++)
            {
                if (count > 0 && removed >= count) break;
                StatusKind k = copy[i].Def.Kind;
                bool harmful = k == StatusKind.Stun || k == StatusKind.Root || k == StatusKind.Silence || k == StatusKind.Slow ||
                               k == StatusKind.Dot || k == StatusKind.Debuff || k == StatusKind.Taunt;
                bool beneficial = k == StatusKind.Buff || k == StatusKind.Shield || k == StatusKind.Hot;
                if (dispel ? !beneficial : !harmful) continue;
                RemoveStatus(target, copy[i]);
                removed++;
            }
            return removed;
        }

        /// <summary>Cura. Só a cura efetiva (sem overheal) gera ameaça e contribuição, e só para aliados em combate.</summary>
        internal double HealTarget(Combatant healer, Combatant target, double amount, string skillId)
        {
            if (target == null || target.Dead || amount <= 0) return 0;
            double healed = Math.Min(amount, target.MaxHp - target.Hp);
            if (healed <= 0) return 0;
            target.Hp += healed;
            Emit(new GameEvent { Type = GameEventType.Heal, SourceId = healer != null ? healer.Id : 0, TargetId = target.Id, SkillId = skillId, Amount = healed, Position = target.Position });

            if (healer == null || !healer.IsPlayer || healer.Id == target.Id) return healed;
            for (int i = 0; i < _all.Count; i++)
            {
                Combatant m = _all[i];
                if (!m.IsMonster || m.Dead || !m.Threat.ContainsKey(target.Id)) continue;
                AddThreat(m, healer, healed * Data.Combat.Threat.HealingMultiplier);
                double c;
                m.Contribution.TryGetValue(healer.Id, out c);
                m.Contribution[healer.Id] = c + healed * Data.Progression.Contribution.HealingWeight;
            }
            return healed;
        }
    }
}
