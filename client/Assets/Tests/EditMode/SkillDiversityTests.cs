using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Mmorpg.Core.Combat;
using Mmorpg.Core.Data;
using Mmorpg.Core.Events;

namespace Mmorpg.Core.Tests
{
    /// <summary>Garante que as habilidades de classe são mecanicamente diferentes e que as mecânicas novas funcionam.</summary>
    [TestFixture]
    public class SkillDiversityTests
    {
        private static string Signature(SkillDef s)
        {
            return s.Type + "|" + s.Targeting + "|" + s.Element + "|" + string.Join(";", s.Effects.Select(e =>
                e.Kind + "," + e.ResolvedSide() + "," + e.StatusId + "," + e.DamageType + "," + e.Hits + "," + e.Leech + "," +
                e.ExecuteBelowHpPercent + "," + e.BonusIfStatusId + "," + e.Base + "," +
                string.Join("+", e.Scaling.OrderBy(k => k.Key).Select(k => k.Key + k.Value))));
        }

        [Test]
        public void ClassSkills_NoTwoSkillsShareTheSameEffectsAndNumbers()
        {
            var t = new TestWorld();
            var groups = t.Data.Skills.Values.Where(s => s.OwnerType == SkillOwner.Class)
                .GroupBy(s => string.Join("/", s.Effects.Select(e => e.Kind + "," + e.ResolvedSide() + "," + e.StatusId + "," + e.DamageType +
                    "," + e.Base + "," + e.Hits + "," + e.Leech + "," + e.ExecuteBelowHpPercent + "," + e.BonusIfStatusId + "," + e.Duration + "," +
                    string.Join("+", e.Scaling.OrderBy(k => k.Key).Select(k => k.Key + k.Value)))))
                .Where(g => g.Count() > 1).ToList();
            Assert.IsEmpty(groups, "habilidades idênticas: " + string.Join(" | ", groups.Select(g => string.Join(",", g.Select(s => s.Id)))));
        }

        [Test]
        public void EveryClass_HasAtLeastSixDistinctSkillMechanics()
        {
            var t = new TestWorld();
            foreach (ClassDef c in t.Data.Classes.Values)
            {
                var skills = t.Data.Skills.Values.Where(s => s.OwnerType == SkillOwner.Class && s.OwnerId == c.Id).ToList();
                Assert.AreEqual(9, skills.Count, c.Id);
                int distinct = skills.Select(s => Signature(s)).Distinct().Count();
                Assert.AreEqual(9, distinct, c.Id + ": habilidades com mesma assinatura");
            }
        }

        [Test]
        public void MultiHit_ProducesOneDamageEventPerHit()
        {
            var t = new TestWorld();
            Combatant a = t.Player("assassino", 1, 0, 0);
            Combatant dummy = t.Dummy(1, 0);
            Assert.AreEqual(UseSkillResult.Ok, t.Use(a, "assassino_corte_rapido", dummy));
            t.Flush();
            Assert.AreEqual(2, t.Of(GameEventType.Damage).Count());
        }

        [Test]
        public void Leech_HealsTheCasterFromDamageDealt()
        {
            var t = new TestWorld();
            Combatant n = t.Player("necromante", 1, 0, 0);
            Combatant dummy = t.Dummy(5, 0);
            n.Hp = n.MaxHp * 0.5;
            double before = n.Hp;
            Assert.AreEqual(UseSkillResult.Ok, t.Use(n, "necromante_raio_necrotico", dummy));
            t.Step(1.2);
            t.Flush();
            Assert.Greater(n.Hp, before);
            Assert.IsTrue(t.Of(GameEventType.Heal).Any(e => e.TargetId == n.Id));
        }

        [Test]
        public void Execute_DealsMoreDamageToLowHealthTargets()
        {
            var t = new TestWorld();
            Combatant a = t.Player("arqueiro", 1, 0, 0);
            Combatant healthy = t.Dummy(10, 0);
            Combatant weak = t.Dummy(10, 3);
            weak.Hp = weak.MaxHp * 0.2;
            Assert.AreEqual(UseSkillResult.Ok, t.Use(a, "arqueiro_tiro_preciso", healthy));
            t.Step(t.Data.Combat.GlobalCooldown + 0.1);
            Assert.AreEqual(UseSkillResult.Ok, t.Use(a, "arqueiro_tiro_preciso", weak));
            t.Flush();
            double dHealthy = t.Of(GameEventType.Damage).First(e => e.TargetId == healthy.Id).Amount;
            double dWeak = t.Of(GameEventType.Damage).First(e => e.TargetId == weak.Id).Amount;
            Assert.AreEqual(1.7, dWeak / dHealthy, 0.05);
        }

        [Test]
        public void BonusIfStatus_AmplifiesDamageOnlyWhenTheStatusIsPresent()
        {
            var t = new TestWorld();
            Combatant m = t.Player("mago_runico", 1, 0, 0);
            Combatant plain = t.Dummy(10, 0);
            Combatant chilled = t.Dummy(10, 3);
            Assert.IsTrue(t.World.ApplyStatus(m, chilled, t.Data.Statuses["chilled"], 20.0, 0, DamageType.Magic, "teste"));
            Assert.AreEqual(UseSkillResult.Ok, t.Use(m, "mago_runico_runa_de_fogo", plain));
            t.Step(1.5);
            Assert.AreEqual(UseSkillResult.Ok, t.Use(m, "mago_runico_runa_de_fogo", chilled));
            t.Step(1.5);
            t.Flush();
            double dPlain = t.Of(GameEventType.Damage).First(e => e.TargetId == plain.Id).Amount;
            double dChilled = t.Of(GameEventType.Damage).First(e => e.TargetId == chilled.Id).Amount;
            Assert.AreEqual(1.5, dChilled / dPlain, 0.05);
        }

        [Test]
        public void Cleanse_RemovesHarmfulStatusesFromAnAlly()
        {
            var t = new TestWorld();
            Combatant p = t.Player("paladino", 1, 0, 0);
            Combatant ally = t.Player("guerreiro", 1, 2, 0);
            Combatant dummy = t.Dummy(15, 0);
            t.World.ApplyStatus(dummy, ally, t.Data.Statuses["crippled"], 10.0, 0, DamageType.Physical, "teste");
            ally.Hp = ally.MaxHp * 0.5;
            Assert.IsTrue(ally.Statuses.Any(s => s.Def.Id == "crippled"));
            Assert.AreEqual(UseSkillResult.Ok, t.Use(p, "paladino_mao_curativa", ally));
            t.Step(1.5);
            Assert.IsFalse(ally.Statuses.Any(s => s.Def.Id == "crippled"), "efeito nocivo removido");
        }
    }
}
