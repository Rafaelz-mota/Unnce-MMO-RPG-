using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Mmorpg.Core.Characters;
using Mmorpg.Core.Combat;
using Mmorpg.Core.Common;
using Mmorpg.Core.Data;
using Mmorpg.Core.Events;

namespace Mmorpg.Core.Tests
{
    /// <summary>Regras de combate: validação de habilidades, dano, recursos, controle, escudos, DoT, dash, PvP.</summary>
    [TestFixture]
    public class CombatTests
    {
        [Test]
        public void UseSkill_RefusesUnknownForeignUnlearnedAndDeadCasters()
        {
            var t = new TestWorld();
            Combatant w = t.Player("guerreiro", 1, 0, 0);
            Combatant dummy = t.Dummy(1, 0);

            Assert.AreEqual(UseSkillResult.UnknownSkill, t.World.UseSkill(w, "nao_existe", dummy.Id, dummy.Position));
            Assert.AreEqual(UseSkillResult.NotLearned, t.Use(w, "mago_raio_arcano", dummy), "habilidade de outra classe");
            Assert.AreEqual(UseSkillResult.NotLearned, t.Use(w, "guerreiro_muralha_de_aco", dummy), "habilidade paga não comprada");

            w.Dead = true;
            Assert.AreEqual(UseSkillResult.Dead, t.Use(w, "guerreiro_golpe_poderoso", dummy));
        }

        [Test]
        public void BasicAttack_DealsDamage_GeneratesRage_AndTriggersGlobalCooldown()
        {
            var t = new TestWorld();
            Combatant w = t.Player("guerreiro", 1, 0, 0);
            Combatant dummy = t.Dummy(1, 0);
            double hpBefore = dummy.Hp;

            Assert.AreEqual(UseSkillResult.Ok, t.Use(w, "guerreiro_golpe_poderoso", dummy));
            t.Flush();

            GameEvent hit = t.Of(GameEventType.Damage).Single();
            Assert.AreEqual(w.Id, hit.SourceId);
            Assert.AreEqual(dummy.Id, hit.TargetId);
            Assert.AreEqual("guerreiro_golpe_poderoso", hit.SkillId);
            Assert.Less(dummy.Hp, hpBefore);
            Assert.AreEqual(t.Data.Resources["rage"].GainOnDealDamage, w.Resource, 1e-6, "fúria ganha ao causar dano (attributes.json)");

            Assert.AreEqual(UseSkillResult.GlobalCooldown, t.Use(w, "guerreiro_golpe_poderoso", dummy));
            t.Step(t.Data.Combat.GlobalCooldown + 0.1);
            Assert.AreEqual(UseSkillResult.Ok, t.Use(w, "guerreiro_golpe_poderoso", dummy));
        }

        [Test]
        public void Cooldown_IsReportedAndBlocksReuse()
        {
            var t = new TestWorld();
            Combatant w = t.Player("guerreiro", 2, 0, 0);
            Combatant dummy = t.Dummy(10, 0);
            SkillDef charge = t.Data.Skills["guerreiro_investida"];

            Assert.AreEqual(UseSkillResult.Ok, t.Use(w, charge.Id, dummy));
            Assert.AreEqual(charge.Cooldown, t.World.CooldownRemaining(w, charge.Id), 1e-6);

            t.Step(charge.Cooldown - 1.0);
            Assert.AreEqual(UseSkillResult.OnCooldown, t.Use(w, charge.Id, dummy));

            t.Step(1.5);
            Assert.AreEqual(UseSkillResult.Ok, t.Use(w, charge.Id, dummy));
        }

        [Test]
        public void NotEnoughResource_IsRefusedWithoutSideEffects()
        {
            var t = new TestWorld();
            Combatant m = t.Player("mago", 1, 0, 0);
            Combatant dummy = t.Dummy(5, 0);
            m.Resource = 0;

            Assert.AreEqual(UseSkillResult.NotEnoughResource, t.Use(m, "mago_raio_arcano", dummy));
            Assert.AreEqual(0.0, t.World.CooldownRemaining(m, "mago_raio_arcano"), 1e-9);
            Assert.AreEqual(0.0, t.World.GlobalCooldownRemaining(m), 1e-9);
            Assert.IsNull(m.Cast);
        }

        [Test]
        public void TargetValidation_ReportsTheExactReason()
        {
            var t = new TestWorld();
            Combatant w = t.Player("guerreiro", 1, 0, 0);
            Combatant far = t.Dummy(10, 0);
            Combatant near = t.Dummy(1, 0);
            const string skill = "guerreiro_golpe_poderoso";

            Assert.AreEqual(UseSkillResult.OutOfRange, t.Use(w, skill, far));
            Assert.AreEqual(UseSkillResult.NoTarget, t.World.UseSkill(w, skill, 0, Vec2.Zero));
            Assert.AreEqual(UseSkillResult.NoTarget, t.World.UseSkill(w, skill, 99999, Vec2.Zero));
            Assert.AreEqual(UseSkillResult.InvalidTarget, t.World.UseSkill(w, skill, w.Id, w.Position), "não ataca a si mesmo");
            Assert.AreEqual(UseSkillResult.InvalidTarget, t.World.UseSkill(w, skill, near.Id, new Vec2(double.NaN, 0)), "ponto inválido");

            near.Dead = true;
            Assert.AreEqual(UseSkillResult.NoTarget, t.Use(w, skill, near), "alvo morto");
        }

        [Test]
        public void Mitigation_ReducesDamage_NeverBeyondTheCap_AndNeverBelowOne()
        {
            var t = new TestWorld();
            Combatant w = t.Player("guerreiro", 1, 0, 0);
            Combatant plain = t.Dummy(1, 0);
            Combatant armored = t.Dummy(1.5, 0);
            TestWorld.ForceStats(plain, "physDefense", 0);
            TestWorld.ForceStats(armored, "physDefense", 1e9);

            t.Use(w, "guerreiro_golpe_poderoso", plain);
            t.Step(t.Data.Combat.GlobalCooldown + 0.1);
            t.Use(w, "guerreiro_golpe_poderoso", armored);
            t.Flush();

            double dPlain = t.Of(GameEventType.Damage).First(e => e.TargetId == plain.Id).Amount;
            double dArmored = t.Of(GameEventType.Damage).First(e => e.TargetId == armored.Id).Amount;
            double cap = t.Data.Combat.Mitigation.MaxMitigation;

            Assert.Less(dArmored, dPlain);
            Assert.AreEqual(dPlain * (1.0 - cap), dArmored, 1e-6, "mitigação limitada por combat.json");

            double floor = t.World.ApplyDamage(w, w.Id, 1, true, armored, 0.5, DamageType.Physical, "teste", false);
            Assert.AreEqual(1.0, floor, 1e-9, "dano mínimo de 1");
        }

        [Test]
        public void AoeMultiplier_FollowsConfig_AndRespectsFloor()
        {
            var t = new TestWorld();
            AoeFalloffConfig a = t.Data.Combat.AoeFalloff;
            Assert.AreEqual(1.0, DamageFormula.AoeMultiplier(t.Data, a.FreeTargets), 1e-9);
            Assert.AreEqual(1.0 - a.PerExtraTarget, DamageFormula.AoeMultiplier(t.Data, a.FreeTargets + 1), 1e-9);
            Assert.AreEqual(a.MinMultiplier, DamageFormula.AoeMultiplier(t.Data, 100), 1e-9);
        }

        [Test]
        public void SelfAreaSkill_HitsOnlyTargetsInsideRadius_UpToMaxTargets()
        {
            var t = new TestWorld();
            Combatant w = t.Player("guerreiro", 5, 0, 0);
            SkillDef spin = t.Data.Skills["guerreiro_golpe_giratorio"];
            w.Resource = 100;
            var inside = new List<Combatant>();
            for (int i = 0; i < spin.MaxTargets + 2; i++) inside.Add(t.Dummy(1.0 + i * 0.1, 0));
            Combatant outside = t.Dummy(spin.Radius + 6, 0);

            Assert.AreEqual(UseSkillResult.Ok, t.Use(w, spin.Id));
            t.Flush();

            Assert.AreEqual(spin.MaxTargets, t.Of(GameEventType.Damage).Count(), "limite de alvos da habilidade");
            Assert.AreEqual(outside.MaxHp, outside.Hp, 1e-9, "fora do raio não é atingido");
        }

        [Test]
        public void ControlEffects_BlockSkills_AsConfigured()
        {
            var t = new TestWorld();
            Combatant w = t.Player("guerreiro", 7, 0, 0);
            Combatant dummy = t.Dummy(1, 0);

            Assert.IsTrue(t.World.ApplyStatus(dummy, w, t.Data.Statuses["stunned"], 1.0, 0, DamageType.Physical, "teste"));
            Assert.AreEqual(UseSkillResult.Stunned, t.Use(w, "guerreiro_golpe_poderoso", dummy));
            t.Step(1.1);
            Assert.AreEqual(UseSkillResult.Ok, t.Use(w, "guerreiro_golpe_poderoso", dummy));

            // Silêncio só bloqueia habilidades marcadas como "silenceable" nos dados (magias).
            // O Golpe Poderoso é físico e não é silenciável; o Raio Arcano é.
            t.Step(t.Data.Combat.GlobalCooldown + 0.1);
            Assert.IsTrue(t.World.ApplyStatus(dummy, w, t.Data.Statuses["silenced"], 1.0, 0, DamageType.Physical, "teste"));
            Assert.AreEqual(UseSkillResult.Ok, t.Use(w, "guerreiro_golpe_poderoso", dummy), "habilidade física não é silenciável");

            Combatant m = t.Player("mago", 1, 0, 3);
            Assert.IsTrue(t.World.ApplyStatus(dummy, m, t.Data.Statuses["silenced"], 1.0, 0, DamageType.Physical, "teste"));
            Assert.AreEqual(UseSkillResult.Silenced, t.Use(m, "mago_raio_arcano", dummy));
        }

        [Test]
        public void Root_BlocksMobilitySkills_ButNotAttacks()
        {
            var t = new TestWorld();
            Combatant w = t.Player("guerreiro", 2, 0, 0);
            Combatant dummy = t.Dummy(10, 0);

            Assert.IsTrue(t.World.ApplyStatus(dummy, w, t.Data.Statuses["rooted"], 2.0, 0, DamageType.Physical, "teste"));
            Assert.AreEqual(UseSkillResult.Rooted, t.Use(w, "guerreiro_investida", dummy));

            Combatant near = t.Dummy(1, 0);
            Assert.AreEqual(UseSkillResult.Ok, t.Use(w, "guerreiro_golpe_poderoso", near));
        }

        [Test]
        public void Control_IsCappedByMaxDuration_ThenImmunityBlocksReapplication()
        {
            var t = new TestWorld();
            Combatant w = t.Player("guerreiro", 1, 0, 0);
            Combatant dummy = t.Dummy(1, 0);
            StatusDef stun = t.Data.Statuses["stunned"];
            double max = t.Data.Combat.Control.MaxDuration["stun"];
            double immunity = t.Data.Combat.Control.ImmunityAfter["stun"];

            Assert.IsTrue(t.World.ApplyStatus(w, dummy, stun, 99.0, 0, DamageType.Physical, "teste"));
            StatusInstance s = dummy.Statuses.Single(x => x.Def.Id == "stunned");
            Assert.AreEqual(max, s.ExpiresAt - t.World.Time, 1e-6, "duração limitada por combat.json");

            t.Step(max + 0.1); // o atordoamento acabou, mas a imunidade temporária ainda vale
            Assert.IsFalse(dummy.HasControl("stun"));
            Assert.IsFalse(t.World.ApplyStatus(w, dummy, stun, 1.0, 0, DamageType.Physical, "teste"));
            t.Flush();
            Assert.AreEqual(1, t.Count(GameEventType.StatusResisted));

            t.Step(immunity + 0.1);
            Assert.IsTrue(t.World.ApplyStatus(w, dummy, stun, 1.0, 0, DamageType.Physical, "teste"), "imunidade acabou");
        }

        [Test]
        public void Boss_IsImmuneToStun_AndHalvesSlowDuration()
        {
            var t = new TestWorld();
            Combatant w = t.Player("guerreiro", 1, 0, 0);
            Combatant boss = t.Monster("colosso_da_raiz", 60, 0);

            Assert.IsFalse(t.World.ApplyStatus(w, boss, t.Data.Statuses["stunned"], 2.0, 0, DamageType.Physical, "teste"));
            t.Flush();
            Assert.AreEqual(1, t.Count(GameEventType.StatusResisted));

            double resist = boss.MonsterDef.CcResist["slow"];
            Assert.IsTrue(t.World.ApplyStatus(w, boss, t.Data.Statuses["chilled"], 4.0, 0, DamageType.Physical, "teste"));
            StatusInstance slow = boss.Statuses.Single(x => x.Def.Id == "chilled");
            Assert.AreEqual(4.0 * resist, slow.ExpiresAt - t.World.Time, 1e-6);
        }

        [Test]
        public void Shield_AbsorbsDamageBeforeHp_AndIsRemovedWhenSpent()
        {
            var t = new TestWorld();
            Combatant m = t.Player("mago", 5, 0, 0);
            TestWorld.ForceStats(m, "magicResist", 0);
            Assert.AreEqual(UseSkillResult.Ok, t.Use(m, "mago_escudo_arcano"));

            double shield = m.ShieldTotal;
            double hp = m.Hp;
            Assert.Greater(shield, 0.0);

            double half = shield / 2.0;
            t.World.ApplyDamage(null, 0, 1, false, m, half, DamageType.Magic, "teste", false);
            Assert.AreEqual(hp, m.Hp, 1e-9, "o escudo absorve primeiro");
            Assert.AreEqual(shield - half, m.ShieldTotal, 1e-6);

            t.World.ApplyDamage(null, 0, 1, false, m, shield, DamageType.Magic, "teste", false);
            Assert.AreEqual(0.0, m.ShieldTotal, 1e-9);
            Assert.AreEqual(hp - (shield - half), m.Hp, 1e-6, "só o excedente chega à vida");
            Assert.IsFalse(m.Statuses.Any(s => s.Def.Kind == StatusKind.Shield), "escudo gasto é removido");
        }

        [Test]
        public void DamageOverTime_TicksAtTheConfiguredInterval_AndExpires()
        {
            var t = new TestWorld();
            Combatant w = t.Player("mago", 1, 0, 0);
            Combatant dummy = t.Dummy(5, 0);
            StatusDef burning = t.Data.Statuses["burning"];

            Assert.IsTrue(t.World.ApplyStatus(w, dummy, burning, 3.0, 5.0, DamageType.Magic, "teste_dot"));
            t.Step(3.5);

            int ticks = t.Of(GameEventType.Damage).Count(e => e.SkillId == "teste_dot");
            Assert.AreEqual((int)(3.0 / burning.TickInterval), ticks);
            Assert.IsFalse(dummy.Statuses.Any(s => s.Def.Id == "burning"));
        }

        [Test]
        public void Healer_CastsOnSelf_RestoringHpAndSpendingMana()
        {
            var t = new TestWorld();
            Combatant p = t.Player("sacerdote", 1, 0, 0);
            TestWorld.ForceStats(p, "manaRegen", 0);
            SkillDef heal = t.Data.Skills["sacerdote_toque_de_luz"];
            p.Hp = 1;
            double manaBefore = p.Resource;

            Assert.AreEqual(UseSkillResult.Ok, t.Use(p, heal.Id)); // sem alvo = o próprio sacerdote
            Assert.AreEqual(1.0, p.Hp, 1e-9, "ainda está conjurando");

            t.Step(heal.CastTime + 0.2);
            Assert.Greater(p.Hp, 1.0);
            Assert.AreEqual(manaBefore - heal.ResourceCost, p.Resource, 1e-6);
            Assert.GreaterOrEqual(t.Count(GameEventType.Heal), 1);
        }

        [Test]
        public void Cast_IsInterruptedByMovement_WithoutSpendingResource()
        {
            var t = new TestWorld();
            Combatant m = t.Player("mago", 1, 0, 0);
            TestWorld.ForceStats(m, "manaRegen", 0);
            Combatant dummy = t.Dummy(5, 0);
            double mana = m.Resource;

            Assert.AreEqual(UseSkillResult.Ok, t.Use(m, "mago_bola_de_fogo", dummy));
            Assert.IsNotNull(m.Cast);
            t.World.SetMoveIntent(m, 0, 1);
            t.Step(0.2);

            Assert.AreEqual(1, t.Count(GameEventType.CastInterrupted));
            Assert.IsNull(m.Cast);
            Assert.AreEqual(dummy.MaxHp, dummy.Hp, 1e-9, "nenhum dano: a habilidade não saiu");
            Assert.AreEqual(mana, m.Resource, 1e-9);
            Assert.Greater(m.Position.Z, 0.0, "o personagem se moveu");
        }

        [Test]
        public void Dash_TowardStopsNextToTheTarget_AndAppliesSlow()
        {
            var t = new TestWorld();
            Combatant w = t.Player("guerreiro", 2, 0, 0);
            Combatant dummy = t.Dummy(10, 0);

            Assert.AreEqual(UseSkillResult.Ok, t.Use(w, "guerreiro_investida", dummy));
            double stopAt = 10.0 - 2.0 * t.World.BodyRadius;
            Assert.AreEqual(stopAt, w.Position.X, 1e-6, "para junto do alvo, sem atravessá-lo");
            Assert.IsTrue(dummy.Statuses.Any(s => s.Def.Id == "crippled"));
        }

        [Test]
        public void Dash_AwayMovesOppositeToTheTarget_AndSpendsEnergy()
        {
            var t = new TestWorld();
            Combatant a = t.Player("arqueiro", 5, 0, 0);
            Combatant dummy = t.Dummy(3, 0);
            SkillDef jump = t.Data.Skills["arqueiro_salto_agil"];
            double energy = a.Resource;

            Assert.AreEqual(UseSkillResult.Ok, t.Use(a, jump.Id, dummy));
            t.Flush();

            Assert.AreEqual(-9.0, a.Position.X, 1e-6);
            Assert.AreEqual(energy - jump.ResourceCost, a.Resource, 1e-6);
            Assert.AreEqual(1, t.Count(GameEventType.Dash));
        }

        [Test]
        public void Pvp_IsOffByDefault_AndOnlyHostileBetweenFactionsWhenEnabled()
        {
            var t = new TestWorld();
            Combatant a = t.Player("guerreiro", 1, 0, 0);  // concórdia
            Combatant b = t.Player("assassino", 1, 1, 0);  // legião
            Combatant c = t.Player("mago", 1, 2, 0);       // concórdia

            Assert.IsFalse(t.World.IsHostile(a, b), "PvP desligado na vertical slice");
            t.World.PvpEnabled = true;
            Assert.IsTrue(t.World.IsHostile(a, b));
            Assert.IsFalse(t.World.IsHostile(a, c), "mesma facção");
            Assert.IsFalse(t.World.IsHostile(a, a));
        }

        [Test]
        public void InputIntent_IsClampedToUnitLength_AndRejectsNaN()
        {
            var t = new TestWorld();
            Combatant w = t.Player("guerreiro", 1, 0, 0);

            t.World.SetMoveIntent(w, 100, 0); // tentativa de "speed hack" por vetor enorme
            Assert.AreEqual(1.0, w.MoveIntent.Length, 1e-9);

            t.World.SetMoveIntent(w, double.NaN, 0);
            Assert.AreEqual(1.0, w.MoveIntent.Length, 1e-9, "entrada inválida é ignorada");

            double speed = w.Stats.Get("moveSpeed");
            t.Step(1.0);
            Assert.AreEqual(speed, w.Position.X, 0.01, "anda no máximo moveSpeed por segundo");
        }

        // ---- Provocação (taunt) ----

        private static Combatant Tank(TestWorld t, double x, double z)
        {
            Combatant tank = t.Player("guerreiro", 18, x, z);
            tank.Character.AddGold(20000);
            Assert.AreEqual(LearnSkillResult.Ok, tank.Character.TryLearnSkill("guerreiro_desafio_do_campeao"));
            return tank;
        }

        [Test]
        public void Taunt_ForcesMonsterToTargetTaunter_AndTakesTopOfThreatList()
        {
            var t = new TestWorld();
            Combatant tank = Tank(t, 0, 0);
            Combatant dps = t.Player("mago", 18, 0, 4);
            Combatant m = t.Monster("broto_ancestral", 3, 2);
            t.World.AddThreat(m, dps, 100);

            t.Step(0.1);
            Assert.AreEqual(dps.Id, m.TargetId, "sem provocação, quem tem mais ameaça é o alvo");

            tank.Resource = 100; // guerreiro usa fúria, que começa vazia
            Assert.AreEqual(UseSkillResult.Ok, t.Use(tank, "guerreiro_desafio_do_campeao", m));
            t.Step(0.1);
            Assert.AreEqual(tank.Id, m.TargetId, "provocado: ataca o provocador");
            Assert.Greater(m.Threat[tank.Id], m.Threat[dps.Id], "provocador passa a liderar a ameaça");
        }

        [Test]
        public void Taunt_IsNotAppliedToPlayers()
        {
            var t = new TestWorld();
            Combatant a = t.Player("guerreiro", 18, 0, 0);
            Combatant b = t.Player("mago", 18, 1, 0);

            Assert.IsFalse(t.World.ApplyStatus(a, b, t.Data.Statuses["taunted"], 4.0, 0, DamageType.Physical, "teste"));
            t.Flush();
            Assert.AreEqual(1, t.Count(GameEventType.StatusResisted));
            Assert.IsFalse(b.Statuses.Any(x => x.Def.Id == "taunted"));
        }

        [Test]
        public void Taunt_IsHalvedOnBoss_AndCappedByMaxDuration()
        {
            var t = new TestWorld();
            Combatant tank = t.Player("guerreiro", 18, 0, 0);
            Combatant boss = t.Monster("colosso_da_raiz", 60, 0);
            double resist = boss.MonsterDef.CcResist["taunt"];

            Assert.IsTrue(t.World.ApplyStatus(tank, boss, t.Data.Statuses["taunted"], 4.0, 0, DamageType.Physical, "teste"));
            StatusInstance s = boss.Statuses.Single(x => x.Def.Id == "taunted");
            Assert.AreEqual(4.0 * resist, s.ExpiresAt - t.World.Time, 1e-6);

            var t2 = new TestWorld();
            Combatant tank2 = t2.Player("guerreiro", 18, 0, 0);
            Combatant m2 = t2.Dummy(3, 0);
            Assert.IsTrue(t2.World.ApplyStatus(tank2, m2, t2.Data.Statuses["taunted"], 60.0, 0, DamageType.Physical, "teste"));
            StatusInstance s2 = m2.Statuses.Single(x => x.Def.Id == "taunted");
            Assert.AreEqual(t2.Data.Combat.Control.MaxDuration["taunt"], s2.ExpiresAt - t2.World.Time, 1e-6);
        }

        [Test]
        public void Taunt_SkillIsPaidSlot7_AndNeedsLevel18()
        {
            var t = new TestWorld();
            SkillDef s = t.Data.Skills["guerreiro_desafio_do_campeao"];
            Assert.AreEqual(7, s.Slot);
            Assert.AreEqual(20000, s.GoldCost);
            Combatant low = t.Player("guerreiro", 5, 0, 0);
            low.Character.AddGold(20000);
            Assert.AreEqual(LearnSkillResult.LevelTooLow, low.Character.TryLearnSkill("guerreiro_desafio_do_campeao"));
        }

        [Test]
        public void MageKit_HasElementalVariety_AndMostSkillsAreArea()
        {
            var t = new TestWorld();
            var skills = t.Data.Classes["mago"].SkillIds.Select(id => t.Data.Skills[id]).ToList();
            Assert.AreEqual(9, skills.Count);
            var elements = skills.Select(x => x.Element).Distinct().ToList();
            foreach (string e in new[] { "arcane", "fire", "ice", "wind", "earth" })
                CollectionAssert.Contains(elements, e);
            int area = skills.Count(x => x.Targeting == Targeting.Ground || x.Targeting == Targeting.SelfArea);
            Assert.GreaterOrEqual(area, 6, "o mago tem poucos pontos de vida e depende de dano em área");
            Assert.IsTrue(skills.Where(x => x.Slot <= 5).All(x => x.RequiresLevel == 1 && x.GoldCost == 0));
            Assert.IsTrue(skills.Where(x => x.Slot > 5).All(x => x.GoldCost > 0));
        }

        [Test]
        public void CastWhileMoving_KeepsTheCast_MovesSlower_AndStillHits()
        {
            var t = new TestWorld();
            Combatant m = t.Player("mago", 1, 0, 0);
            Combatant dummy = t.Dummy(6, 0);
            double speed = m.Stats.Get("moveSpeed");

            Assert.IsTrue(t.Data.Skills["mago_raio_arcano"].CastWhileMoving);
            Assert.AreEqual(UseSkillResult.Ok, t.Use(m, "mago_raio_arcano", dummy));
            t.World.SetMoveIntent(m, 0, 1);
            t.Step(0.4);

            Assert.AreEqual(0, t.Count(GameEventType.CastInterrupted), "andar não interrompe");
            Assert.IsNotNull(m.Cast);
            Assert.AreEqual(speed * t.Data.Combat.CastMoveSpeedMultiplier * 0.4, m.Position.Z, 0.05, "anda mais devagar enquanto conjura");

            t.World.SetMoveIntent(m, 0, 0);
            t.Step(0.6);
            Assert.IsNull(m.Cast);
            Assert.Less(dummy.Hp, dummy.MaxHp, "a habilidade saiu e acertou");
        }

        [Test]
        public void HeavySpells_StillRequireStandingStill()
        {
            var t = new TestWorld();
            foreach (string id in new[] { "mago_bola_de_fogo", "mago_tempestade_arcana", "mago_chuva_de_meteoros" })
                Assert.IsFalse(t.Data.Skills[id].CastWhileMoving, id);
        }
    }
}
