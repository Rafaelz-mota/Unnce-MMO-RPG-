using System;
using System.Collections.Generic;
using Mmorpg.Core.Aiming;
using Mmorpg.Core.Characters;
using Mmorpg.Core.Combat;
using Mmorpg.Core.Common;
using Mmorpg.Core.Data;
using Mmorpg.Core.World;

namespace Mmorpg.Core.Tests
{
    /// <summary>
    /// Jogador automático para testes e telemetria de balanceamento (sem Unity). Escolhe o monstro mais próximo
    /// adequado ao nível, se aproxima, usa as habilidades que conhece e cura a si mesmo quando a vida está baixa.
    /// Não é IA de jogo: serve só para exercitar a simulação do começo ao fim e medir progressão.
    /// </summary>
    public sealed class HeadlessBot
    {
        private readonly WorldSimulation _sim;
        private readonly Combatant _me;
        private int _targetId;

        /// <summary>Se verdadeiro, o bot também enfrenta chefes regionais.</summary>
        public bool AllowBosses { get; set; }

        /// <summary>O bot só escolhe monstros até este nível acima do dele.</summary>
        public int MaxLevelAbove { get; set; } = 2;

        public HeadlessBot(WorldSimulation sim, Combatant me)
        {
            if (sim == null) throw new ArgumentNullException("sim");
            if (me == null) throw new ArgumentNullException("me");
            _sim = sim;
            _me = me;
        }

        public Combatant Target { get { return _sim.World.Get(_targetId); } }

        /// <summary>Chame a cada ~0,1 s de tempo de simulação, antes de <see cref="WorldSimulation.Update"/>.</summary>
        public void Think()
        {
            CombatWorld world = _sim.World;
            if (_me.Dead)
            {
                world.SetMoveIntent(_me, 0, 0);
                return;
            }

            Combatant target = world.Get(_targetId);
            if (target == null || !TargetSelector.IsValid(world, _me, _targetId) || !IsAcceptable(target)) target = Choose();
            if (target == null)
            {
                _targetId = 0;
                world.SetMoveIntent(_me, 0, 0);
                return;
            }
            _targetId = target.Id;

            double desired = DesiredDistance();
            Vec2 delta = target.Position - _me.Position;
            double dist = delta.Length;
            if (dist > desired)
            {
                Vec2 dir = delta.Normalized();
                world.SetMoveIntent(_me, dir.X, dir.Z);
            }
            else
            {
                world.SetMoveIntent(_me, 0, 0);
            }

            UseBestSkill(target);
        }

        private bool IsAcceptable(Combatant c)
        {
            if (!c.IsMonster) return false;
            bool boss = c.Category != null && c.Category.Id == _sim.Data.Rules.BossCategoryId;
            if (boss && !AllowBosses) return false;
            return c.Level <= _me.Level + MaxLevelAbove;
        }

        private Combatant Choose()
        {
            Combatant best = null;
            double bestDist = double.MaxValue;
            IList<Combatant> all = _sim.World.All;
            for (int i = 0; i < all.Count; i++)
            {
                Combatant c = all[i];
                if (c.Dead || c.Invulnerable || !_sim.World.IsHostile(_me, c) || !IsAcceptable(c)) continue;
                double d = Vec2.Distance(_me.Position, c.Position);
                if (d < bestDist) { best = c; bestDist = d; }
            }
            return best;
        }

        /// <summary>Corpo a corpo fica colado; à distância fica a 80% do alcance da habilidade básica.</summary>
        private double DesiredDistance()
        {
            double range = 0;
            foreach (SkillDef s in _me.Character.ClassSkills())
            {
                if (s.Slot == 1 && s.Targeting == Targeting.Enemy) { range = s.Range; break; }
            }
            if (range <= 0) range = 0;
            if (range > 6) return range * 0.8;
            return 1.8;
        }

        private void UseBestSkill(Combatant target)
        {
            CombatWorld world = _sim.World;
            PlayerCharacter pc = _me.Character;
            IList<SkillDef> skills = pc.ClassSkills();

            var ordered = new List<SkillDef>();
            for (int i = 0; i < skills.Count; i++) if (pc.IsSkillLearned(skills[i].Id)) ordered.Add(skills[i]);
            ordered.Sort((a, b) => b.Slot.CompareTo(a.Slot)); // as mais fortes (slots altos) primeiro

            for (int i = 0; i < ordered.Count; i++)
            {
                SkillDef s = ordered[i];
                bool defensive = s.Type == SkillType.Heal || s.Type == SkillType.Buff;
                if (defensive && _me.HpPercent > 55) continue;
                if (!defensive && (s.Type == SkillType.Utility)) continue;

                int selected = defensive ? _me.Id : target.Id;
                SkillAim aim = SkillTargeting.Aim(world, _me, s, selected, true);
                if (!aim.Ok) continue;
                if (world.UseSkill(_me, s.Id, aim.TargetId, aim.Ground) == UseSkillResult.Ok) return;
            }
        }
    }
}
