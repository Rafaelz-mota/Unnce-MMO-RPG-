using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Mmorpg.Core.Aiming;
using Mmorpg.Core.Characters;
using Mmorpg.Core.Combat;
using Mmorpg.Core.Common;
using Mmorpg.Core.Data;
using Mmorpg.Core.Events;
using Mmorpg.Core.Hotbar;
using Mmorpg.Core.Presentation;

namespace Mmorpg.Core.Tests
{
    /// <summary>Seleção de alvo, mira de habilidades, barra de habilidades e mensagens do registro.</summary>
    [TestFixture]
    public class AimingAndHotbarTests
    {
        // ------------------------------------------------------------ TargetSelector
        [Test]
        public void Nearest_PicksTheClosestValidEnemy_IgnoringDeadAndInvulnerable()
        {
            var t = new TestWorld();
            Combatant p = t.Player("guerreiro", 1, 0, 0);
            Combatant far = t.Dummy(9, 0);
            Combatant mid = t.Dummy(6, 0);
            Combatant near = t.Dummy(3, 0);

            Assert.AreEqual(near.Id, TargetSelector.Nearest(t.World, p, 20).Id);
            near.Dead = true;
            Assert.AreEqual(mid.Id, TargetSelector.Nearest(t.World, p, 20).Id);
            mid.Invulnerable = true;
            Assert.AreEqual(far.Id, TargetSelector.Nearest(t.World, p, 20).Id);
            Assert.IsNull(TargetSelector.Nearest(t.World, p, 5), "ninguém dentro de 5 m");
        }

        [Test]
        public void CycleNext_WalksFromNearestToFarthest_ThenWraps()
        {
            var t = new TestWorld();
            Combatant p = t.Player("guerreiro", 1, 0, 0);
            Combatant a = t.Dummy(3, 0);
            Combatant b = t.Dummy(6, 0);
            Combatant c = t.Dummy(9, 0);

            int id = TargetSelector.CycleNext(t.World, p, 0, 20);
            Assert.AreEqual(a.Id, id);
            id = TargetSelector.CycleNext(t.World, p, id, 20);
            Assert.AreEqual(b.Id, id);
            id = TargetSelector.CycleNext(t.World, p, id, 20);
            Assert.AreEqual(c.Id, id);
            id = TargetSelector.CycleNext(t.World, p, id, 20);
            Assert.AreEqual(a.Id, id, "volta ao começo");
            Assert.AreEqual(0, TargetSelector.CycleNext(t.World, p, 0, 1.0), "sem inimigos no alcance");
        }

        [Test]
        public void IsValid_RejectsMissingSelfDeadAndUnknownTargets()
        {
            var t = new TestWorld();
            Combatant p = t.Player("guerreiro", 1, 0, 0);
            Combatant m = t.Dummy(3, 0);

            Assert.IsTrue(TargetSelector.IsValid(t.World, p, m.Id));
            Assert.IsFalse(TargetSelector.IsValid(t.World, p, 0));
            Assert.IsFalse(TargetSelector.IsValid(t.World, p, p.Id), "não é hostil a si mesmo");
            Assert.IsFalse(TargetSelector.IsValid(t.World, p, 99999));
            m.Dead = true;
            Assert.IsFalse(TargetSelector.IsValid(t.World, p, m.Id));
        }

        // ------------------------------------------------------------ SkillTargeting
        [Test]
        public void Aim_SelfSkills_TargetTheCaster()
        {
            var t = new TestWorld();
            Combatant p = t.Player("guerreiro", 7, 0, 0);
            SkillAim aim = SkillTargeting.Aim(t.World, p, t.Data.Skills["guerreiro_grito_de_guerra"], 0, true);
            Assert.IsTrue(aim.Ok);
            Assert.AreEqual(p.Id, aim.TargetId);
        }

        [Test]
        public void Aim_EnemySkill_UsesSelectionFirst_ThenAutoTargetOnlyWhenAllowed()
        {
            var t = new TestWorld();
            Combatant p = t.Player("guerreiro", 7, 0, 0);
            Combatant near = t.Dummy(1, 0);
            Combatant other = t.Dummy(2, 0);
            SkillDef strike = t.Data.Skills["guerreiro_golpe_poderoso"]; // alcance 2,5

            SkillAim noAuto = SkillTargeting.Aim(t.World, p, strike, 0, false);
            Assert.IsFalse(noAuto.Ok);
            Assert.AreEqual(UseSkillResult.NoTarget, noAuto.Failure);

            SkillAim auto = SkillTargeting.Aim(t.World, p, strike, 0, true);
            Assert.IsTrue(auto.Ok);
            Assert.AreEqual(near.Id, auto.TargetId);

            SkillAim selected = SkillTargeting.Aim(t.World, p, strike, other.Id, true);
            Assert.AreEqual(other.Id, selected.TargetId, "o alvo selecionado manda, mesmo não sendo o mais próximo");
        }

        [Test]
        public void Aim_SelectedTargetOutOfRange_IsNotSwappedSilently_AndTheWorldRefusesIt()
        {
            var t = new TestWorld();
            Combatant p = t.Player("guerreiro", 1, 0, 0);
            Combatant far = t.Dummy(10, 0);
            Combatant near = t.Dummy(1, 0);
            SkillDef strike = t.Data.Skills["guerreiro_golpe_poderoso"];

            SkillAim aim = SkillTargeting.Aim(t.World, p, strike, far.Id, true);
            Assert.IsTrue(aim.Ok);
            Assert.AreEqual(far.Id, aim.TargetId, "não troca de alvo sem avisar");
            Assert.AreEqual(UseSkillResult.OutOfRange, t.World.UseSkill(p, strike.Id, aim.TargetId, aim.Ground));
            Assert.IsTrue(near.Id != far.Id);
        }

        [Test]
        public void Aim_GroundSkill_FallsBackToAPointInFrontOfTheCaster()
        {
            var t = new TestWorld();
            Combatant p = t.Player("mago", 10, 0, 0);
            SkillDef storm = t.Data.Skills["mago_tempestade_arcana"];

            SkillAim aim = SkillTargeting.Aim(t.World, p, storm, 0, true);
            Assert.IsTrue(aim.Ok);
            Assert.AreEqual(0, aim.TargetId);
            Assert.AreEqual(0.0, aim.Ground.X, 1e-9);
            Assert.AreEqual(SkillTargeting.DefaultGroundDistance, aim.Ground.Z, 1e-9);

            Combatant far = t.Dummy(0, 40);
            SkillAim tooFar = SkillTargeting.Aim(t.World, p, storm, far.Id, true);
            Assert.IsFalse(tooFar.Ok);
            Assert.AreEqual(UseSkillResult.OutOfRange, tooFar.Failure);
        }

        [Test]
        public void Aim_AllySkill_DefaultsToTheCaster()
        {
            var t = new TestWorld();
            Combatant p = t.Player("sacerdote", 1, 0, 0);
            SkillAim aim = SkillTargeting.Aim(t.World, p, t.Data.Skills["sacerdote_toque_de_luz"], 0, true);
            Assert.IsTrue(aim.Ok);
            Assert.AreEqual(p.Id, aim.TargetId);
        }

        // ------------------------------------------------------------ HotbarModel
        [Test]
        public void Hotbar_ShowsReadyLockedAndEmptySlots_ForAFullKit()
        {
            GameData d = TestData.Fresh();
            PlayerCharacter pc = PlayerCharacter.CreateNew(d, "Rex", "guerreiro");
            IList<HotbarSlot> bar = HotbarModel.Build(pc);

            Assert.AreEqual(d.Rules.SkillsPerClass, bar.Count);
            Assert.AreEqual(Enumerable.Range(1, d.Rules.SkillsPerClass), bar.Select(s => s.Number));
            Assert.AreEqual(HotbarSlotState.Ready, bar[0].State);
            for (int i = 0; i < 5; i++) Assert.AreEqual(HotbarSlotState.Ready, bar[i].State, "as 5 primeiras são liberadas no nível 1");
            Assert.AreEqual(HotbarSlotState.LockedByLevel, bar[5].State, "paga, mas nível insuficiente");
            Assert.AreEqual(HotbarSlotState.LockedByLevel, bar[8].State, "slot 9 exige nível 40");
            Assert.IsNotNull(bar[8].Skill);
        }

        [Test]
        public void Hotbar_PaidSlot_BecomesPurchasable_ThenReadyAfterBuying()
        {
            GameData d = TestData.Fresh();
            PlayerCharacter pc = PlayerCharacter.CreateNew(d, "Rex", "guerreiro");
            TestWorld.SetLevel(pc, 10);

            HotbarSlot paid = HotbarModel.Build(pc)[5];
            Assert.AreEqual(HotbarSlotState.Purchasable, paid.State);
            Assert.AreEqual(10000, paid.GoldCost);
            Assert.IsFalse(paid.CanAfford);

            pc.AddGold(10000);
            Assert.IsTrue(HotbarModel.Build(pc)[5].CanAfford);

            Assert.AreEqual(LearnSkillResult.Ok, pc.TryLearnSkill(paid.Skill.Id));
            Assert.AreEqual(HotbarSlotState.Ready, HotbarModel.Build(pc)[5].State);
            Assert.AreEqual(0, pc.Gold);
        }

        // ------------------------------------------------------------ EventLog
        [Test]
        public void EventLog_ShowsOnlyWhatConcernsTheLocalPlayer_AndMarksRarityWithText()
        {
            GameData d = TestData.Data();
            const int me = 5, other = 6;

            Assert.AreEqual("+25 XP", EventLog.Format(d, new GameEvent { Type = GameEventType.XpGained, TargetId = me, Amount = 25 }, me));
            Assert.IsNull(EventLog.Format(d, new GameEvent { Type = GameEventType.XpGained, TargetId = other, Amount = 25 }, me));
            Assert.AreEqual("+7 ouro", EventLog.Format(d, new GameEvent { Type = GameEventType.GoldGained, TargetId = me, Amount = 7 }, me));

            string loot = EventLog.Format(d, new GameEvent { Type = GameEventType.ItemLooted, TargetId = me, ItemId = "fragmento_de_raiz_ancestral", Count = 2 }, me);
            Assert.IsTrue(loot.Contains("2x") && loot.Contains("[R]"), loot);

            Assert.AreEqual("Você foi derrotado.", EventLog.Format(d, new GameEvent { Type = GameEventType.Died, TargetId = me }, me));
            Assert.IsNull(EventLog.Format(d, new GameEvent { Type = GameEventType.Died, TargetId = other, SourceId = other }, me));
            Assert.AreEqual("A seiva ancestral desperta!", EventLog.Format(d, new GameEvent { Type = GameEventType.BossPhase, Text = "A seiva ancestral desperta!" }, me));
            Assert.IsNull(EventLog.Format(d, new GameEvent { Type = GameEventType.Damage, TargetId = me, Amount = 10 }, me), "dano vira número flutuante, não mensagem");
        }

        [Test]
        public void EventLog_DescribesSkillRefusals_ButStaysQuietOnGlobalCooldown()
        {
            Assert.IsNull(EventLog.Describe(UseSkillResult.Ok));
            Assert.IsNull(EventLog.Describe(UseSkillResult.GlobalCooldown));
            Assert.IsNotNull(EventLog.Describe(UseSkillResult.OutOfRange));
            Assert.IsNotNull(EventLog.Describe(UseSkillResult.NotEnoughResource));
            Assert.IsNotNull(EventLog.Describe(UseSkillResult.NoTarget));
            Assert.IsNotNull(EventLog.Describe(UseSkillResult.OnCooldown));
        }

        [Test]
        public void TargetLock_KeepsTheTarget_UntilUnlockedSwitchedOrDead()
        {
            GameData data = TestData.Fresh();
            PlayerCharacter pc = PlayerCharacter.CreateNew(data, "Heroi", "arqueiro");
            var s = Mmorpg.Core.Session.GameSession.Start(data, pc, "vale_do_despertar", new Mmorpg.Core.Common.SystemRng(4), null);
            var def = data.Monsters["broto_ancestral"];
            def.AggroRange = 0;
            def.MoveSpeed = 0;
            def.HpMultiplier = 1000;
            Combatant a = s.Sim.World.AddMonster(def, new Mmorpg.Core.Common.Vec2(s.Player.Position.X, s.Player.Position.Z + 8), null);
            Combatant b = s.Sim.World.AddMonster(def, new Mmorpg.Core.Common.Vec2(s.Player.Position.X, s.Player.Position.Z + 12), null);

            s.SelectTarget(b.Id);
            Assert.IsTrue(s.LockTarget());
            Assert.IsTrue(s.TargetLocked);
            Assert.AreEqual(b.Id, s.SelectedTargetId);

            s.SelectTarget(0);                       // clique em nada não solta a mira
            Assert.AreEqual(b.Id, s.SelectedTargetId);
            s.ClearTarget();                         // primeiro comando só destrava
            Assert.IsFalse(s.TargetLocked);
            Assert.AreEqual(b.Id, s.SelectedTargetId);

            s.LockTarget();
            s.SelectTarget(a.Id);                    // trocar de propósito move a trava
            Assert.IsTrue(s.TargetLocked);
            Assert.AreEqual(a.Id, s.SelectedTargetId);

            s.UseSkill("arqueiro_tiro_preciso");     // usa o alvo travado, mesmo havendo outro mais perto/longe
            s.Tick(0.05);
            Assert.AreEqual(a.Id, s.SelectedTargetId);

            a.Dead = true;                           // morreu: destrava
            s.Tick(0.05);
            Assert.IsFalse(s.TargetLocked);
            Assert.AreEqual(0, s.SelectedTargetId);
        }
    }
}
