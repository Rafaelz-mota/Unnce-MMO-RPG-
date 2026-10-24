using System;
using Mmorpg.Core.Combat;
using Mmorpg.Core.Common;
using Mmorpg.Core.Data;

namespace Mmorpg.Core.Aiming
{
    /// <summary>Resultado de "para onde mirar" uma habilidade: alvo e ponto no chão a enviar para <see cref="CombatWorld.UseSkill"/>.</summary>
    public struct SkillAim
    {
        public bool Ok;
        public UseSkillResult Failure;
        public int TargetId;
        public Vec2 Ground;
    }

    /// <summary>
    /// Traduz "apertei o botão da habilidade X" em alvo + ponto. É só conveniência de interface: quem valida alvo,
    /// alcance, recarga e recurso continua sendo o <see cref="CombatWorld"/> (no jogo online, o servidor).
    /// </summary>
    public static class SkillTargeting
    {
        /// <summary>Distância padrão para habilidades de chão lançadas sem alvo (à frente do personagem).</summary>
        public const double DefaultGroundDistance = 6.0;

        public static SkillAim Aim(CombatWorld world, Combatant caster, SkillDef skill, int selectedTargetId, bool autoTarget)
        {
            if (world == null) throw new ArgumentNullException("world");
            if (caster == null) throw new ArgumentNullException("caster");
            if (skill == null) throw new ArgumentNullException("skill");

            var aim = new SkillAim { Ok = true, Failure = UseSkillResult.Ok, TargetId = 0, Ground = caster.Position };

            switch (skill.Targeting)
            {
                case Targeting.Self:
                case Targeting.SelfArea:
                    aim.TargetId = caster.Id;
                    return aim;

                case Targeting.Ally:
                {
                    Combatant ally = world.Get(selectedTargetId);
                    bool usable = ally != null && !ally.Dead && ally.Kind == caster.Kind && !world.IsHostile(caster, ally);
                    Combatant chosen = usable ? ally : caster;
                    aim.TargetId = chosen.Id;
                    aim.Ground = chosen.Position;
                    return aim;
                }

                case Targeting.Enemy:
                {
                    Combatant t = PickEnemy(world, caster, skill, selectedTargetId, autoTarget);
                    if (t == null) return Fail(aim, UseSkillResult.NoTarget);
                    aim.TargetId = t.Id;
                    aim.Ground = t.Position;
                    return aim;
                }

                case Targeting.Ground:
                {
                    Combatant t = PickEnemy(world, caster, skill, selectedTargetId, autoTarget);
                    if (t != null)
                    {
                        aim.TargetId = t.Id;
                        aim.Ground = t.Position;
                        double dist = Vec2.Distance(caster.Position, t.Position);
                        if (dist > skill.Range + world.Data.Combat.RangeTolerance) return Fail(aim, UseSkillResult.OutOfRange);
                        return aim;
                    }
                    double d = Math.Min(skill.Range, DefaultGroundDistance);
                    aim.Ground = caster.Position + caster.Facing * d;
                    return aim;
                }
            }
            return Fail(aim, UseSkillResult.InvalidTarget);
        }

        private static Combatant PickEnemy(CombatWorld world, Combatant caster, SkillDef skill, int selectedTargetId, bool autoTarget)
        {
            // Alvo já selecionado manda (se estiver longe, o mundo recusa com OutOfRange: não trocamos de alvo em silêncio).
            if (TargetSelector.IsValid(world, caster, selectedTargetId)) return world.Get(selectedTargetId);
            if (!autoTarget) return null;
            return TargetSelector.Nearest(world, caster, skill.Range);
        }

        private static SkillAim Fail(SkillAim aim, UseSkillResult reason)
        {
            aim.Ok = false;
            aim.Failure = reason;
            return aim;
        }
    }
}
