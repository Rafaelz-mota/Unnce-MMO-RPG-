using System;
using System.Collections.Generic;
using Mmorpg.Core.Common;
using Mmorpg.Core.Data;
using Mmorpg.Core.Events;

namespace Mmorpg.Core.Combat
{
    public enum UseSkillResult
    {
        Ok,
        UnknownSkill,
        Dead,
        NotLearned,
        Stunned,
        Silenced,
        Rooted,
        AlreadyCasting,
        OnCooldown,
        GlobalCooldown,
        NotEnoughResource,
        NoTarget,
        InvalidTarget,
        OutOfRange
    }

    /// <summary>
    /// Uso de habilidades. Tudo é validado aqui (ownership, controle, recarga, recurso, alvo, alcance): o cliente só
    /// envia a intenção (qual habilidade, qual alvo, qual ponto no chão). Os efeitos vêm de skills.json.
    /// </summary>
    public sealed partial class CombatWorld
    {
        /// <summary>Pede para usar uma habilidade. Devolve o motivo exato da recusa, ou Ok (também quando só iniciou o cast).</summary>
        public UseSkillResult UseSkill(Combatant caster, string skillId, int targetId, Vec2 ground)
        {
            if (caster == null || caster.Dead) return UseSkillResult.Dead;
            SkillDef skill;
            if (skillId == null || !Data.Skills.TryGetValue(skillId, out skill)) return UseSkillResult.UnknownSkill;
            if (!OwnsSkill(caster, skill)) return UseSkillResult.NotLearned;
            if (double.IsNaN(ground.X) || double.IsNaN(ground.Z) || double.IsInfinity(ground.X) || double.IsInfinity(ground.Z))
                return UseSkillResult.InvalidTarget;

            if (caster.HasControl("stun")) return UseSkillResult.Stunned;
            if (skill.Silenceable && caster.HasControl("silence")) return UseSkillResult.Silenced;
            if (skill.Type == SkillType.Mobility && caster.HasControl("root")) return UseSkillResult.Rooted;
            if (caster.Cast != null) return UseSkillResult.AlreadyCasting;

            double until;
            if (caster.CooldownUntil.TryGetValue(skill.Id, out until) && until > Time) return UseSkillResult.OnCooldown;
            if (skill.UsesGlobalCooldown && caster.GlobalCooldownUntil > Time) return UseSkillResult.GlobalCooldown;
            if (!HasResource(caster, skill)) return UseSkillResult.NotEnoughResource;

            Combatant target;
            Vec2 point;
            UseSkillResult r = ResolveTarget(caster, skill, targetId, ground, Data.Combat.RangeTolerance, out target, out point);
            if (r != UseSkillResult.Ok) return r;

            Face(caster, target, point, skill);

            if (skill.CastTime > 0)
            {
                caster.Cast = new CastState
                {
                    Skill = skill,
                    StartedAt = Time,
                    EndsAt = Time + skill.CastTime,
                    TargetId = target != null ? target.Id : 0,
                    Ground = point
                };
                if (skill.UsesGlobalCooldown) caster.GlobalCooldownUntil = Math.Max(caster.GlobalCooldownUntil, Time + Data.Combat.GlobalCooldown);
                Vec2 where = skill.Targeting == Targeting.Ground ? point : (skill.Targeting == Targeting.SelfArea ? caster.Position : (target != null ? target.Position : caster.Position));
                Emit(new GameEvent
                {
                    Type = GameEventType.CastStarted, SourceId = caster.Id, TargetId = target != null ? target.Id : 0,
                    SkillId = skill.Id, Amount = skill.CastTime, Position = where, Radius = skill.Radius
                });
                return UseSkillResult.Ok;
            }

            Execute(caster, skill, target, point);
            return UseSkillResult.Ok;
        }

        /// <summary>Segundos restantes de recarga (0 = pronta).</summary>
        public double CooldownRemaining(Combatant c, string skillId)
        {
            double until;
            return c.CooldownUntil.TryGetValue(skillId, out until) ? Math.Max(0, until - Time) : 0;
        }

        public double GlobalCooldownRemaining(Combatant c) { return Math.Max(0, c.GlobalCooldownUntil - Time); }

        // ------------------------------------------------------------------ validação
        private static bool OwnsSkill(Combatant caster, SkillDef skill)
        {
            if (caster.IsPlayer)
                return skill.OwnerType == SkillOwner.Class && skill.OwnerId == caster.Class.Id && caster.Character.IsSkillLearned(skill.Id);
            if (skill.OwnerType != SkillOwner.Monster || skill.OwnerId != caster.MonsterDef.Id) return false;
            if (caster.MonsterDef.SkillIds.Contains(skill.Id)) return true;
            foreach (BossPhaseDef p in caster.MonsterDef.Phases) if (p.SkillIds.Contains(skill.Id)) return true;
            return false;
        }

        private bool HasResource(Combatant c, SkillDef skill)
        {
            if (!c.IsPlayer || skill.ResourceCost <= 0) return true;
            return c.ResourceId != null && c.Resource + 1e-9 >= skill.ResourceCost;
        }

        /// <summary>Resolve e valida alvo/ponto. 'leeway' é a folga de alcance (maior ao concluir um cast, pois o alvo pode ter se mexido).</summary>
        private UseSkillResult ResolveTarget(Combatant caster, SkillDef skill, int targetId, Vec2 ground, double leeway,
            out Combatant target, out Vec2 point)
        {
            target = null;
            point = ground;
            switch (skill.Targeting)
            {
                case Targeting.Self:
                case Targeting.SelfArea:
                    target = caster;
                    point = caster.Position;
                    return UseSkillResult.Ok;

                case Targeting.Enemy:
                    target = Get(targetId);
                    if (target == null || target.Dead) return UseSkillResult.NoTarget;
                    if (!IsHostile(caster, target) || target.Invulnerable) return UseSkillResult.InvalidTarget;
                    point = target.Position;
                    return Distance(caster, target) <= skill.Range + leeway ? UseSkillResult.Ok : UseSkillResult.OutOfRange;

                case Targeting.Ally:
                    target = targetId == 0 ? caster : Get(targetId);
                    if (target == null || target.Dead) return UseSkillResult.NoTarget;
                    if (target.Id != caster.Id && (IsHostile(caster, target) || target.Kind != caster.Kind)) return UseSkillResult.InvalidTarget;
                    point = target.Position;
                    return Distance(caster, target) <= skill.Range + leeway ? UseSkillResult.Ok : UseSkillResult.OutOfRange;

                case Targeting.Ground:
                    return Vec2.Distance(caster.Position, ground) <= skill.Range + leeway ? UseSkillResult.Ok : UseSkillResult.OutOfRange;
            }
            return UseSkillResult.InvalidTarget;
        }

        private static void Face(Combatant caster, Combatant target, Vec2 point, SkillDef skill)
        {
            Vec2 to = skill.Targeting == Targeting.Ground ? point : (target != null && target.Id != caster.Id ? target.Position : caster.Position);
            Vec2 dir = (to - caster.Position).Normalized();
            if (dir.SqrLength > 0.5) caster.Facing = dir;
        }

        // ------------------------------------------------------------------ cast
        private void UpdateCast(Combatant c)
        {
            CastState cast = c.Cast;
            if (cast == null || Time < cast.EndsAt) return;
            c.Cast = null;

            Combatant target;
            Vec2 point;
            UseSkillResult r = ResolveTarget(c, cast.Skill, cast.TargetId, cast.Ground, Data.Combat.CastRangeLeeway, out target, out point);
            if (r == UseSkillResult.Ok && !HasResource(c, cast.Skill)) r = UseSkillResult.NotEnoughResource;
            if (r != UseSkillResult.Ok)
            {
                Emit(new GameEvent { Type = GameEventType.CastInterrupted, SourceId = c.Id, SkillId = cast.Skill.Id, Text = r.ToString() });
                return;
            }
            // Habilidades no chão mantêm o ponto marcado no início (telegrafia): o alvo pode sair da área.
            Execute(c, cast.Skill, target, cast.Skill.Targeting == Targeting.Ground ? cast.Ground : point);
        }

        internal void InterruptCast(Combatant c, string reason)
        {
            if (c.Cast == null) return;
            SkillDef skill = c.Cast.Skill;
            c.Cast = null;
            Emit(new GameEvent { Type = GameEventType.CastInterrupted, SourceId = c.Id, SkillId = skill.Id, Text = reason });
        }

        // ------------------------------------------------------------------ execução
        private void Execute(Combatant caster, SkillDef skill, Combatant target, Vec2 point)
        {
            if (caster.IsPlayer && skill.ResourceCost > 0)
            {
                caster.Resource -= skill.ResourceCost;
                ClampResource(caster);
            }
            double cd = skill.Cooldown * (caster.IsMonster ? caster.PhaseCooldownMultiplier : 1.0);
            caster.CooldownUntil[skill.Id] = Time + cd;
            if (skill.UsesGlobalCooldown && skill.CastTime <= 0)
                caster.GlobalCooldownUntil = Math.Max(caster.GlobalCooldownUntil, Time + Data.Combat.GlobalCooldown);

            Emit(new GameEvent
            {
                Type = GameEventType.SkillUsed, SourceId = caster.Id, TargetId = target != null ? target.Id : 0,
                SkillId = skill.Id, Position = skill.Targeting == Targeting.Ground ? point : caster.Position, Radius = skill.Radius
            });

            List<Combatant> enemies = null, allies = null;
            for (int i = 0; i < skill.Effects.Count; i++)
            {
                SkillEffectDef e = skill.Effects[i];
                EffectSide side = e.ResolvedSide();

                if (e.Kind == EffectKind.Dash)
                {
                    Dash(caster, target, e, skill);
                    continue;
                }

                List<Combatant> targets;
                if (side == EffectSide.Self) targets = new List<Combatant>(1) { caster };
                else if (side == EffectSide.Enemy) targets = enemies ?? (enemies = CollectTargets(caster, skill, target, point, EffectSide.Enemy));
                else targets = allies ?? (allies = CollectTargets(caster, skill, target, point, EffectSide.Ally));

                switch (e.Kind)
                {
                    case EffectKind.Damage:
                    {
                        double aoe = skill.MaxTargets > 1 ? DamageFormula.AoeMultiplier(Data, targets.Count) : 1.0;
                        for (int t = 0; t < targets.Count; t++) DealSkillDamage(caster, targets[t], skill, e, aoe);
                        break;
                    }
                    case EffectKind.Heal:
                    {
                        double amount = DamageFormula.Scale(e, caster.Stats);
                        for (int t = 0; t < targets.Count; t++) HealTarget(caster, targets[t], amount, skill.Id);
                        break;
                    }
                    case EffectKind.Shield:
                    case EffectKind.ApplyStatus:
                    {
                        StatusDef def = Data.Statuses[e.StatusId];
                        double value = DamageFormula.Scale(e, caster.Stats);
                        for (int t = 0; t < targets.Count; t++)
                        {
                            if (e.Chance < 1.0 && Rng.NextDouble() >= e.Chance) continue;
                            ApplyStatus(caster, targets[t], def, e.Duration, value, e.DamageType, skill.Id);
                        }
                        break;
                    }
                }
            }
        }

        private List<Combatant> CollectTargets(Combatant caster, SkillDef skill, Combatant target, Vec2 point, EffectSide side)
        {
            var list = new List<Combatant>();
            if (side == EffectSide.Enemy)
            {
                switch (skill.Targeting)
                {
                    case Targeting.Enemy:
                        if (target != null && !target.Dead) list.Add(target);
                        break;
                    case Targeting.Ground:
                        CollectInRadius(caster, point, skill.Radius, true, list);
                        break;
                    case Targeting.SelfArea:
                        CollectInRadius(caster, caster.Position, skill.Radius, true, list);
                        break;
                }
                list.Sort((a, b) => Vec2.Distance(a.Position, point).CompareTo(Vec2.Distance(b.Position, point)));
            }
            else
            {
                switch (skill.Targeting)
                {
                    case Targeting.Ally:
                    case Targeting.Self:
                        if (target != null && !target.Dead) list.Add(target);
                        break;
                    case Targeting.Ground:
                        CollectInRadius(caster, point, skill.Radius, false, list);
                        break;
                    case Targeting.SelfArea:
                        CollectInRadius(caster, caster.Position, skill.Radius, false, list);
                        break;
                }
                // Cura em área prioriza os mais feridos.
                list.Sort((a, b) => a.HpPercent.CompareTo(b.HpPercent));
            }
            int max = Math.Max(1, skill.MaxTargets);
            if (list.Count > max) list.RemoveRange(max, list.Count - max);
            return list;
        }

        private void CollectInRadius(Combatant caster, Vec2 center, double radius, bool hostile, List<Combatant> into)
        {
            for (int i = 0; i < _all.Count; i++)
            {
                Combatant o = _all[i];
                if (o.Dead || o.Invulnerable) continue;
                if (hostile ? !IsHostile(caster, o) : (IsHostile(caster, o) || o.Kind != caster.Kind)) continue;
                if (Vec2.Distance(o.Position, center) <= radius) into.Add(o);
            }
        }

        private void Dash(Combatant caster, Combatant target, SkillEffectDef e, SkillDef skill)
        {
            Vec2 from = caster.Position;
            bool hasTarget = target != null && target.Id != caster.Id;
            Vec2 dir;
            double distance = e.DashDistance;
            if (e.DashDirection == DashDirection.Toward)
            {
                dir = hasTarget ? (target.Position - from).Normalized() : caster.Facing;
                if (hasTarget) distance = Math.Min(distance, Math.Max(0.0, Vec2.Distance(from, target.Position) - 2.0 * BodyRadius));
            }
            else
            {
                dir = hasTarget ? (from - target.Position).Normalized() : caster.Facing * -1.0;
            }
            if (dir.SqrLength < 1e-9) dir = caster.Facing;
            Vec2 to = Map.Move(from, from + dir * distance, BodyRadius);
            caster.Position = to;
            if (e.DashDirection == DashDirection.Toward) caster.Facing = dir;
            Emit(new GameEvent { Type = GameEventType.Dash, SourceId = caster.Id, SkillId = skill.Id, Position = from, Position2 = to });
        }
    }
}
