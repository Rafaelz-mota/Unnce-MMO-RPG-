using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Mmorpg.Core.Combat;
using Mmorpg.Core.Common;
using Mmorpg.Core.Events;

namespace Mmorpg.Core.Tests
{
    /// <summary>Comportamento dos monstros (aggro, perseguição, retorno) e as fases do Colosso da Raiz.</summary>
    [TestFixture]
    public class MonsterAiTests
    {
        [Test]
        public void Monster_IgnoresPlayersOutsideItsAggroRange_AndEngagesInsideIt()
        {
            var t = new TestWorld();
            Combatant p = t.Player("guerreiro", 1, 30, 0);
            Combatant m = t.Monster("broto_ancestral", 0, 0); // aggro 8

            t.Step(2.0);
            Assert.AreEqual(AiState.Idle, m.AiState);
            Assert.AreEqual(0.0, m.Position.X, 1e-9);

            p.Position = new Vec2(5, 0);
            t.Step(0.2);
            Assert.AreEqual(AiState.Combat, m.AiState);
        }

        [Test]
        public void Monster_ChasesTheTargetAndDamagesIt()
        {
            var t = new TestWorld();
            Combatant p = t.Player("guerreiro", 1, 0, 0);
            Combatant m = t.Monster("broto_ancestral", 6, 0);

            t.Step(8.0);

            Assert.IsTrue(t.Of(GameEventType.Damage).Any(e => e.SourceId == m.Id && e.TargetId == p.Id), "o monstro deveria ter causado dano");
            Assert.Less(Vec2.Distance(m.Position, p.Position), 3.0, "o monstro deveria ter se aproximado");
        }

        [Test]
        public void Monster_ThatLosesItsTarget_EvadesHealsAndResetsAtHome()
        {
            var t = new TestWorld();
            Combatant p = t.Player("guerreiro", 1, 5, 0);
            Combatant m = t.Monster("broto_ancestral", 0, 0);
            Vec2 home = m.Home;

            t.Step(1.0);
            Assert.AreEqual(AiState.Combat, m.AiState);
            m.Hp = m.MaxHp * 0.5;
            p.Position = new Vec2(300, 0); // longe demais: o monstro persegue até o limite e desiste

            bool evaded = false, wasInvulnerable = false;
            for (int i = 0; i < 900; i++)
            {
                t.Step(0.1);
                if (m.AiState == AiState.Evade) evaded = true;
                if (m.Invulnerable) wasInvulnerable = true;
                if (evaded && m.AiState == AiState.Idle) break;
            }

            Assert.IsTrue(evaded, "deveria ter entrado em modo de retorno");
            Assert.IsTrue(wasInvulnerable, "o monstro é invulnerável enquanto volta");
            Assert.AreEqual(AiState.Idle, m.AiState);
            Assert.IsFalse(m.Invulnerable);
            Assert.AreEqual(m.MaxHp, m.Hp, 1e-6);
            Assert.AreEqual(home.X, m.Position.X, 1e-6);
            Assert.AreEqual(home.Z, m.Position.Z, 1e-6);
        }

        [Test]
        public void EvadingMonster_CannotBeDamaged_AndIsNotATargetCandidate()
        {
            var t = new TestWorld();
            Combatant p = t.Player("guerreiro", 1, 1, 0);
            Combatant m = t.Monster("broto_ancestral", 0, 0);
            m.Invulnerable = true;

            double before = m.Hp;
            t.Use(p, "guerreiro_golpe_poderoso", m);
            t.Step(1.0);

            Assert.AreEqual(before, m.Hp, 1e-9);
            Assert.IsFalse(Mmorpg.Core.Aiming.TargetSelector.IsValid(t.World, p, m.Id));
        }

        private static TestWorld BossFight(out Combatant player, out Combatant boss)
        {
            var t = new TestWorld(new FixedRng(0.5));
            player = t.Player("guerreiro", 7, 0, 0);
            TestWorld.ForceStats(player, "maxHp", 1e7); // o teste observa o chefe, não a sobrevivência
            player.Hp = 1e7;
            boss = t.Monster("colosso_da_raiz", 6, 0);
            return t;
        }

        [Test]
        public void Boss_AnnouncesPhaseTwoOnce_WhenBelowHalfLife_AndGainsItsBonuses()
        {
            Combatant p, boss;
            TestWorld t = BossFight(out p, out boss);

            t.Step(0.3);
            Assert.AreEqual(0, boss.PhaseIndex);
            Assert.AreEqual(1.0, boss.PhaseDamageMultiplier, 1e-9);
            Assert.AreEqual(0, t.Count(GameEventType.BossPhase), "a fase inicial não é anunciada");

            boss.Hp = boss.MaxHp * 0.49;
            t.Step(0.3);

            Assert.AreEqual(1, boss.PhaseIndex);
            GameEvent phase = t.Of(GameEventType.BossPhase).Single();
            Assert.AreEqual("A seiva ancestral desperta!", phase.Text);
            Assert.AreEqual(1.2, boss.PhaseDamageMultiplier, 1e-9);
            Assert.AreEqual(0.85, boss.PhaseCooldownMultiplier, 1e-9);

            t.Step(5.0);
            Assert.AreEqual(1, t.Count(GameEventType.BossPhase), "o anúncio acontece uma única vez");
        }

        [Test]
        public void Boss_UsesOnlyItsFirstPhaseSkillsAtFullLife_AndUnlocksMoreInPhaseTwo()
        {
            Combatant p, boss;
            TestWorld t = BossFight(out p, out boss);
            var phaseOne = new HashSet<string>(boss.MonsterDef.Phases[0].SkillIds);

            t.Step(25.0);
            var used1 = t.Of(GameEventType.SkillUsed).Where(e => e.SourceId == boss.Id).Select(e => e.SkillId).ToList();
            Assert.Greater(used1.Count, 0, "o chefe deveria atacar");
            Assert.IsTrue(used1.All(phaseOne.Contains), "na fase 1 só pode usar as habilidades da fase 1: " + string.Join(",", used1.Distinct()));

            boss.Hp = boss.MaxHp * 0.49;
            t.ClearEvents();
            t.Step(60.0);
            var phaseTwoOnly = boss.MonsterDef.Phases[1].SkillIds.Where(s => !phaseOne.Contains(s)).ToList();
            var used2 = t.Of(GameEventType.SkillUsed).Where(e => e.SourceId == boss.Id).Select(e => e.SkillId).Distinct().ToList();
            Assert.IsTrue(used2.Any(phaseTwoOnly.Contains), "na fase 2 deveria usar alguma habilidade nova: usou " + string.Join(",", used2));
        }

        [Test]
        public void Boss_TelegraphsItsHeavyAttack_ByCastingBeforeTheHit()
        {
            Combatant p, boss;
            TestWorld t = BossFight(out p, out boss);
            t.Step(40.0);

            var casts = t.Of(GameEventType.CastStarted).Where(e => e.SourceId == boss.Id).ToList();
            Assert.Greater(casts.Count, 0, "o chefe deveria sinalizar ataques pesados com tempo de conjuração");
            Assert.IsTrue(casts.Any(c => c.SkillId == "boss_pancada_de_raiz"), "a pancada principal tem tempo de conjuração (0,8 s)");
        }
    }
}
