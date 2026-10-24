using System;
using System.Collections.Generic;
using Mmorpg.Core.Common;
using Mmorpg.Core.Data;
using Mmorpg.Core.Events;

namespace Mmorpg.Core.Combat
{
    /// <summary>Efeitos de estado: dano/cura ao longo do tempo, escudos, buffs, debuffs e controle de grupo com limites.</summary>
    public sealed partial class CombatWorld
    {
        /// <summary>
        /// Aplica um status. Regras (todas configuráveis em combat.json/statuses.json/monsters.json):
        /// controle respeita duração máxima, resistência do monstro (ccResist) e imunidade temporária após o efeito;
        /// o mesmo status da mesma fonte/habilidade apenas renova; acima de maxInstancesPerTarget substitui o mais antigo.
        /// </summary>
        internal bool ApplyStatus(Combatant source, Combatant target, StatusDef def, double duration, double value,
            DamageType damageType, string skillId)
        {
            if (target == null || target.Dead) return false;
            int sourceId = source != null ? source.Id : 0;
            if (duration <= 0) duration = def.DefaultDuration;
            string key = def.ControlKey;

            if (key != null)
            {
                if (target.Invulnerable) return Resist(sourceId, target, def, "invulnerável");
                double immuneUntil;
                if (target.ControlImmuneUntil.TryGetValue(key, out immuneUntil) && immuneUntil > Time)
                    return Resist(sourceId, target, def, "imune");
                double resist;
                if (target.IsMonster && target.MonsterDef.CcResist.TryGetValue(key, out resist))
                {
                    duration *= resist;
                    if (duration <= 0) return Resist(sourceId, target, def, "resistiu");
                }
                double max;
                if (Data.Combat.Control.MaxDuration.TryGetValue(key, out max)) duration = Math.Min(duration, max);
            }

            StatusInstance existing = null, oldest = null;
            int count = 0;
            for (int i = 0; i < target.Statuses.Count; i++)
            {
                StatusInstance s = target.Statuses[i];
                if (s.Def.Id != def.Id) continue;
                count++;
                if (s.SourceId == sourceId && s.SkillId == skillId) existing = s;
                if (oldest == null || s.ExpiresAt < oldest.ExpiresAt) oldest = s;
            }

            double expiresAt = Time + duration;
            if (existing != null)
            {
                existing.ExpiresAt = expiresAt;
                existing.Value = value;
                if (def.Kind == StatusKind.Shield) existing.ShieldRemaining = value;
            }
            else
            {
                if (count >= Math.Max(1, def.MaxInstancesPerTarget) && oldest != null) RemoveStatus(target, oldest);
                target.Statuses.Add(new StatusInstance
                {
                    Def = def,
                    SourceId = sourceId,
                    SourceLevel = source != null ? source.Level : target.Level,
                    SourceIsPlayer = source != null && source.IsPlayer,
                    SkillId = skillId,
                    AppliedAt = Time,
                    ExpiresAt = expiresAt,
                    NextTickAt = Time + def.TickInterval,
                    Value = value,
                    DamageType = damageType,
                    ShieldRemaining = def.Kind == StatusKind.Shield ? value : 0
                });
            }

            if (key != null)
            {
                double immunity;
                if (Data.Combat.Control.ImmunityAfter.TryGetValue(key, out immunity))
                    target.ControlImmuneUntil[key] = expiresAt + immunity;
            }

            if (def.StatMods.Count > 0) RecomputeEffective(target);
            if (def.Kind == StatusKind.Stun) InterruptCast(target, "atordoado");
            else if (def.Kind == StatusKind.Silence && target.Cast != null && target.Cast.Skill.Silenceable) InterruptCast(target, "silenciado");

            Emit(new GameEvent { Type = GameEventType.StatusApplied, SourceId = sourceId, TargetId = target.Id, StatusId = def.Id, SkillId = skillId, Amount = duration });

            if (source != null && source.Id != target.Id && IsHostile(source, target))
            {
                source.LastCombatTime = Time;
                target.LastCombatTime = Time;
                if (target.IsMonster && source.IsPlayer) AddThreat(target, source, 1.0);
            }
            return true;
        }

        private bool Resist(int sourceId, Combatant target, StatusDef def, string why)
        {
            Emit(new GameEvent { Type = GameEventType.StatusResisted, SourceId = sourceId, TargetId = target.Id, StatusId = def.Id, Text = why });
            return false;
        }

        internal void RemoveStatus(Combatant c, StatusInstance s)
        {
            if (!c.Statuses.Remove(s)) return;
            Emit(new GameEvent { Type = GameEventType.StatusRemoved, TargetId = c.Id, StatusId = s.Def.Id });
            if (s.Def.StatMods.Count > 0) RecomputeEffective(c);
        }

        /// <summary>Remove todos os status (reset de monstro).</summary>
        internal void ClearStatuses(Combatant c)
        {
            if (c.Statuses.Count == 0) return;
            c.Statuses.Clear();
            RecomputeEffective(c);
        }

        private void UpdateStatuses(Combatant c)
        {
            if (c.Statuses.Count == 0) return;
            StatusInstance[] copy = c.Statuses.ToArray();
            for (int i = 0; i < copy.Length; i++)
            {
                StatusInstance s = copy[i];
                if (c.Dead) return;
                if (!c.Statuses.Contains(s)) continue;

                bool ticking = s.Def.TickInterval > 0 && (s.Def.Kind == StatusKind.Dot || s.Def.Kind == StatusKind.Hot);
                if (ticking)
                {
                    while (s.NextTickAt <= Time && s.NextTickAt <= s.ExpiresAt + 1e-9)
                    {
                        s.NextTickAt += s.Def.TickInterval;
                        Combatant src = Get(s.SourceId);
                        if (s.Def.Kind == StatusKind.Dot)
                        {
                            ApplyDamage(src, s.SourceId, s.SourceLevel, s.SourceIsPlayer, c, s.Value, s.DamageType, s.SkillId, false);
                            if (c.Dead) return;
                        }
                        else
                        {
                            HealTarget(src, c, s.Value, s.SkillId);
                        }
                    }
                }
                if (Time >= s.ExpiresAt) RemoveStatus(c, s);
            }
        }
    }
}
