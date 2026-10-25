using System;
using System.Collections.Generic;
using Mmorpg.Core.Common;
using Mmorpg.Core.Data;
using Mmorpg.Core.Events;

namespace Mmorpg.Core.Combat
{
    /// <summary>
    /// IA simples e previsível dos monstros: fica parado até um jogador entrar no alcance de aggro, combate quem tem mais
    /// ameaça, usa a primeira habilidade pronta da lista (a ordem em monsters.json é a prioridade) e volta para casa
    /// (evade: invulnerável, cura e reseta) se o alvo sumir ou se afastar mais que leashRange.
    /// Limitações conhecidas: monstros não desviam uns dos outros nem fazem patrulha.
    /// </summary>
    internal static class MonsterAi
    {
        private const double MaxEvadeSeconds = 15.0;

        public static void Update(CombatWorld w, Combatant m, double dt)
        {
            UpdatePhase(w, m);

            if (m.AiState == AiState.Evade)
            {
                Evade(w, m, dt);
                return;
            }

            if (m.AiState == AiState.Idle)
            {
                Acquire(w, m);
                if (m.AiState == AiState.Idle) return;
            }

            Combatant target = PickTarget(w, m);
            if (target == null || Vec2.Distance(m.Position, m.Home) > m.MonsterDef.LeashRange)
            {
                BeginEvade(w, m);
                return;
            }
            m.TargetId = target.Id;

            if (m.HasControl("stun")) return;
            Vec2 toTarget = (target.Position - m.Position).Normalized();
            if (toTarget.SqrLength > 0.5) m.Facing = toTarget;
            if (m.Cast != null) return; // parado enquanto prepara a habilidade (telegrafia)

            double dist = Vec2.Distance(m.Position, target.Position);
            if (TryUseSkill(w, m, target, dist)) return;

            double stop = ApproachDistance(w, m);
            if (dist > stop && !m.HasControl("root"))
            {
                double speed = m.Stats.Get("moveSpeed") * w.SlowMultiplier(m);
                double step = Math.Min(speed * dt, dist - stop);
                if (step > 0) m.Position = w.Map.Move(m.Position, m.Position + toTarget * step, w.BodyRadius);
            }
        }

        private static void Acquire(CombatWorld w, Combatant m)
        {
            IList<Combatant> all = w.All;
            for (int i = 0; i < all.Count; i++)
            {
                Combatant p = all[i];
                if (p.Dead || !w.IsHostile(m, p)) continue;
                if (Vec2.Distance(m.Position, p.Position) <= m.MonsterDef.AggroRange)
                {
                    w.AddThreat(m, p, 1.0);
                    m.AiState = AiState.Combat;
                }
            }
        }

        /// <summary>Maior ameaça entre alvos válidos; só troca do alvo atual se a ameaça superar a dele pela razão configurada.</summary>
        private static Combatant PickTarget(CombatWorld w, Combatant m)
        {
            Combatant forced = TauntedBy(w, m);
            if (forced != null) return forced;
            Combatant best = null, current = null;
            double bestThreat = 0, currentThreat = 0;
            var stale = new List<int>();
            foreach (KeyValuePair<int, double> kv in m.Threat)
            {
                Combatant p = w.Get(kv.Key);
                if (p == null || p.Dead || !w.IsHostile(m, p)) { stale.Add(kv.Key); continue; }
                if (kv.Key == m.TargetId) { current = p; currentThreat = kv.Value; }
                if (best == null || kv.Value > bestThreat) { best = p; bestThreat = kv.Value; }
            }
            for (int i = 0; i < stale.Count; i++) m.Threat.Remove(stale[i]);
            if (best == null) return null;
            if (current != null && bestThreat <= currentThreat * w.Data.Combat.Ai.TargetSwitchThreatRatio) return current;
            return best;
        }

        /// <summary>Quem provocou o monstro (status de provocação ativo), se ainda vivo e hostil; senão null.</summary>
        private static Combatant TauntedBy(CombatWorld w, Combatant m)
        {
            Combatant forced = null;
            double latest = double.MinValue;
            for (int i = 0; i < m.Statuses.Count; i++)
            {
                StatusInstance s = m.Statuses[i];
                if (s.Def.Kind != StatusKind.Taunt || s.ExpiresAt <= w.Time) continue;
                Combatant p = w.Get(s.SourceId);
                if (p == null || p.Dead || !w.IsHostile(m, p)) continue;
                if (s.AppliedAt >= latest) { latest = s.AppliedAt; forced = p; }
            }
            return forced;
        }

        private static IList<string> ActiveSkills(Combatant m)
        {
            MonsterDef def = m.MonsterDef;
            if (def.Phases.Count > 0 && m.PhaseIndex >= 0) return def.Phases[m.PhaseIndex].SkillIds;
            return def.SkillIds;
        }

        private static bool TryUseSkill(CombatWorld w, Combatant m, Combatant target, double dist)
        {
            IList<string> skills = ActiveSkills(m);
            for (int i = 0; i < skills.Count; i++)
            {
                SkillDef s = w.Data.Skills[skills[i]];
                if (w.CooldownRemaining(m, s.Id) > 0 || w.GlobalCooldownRemaining(m) > 0) continue;
                if (s.Silenceable && m.HasControl("silence")) continue;

                Vec2 ground = target.Position;
                switch (s.Targeting)
                {
                    case Targeting.Enemy:
                    case Targeting.Ground:
                        if (dist > s.Range) continue;
                        break;
                    case Targeting.SelfArea:
                        if (dist > s.Radius) continue;
                        break;
                    case Targeting.Self:
                        break;
                    default:
                        continue;
                }
                if (w.UseSkill(m, s.Id, target.Id, ground) == UseSkillResult.Ok) return true;
            }
            return false;
        }

        /// <summary>Distância em que o monstro para de se aproximar: fração do menor alcance entre as habilidades corpo a corpo.</summary>
        private static double ApproachDistance(CombatWorld w, Combatant m)
        {
            double min = double.MaxValue;
            IList<string> skills = ActiveSkills(m);
            for (int i = 0; i < skills.Count; i++)
            {
                SkillDef s = w.Data.Skills[skills[i]];
                double r = s.Targeting == Targeting.Enemy ? s.Range : (s.Targeting == Targeting.SelfArea ? s.Radius : double.MaxValue);
                if (r < min) min = r;
            }
            if (min == double.MaxValue) min = 2.0;
            return min * w.Data.Combat.Ai.AttackRangeFraction;
        }

        private static void UpdatePhase(CombatWorld w, Combatant m)
        {
            MonsterDef def = m.MonsterDef;
            if (def.Phases.Count == 0)
            {
                m.PhaseIndex = -1;
                return;
            }
            int idx = 0;
            for (int i = 0; i < def.Phases.Count; i++)
                if (m.HpPercent <= def.Phases[i].HpBelowPercent + 1e-9) idx = i;
            if (idx == m.PhaseIndex) return;

            int previous = m.PhaseIndex;
            BossPhaseDef phase = def.Phases[idx];
            m.PhaseIndex = idx;
            m.PhaseDamageMultiplier = phase.DamageMultiplier;
            m.PhaseCooldownMultiplier = phase.CooldownMultiplier;
            if (previous >= 0)
                w.Emit(new GameEvent { Type = GameEventType.BossPhase, SourceId = m.Id, Count = idx, StatusId = phase.Id, Text = phase.Announcement, Position = m.Position });
        }

        private static void BeginEvade(CombatWorld w, Combatant m)
        {
            m.AiState = AiState.Evade;
            m.EvadeStartedAt = w.Time;
            m.Invulnerable = true;
            m.TargetId = 0;
            m.Threat.Clear();
            m.Contribution.Clear(); // sem crédito por um combate que foi resetado
            m.Cast = null;
            w.ClearStatuses(m);
        }

        private static void Evade(CombatWorld w, Combatant m, double dt)
        {
            AiConfig cfg = w.Data.Combat.Ai;
            // evadeHealPercentPerSecond: fração da vida máxima por segundo (0,25 = 25%/s).
            m.Hp = Math.Min(m.MaxHp, m.Hp + m.MaxHp * cfg.EvadeHealPercentPerSecond * dt);

            double dist = Vec2.Distance(m.Position, m.Home);
            // Sem pathfinding: se um obstáculo prender o monstro, volta para casa por teletransporte após o limite.
            bool stuck = w.Time - m.EvadeStartedAt > MaxEvadeSeconds;
            if (dist > 0.3 && !stuck)
            {
                Vec2 dir = (m.Home - m.Position).Normalized();
                double step = Math.Min(m.MonsterDef.MoveSpeed * cfg.EvadeSpeedMultiplier * dt, dist);
                m.Position = w.Map.Move(m.Position, m.Position + dir * step, w.BodyRadius);
                m.Facing = dir;
                return;
            }
            m.Position = m.Home;
            m.Hp = m.MaxHp;
            m.Invulnerable = false;
            m.AiState = AiState.Idle;
            m.PhaseIndex = -1; // recalcula a fase inicial sem anúncio
            m.PhaseDamageMultiplier = 1.0;
            m.PhaseCooldownMultiplier = 1.0;
        }
    }
}
