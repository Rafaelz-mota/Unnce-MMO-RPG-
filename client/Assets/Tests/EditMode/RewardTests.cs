using System.Linq;
using NUnit.Framework;
using Mmorpg.Core.Combat;
using Mmorpg.Core.Common;
using Mmorpg.Core.Data;
using Mmorpg.Core.Events;
using Mmorpg.Core.Progression;
using Mmorpg.Core.Rewards;

namespace Mmorpg.Core.Tests
{
    /// <summary>XP, ouro e saque: quem recebe, quanto, limites por raridade e trilha de auditoria.</summary>
    [TestFixture]
    public class RewardTests
    {
        private const string Strike = "guerreiro_golpe_poderoso";

        /// <summary>Leva o monstro a 1 de vida, cola o jogador nele e bate até morrer (o golpe conta como contribuição).</summary>
        private static void Slay(TestWorld t, Combatant player, Combatant monster)
        {
            player.Position = monster.Position + new Vec2(1.0, 0);
            monster.Hp = 1;
            for (int i = 0; i < 60 && !monster.Dead; i++)
            {
                t.Use(player, Strike, monster);
                t.Step(1.0);
            }
            Assert.IsTrue(monster.Dead, "o monstro deveria ter morrido");
            t.Flush();
        }

        [Test]
        public void Kill_GrantsXpGoldAndLoot_ToTheContributor_AndWritesAnAuditTrail()
        {
            var t = new TestWorld(new FixedRng(0.5));
            Combatant p = t.Player("guerreiro", 1, 0, 0, "Ana");
            Combatant m = t.Monster("broto_ancestral", 5, 0);
            long expectedXp = LevelCurve.MonsterXp(t.Data, t.Data.Monsters["broto_ancestral"], p.Level);

            Slay(t, p, m);

            GameEvent xp = t.Of(GameEventType.XpGained).Single();
            Assert.AreEqual(p.Id, xp.TargetId);
            Assert.AreEqual(expectedXp, (long)xp.Amount);

            // FixedRng(0.5): ouro = 1 + 0,5 * (4 - 1 + 1) = 3; lasca (60%) cai com NextInt(1,3) = 2; capuz (4%) não cai.
            Assert.AreEqual(3, (long)t.Of(GameEventType.GoldGained).Single().Amount);
            Assert.AreEqual(3, p.Character.Gold);
            Assert.AreEqual(2, p.Character.Inventory.CountOf("lasca_de_madeira_antiga"));
            Assert.AreEqual(0, p.Character.Inventory.CountOf("capuz_de_couro"));

            var kinds = t.Rewards.Audit.Select(a => a.Kind).ToList();
            Assert.IsTrue(kinds.Contains("xp"), "xp");
            Assert.IsTrue(kinds.Contains("gold"), "gold");
            Assert.IsTrue(kinds.Contains("item"), "item");
            Assert.IsTrue(t.Rewards.Audit.All(a => a.Player == "Ana" && a.MonsterId == "broto_ancestral"));
        }

        [Test]
        public void Kill_WithoutAnyContribution_RewardsNobody()
        {
            var t = new TestWorld(new FixedRng(0.5));
            Combatant p = t.Player("guerreiro", 1, 0, 0);
            Combatant m = t.Monster("broto_ancestral", 30, 0);

            t.World.Kill(m, p.Id);
            t.Flush();

            Assert.AreEqual(0, t.Count(GameEventType.XpGained));
            Assert.AreEqual(0, t.Count(GameEventType.GoldGained));
            Assert.AreEqual(0, t.Count(GameEventType.ItemLooted));
            Assert.AreEqual(0, t.Rewards.Audit.Count);
            Assert.AreEqual(0, p.Character.Xp);
        }

        [Test]
        public void PlayerBelowTheMinimumShare_ReceivesNothing()
        {
            var t = new TestWorld(new FixedRng(0.5));
            Combatant a = t.Player("guerreiro", 1, 0, 0, "A");
            Combatant b = t.Player("guerreiro", 1, 0, 3, "B");
            Combatant m = t.Monster("broto_ancestral", 5, 0);
            m.Contribution[a.Id] = 90;
            m.Contribution[b.Id] = 1; // ~1% do total, abaixo do mínimo de 10%

            Slay(t, a, m);

            Assert.AreEqual(1, t.Count(GameEventType.XpGained));
            Assert.IsTrue(t.Of(GameEventType.XpGained).All(e => e.TargetId == a.Id));
            Assert.AreEqual(0, b.Character.Xp);
            Assert.AreEqual(0, b.Character.Gold);
        }

        [Test]
        public void PlayerFarAboveTheMonsterLevel_GetsXpButNoGoldOrItems()
        {
            var t = new TestWorld(new FixedRng(0.5));
            int cutoff = t.Data.Progression.LevelDifference.LootCutoffLevels;
            Combatant p = t.Player("guerreiro", 1 + cutoff, 0, 0);
            Combatant m = t.Monster("broto_ancestral", 5, 0);

            Slay(t, p, m);

            Assert.AreEqual(1, t.Count(GameEventType.XpGained));
            Assert.AreEqual(0, t.Count(GameEventType.GoldGained));
            Assert.AreEqual(0, t.Count(GameEventType.ItemLooted));
            Assert.AreEqual(0, p.Character.Gold);
            Assert.IsTrue(t.Rewards.Audit.Any(a => a.Kind == "item_skipped"), "a recusa precisa ficar registrada");
        }

        [Test]
        public void FullInventory_LosesTheDropAndTellsThePlayer()
        {
            var t = new TestWorld(new FixedRng(0.5));
            Combatant p = t.Player("guerreiro", 1, 0, 0);
            int leftover = p.Character.Inventory.Add("capuz_de_couro", p.Character.Inventory.Capacity);
            Assert.AreEqual(0, leftover);
            Assert.AreEqual(0, p.Character.Inventory.FreeSlots);
            Combatant m = t.Monster("broto_ancestral", 5, 0);

            Slay(t, p, m);

            Assert.GreaterOrEqual(t.Count(GameEventType.InventoryFull), 1);
            Assert.AreEqual(0, t.Count(GameEventType.ItemLooted));
            Assert.AreEqual(0, p.Character.Inventory.CountOf("lasca_de_madeira_antiga"));
            Assert.IsTrue(t.Rewards.Audit.Any(a => a.Kind == "item_skipped" && a.Note == "inventário cheio"));
        }

        [Test]
        public void RegionalBoss_WithAverageLuck_NeverDropsUniqueOrLegendary_ButAlwaysPaysItsGuaranteedLoot()
        {
            var t = new TestWorld(new FixedRng(0.5));
            Combatant p = t.Player("guerreiro", 7, 0, 0);
            TestWorld.ForceStats(p, "maxHp", 1e7);
            p.Hp = 1e7;
            Combatant boss = t.Monster("colosso_da_raiz", 6, 0);

            Slay(t, p, boss);

            var looted = t.Of(GameEventType.ItemLooted).Select(e => t.Data.Items[e.ItemId].Rarity).ToList();
            Assert.IsFalse(looted.Contains("unique"), "unique");
            Assert.IsFalse(looted.Contains("legendary"), "legendary");
            Assert.AreEqual(2, p.Character.Inventory.CountOf("fragmento_de_raiz_ancestral")); // 1 + (int)(0,5 * 3)
            Assert.AreEqual(5, p.Character.Inventory.CountOf("lasca_de_madeira_antiga"));       // 3 + (int)(0,5 * 4)
            Assert.AreEqual(205, p.Character.Gold);                                              // 150 + 0,5 * 111 = 205,5 -> 205
        }

        [Test]
        public void RegionalBoss_WithExtremeLuck_StillRespectsRarityCapsAndItemLimit()
        {
            var t = new TestWorld(new FixedRng(0.0)); // todo item com chance > 0 "cai"
            Combatant p = t.Player("guerreiro", 7, 0, 0);
            TestWorld.ForceStats(p, "maxHp", 1e7);
            p.Hp = 1e7;
            Combatant boss = t.Monster("colosso_da_raiz", 6, 0);
            LootTableDef table = t.Data.LootTables[boss.MonsterDef.LootTableId];

            Slay(t, p, boss);

            var rarities = t.Of(GameEventType.ItemLooted).Select(e => t.Data.Items[e.ItemId].Rarity).ToList();
            Assert.LessOrEqual(rarities.Count, table.MaxItemsPerPlayer);
            foreach (var cap in table.RarityCapsPerKill)
                Assert.LessOrEqual(rarities.Count(r => r == cap.Key), cap.Value, "teto da raridade " + cap.Key);
        }
    }
}
