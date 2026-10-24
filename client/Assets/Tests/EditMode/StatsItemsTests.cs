using System.Linq;
using NUnit.Framework;
using Mmorpg.Core.Characters;
using Mmorpg.Core.Data;
using Mmorpg.Core.Items;
using Mmorpg.Core.Progression;
using Mmorpg.Core.Stats;

namespace Mmorpg.Core.Tests
{
    [TestFixture]
    public class StatsItemsTests
    {
        private GameData D { get { return TestData.Data(); } }

        [Test]
        public void AllTwentyFourClasses_CanBeCreated_WithSaneStats()
        {
            foreach (ClassDef cls in D.Classes.Values)
            {
                PlayerCharacter pc = PlayerCharacter.CreateNew(D, "Heroi", cls.Id);
                StatSheet s = pc.ComputeStats();
                Assert.Greater(s.Get("maxHp"), 0, cls.Id);
                Assert.Greater(s.Get("moveSpeed"), 0, cls.Id);
                foreach (var kv in s.All) Assert.IsFalse(double.IsNaN(kv.Value) || double.IsInfinity(kv.Value), cls.Id + "." + kv.Key);
                foreach (string item in cls.StartingItems)
                    Assert.IsTrue(pc.Equipped.Any(e => e.Value == item), cls.Id + " deve começar com " + item + " equipado");
            }
        }

        [Test]
        public void Stats_GrowWithLevel()
        {
            foreach (string cls in new[] { "guerreiro", "mago", "sacerdote" })
            {
                PlayerCharacter pc = PlayerCharacter.CreateNew(D, "Heroi", cls);
                double hp1 = pc.ComputeStats().Get("maxHp");
                TestWorld.SetLevel(pc, 20);
                double hp20 = pc.ComputeStats().Get("maxHp");
                TestWorld.SetLevel(pc, 55);
                double hp55 = pc.ComputeStats().Get("maxHp");
                Assert.Greater(hp20, hp1, cls);
                Assert.Greater(hp55, hp20, cls);
            }
        }

        [Test]
        public void Equipment_AddsStats_AndSetBonusesRequireEnoughPieces()
        {
            PlayerCharacter pc = PlayerCharacter.CreateNew(D, "Heroi", "assassino");
            TestWorld.SetLevel(pc, 5);
            double baseSpeed = pc.ComputeStats().Get("moveSpeed");
            SetDef set = D.Sets.Values.First();
            Assert.GreaterOrEqual(set.Pieces.Count, 3);

            pc.AddGold(0);
            int equipped = 0;
            double previousBonusCount = pc.ActiveSetBonuses().Count;
            foreach (string piece in set.Pieces)
            {
                pc.Inventory.Add(piece, 1);
                int slot = Enumerable.Range(0, pc.Inventory.Capacity).First(i => pc.Inventory.Get(i) != null && pc.Inventory.Get(i).ItemId == piece);
                Assert.AreEqual(EquipResult.Ok, pc.Equip(slot), piece);
                equipped++;
                int active = pc.ActiveSetBonuses().Count;
                int expected = set.Bonuses.Count(b => b.Pieces <= equipped);
                Assert.AreEqual(expected, active, "bônus ativos com " + equipped + " peças");
                Assert.GreaterOrEqual(active, previousBonusCount);
                previousBonusCount = active;
            }
            Assert.Greater(pc.ComputeStats().Get("moveSpeed"), baseSpeed - 1e-9);
        }

        [Test]
        public void Equip_RespectsLevelClassAndSlotRules()
        {
            PlayerCharacter mage = PlayerCharacter.CreateNew(D, "Mago", "mago");
            mage.Inventory.Add("espada_de_ferro_reforcada", 1);
            int slot = Enumerable.Range(0, mage.Inventory.Capacity).First(i => mage.Inventory.Get(i) != null && mage.Inventory.Get(i).ItemId == "espada_de_ferro_reforcada");
            EquipResult r = mage.Equip(slot);
            Assert.AreNotEqual(EquipResult.Ok, r, "mago não pode usar espada (e é nível 1 para um item de nível 4)");

            PlayerCharacter warrior = PlayerCharacter.CreateNew(D, "Guerreiro", "guerreiro");
            warrior.Inventory.Add("espada_de_ferro_reforcada", 1);
            slot = Enumerable.Range(0, warrior.Inventory.Capacity).First(i => warrior.Inventory.Get(i) != null && warrior.Inventory.Get(i).ItemId == "espada_de_ferro_reforcada");
            Assert.AreEqual(EquipResult.LevelTooLow, warrior.Equip(slot));
            TestWorld.SetLevel(warrior, 4);
            Assert.AreEqual(EquipResult.Ok, warrior.Equip(slot));
            Assert.AreEqual("espada_de_ferro_reforcada", warrior.EquippedItemId("weapon"));
            Assert.IsTrue(warrior.Inventory.CountOf("espada_enferrujada") == 1, "a arma antiga volta para o inventário");
        }

        [Test]
        public void Equip_Swap_WorksWithFullInventory_UsingTheSlotTheNewItemFreed()
        {
            PlayerCharacter w = PlayerCharacter.CreateNew(D, "Guerreiro", "guerreiro");
            TestWorld.SetLevel(w, 4);
            w.Inventory.Add("espada_de_ferro_reforcada", 1);
            // Enche o resto do inventário com itens de pilha 1.
            while (w.Inventory.FreeSlots > 0) w.Inventory.Add("espada_enferrujada", 1);
            int slot = Enumerable.Range(0, w.Inventory.Capacity).First(i => w.Inventory.Get(i).ItemId == "espada_de_ferro_reforcada");
            // Equipar tira 1 do slot (abre espaço para a arma antiga) -> deve funcionar.
            Assert.AreEqual(EquipResult.Ok, w.Equip(slot));
            Assert.AreEqual(0, w.Inventory.FreeSlots);
            Assert.AreEqual("espada_de_ferro_reforcada", w.EquippedItemId("weapon"));
        }

        [Test]
        public void Inventory_Add_ReturnsExactLeftover_AndRemoveIsAtomic()
        {
            var inv = new Inventory(D, 2);
            int max = D.Items["pele_de_lobo"].MaxStack;
            Assert.Greater(max, 1, "material deve empilhar");
            int leftover = inv.Add("pele_de_lobo", max * 2 + 7);
            Assert.AreEqual(7, leftover);
            Assert.AreEqual(max * 2, inv.CountOf("pele_de_lobo"));
            Assert.IsFalse(inv.CanAdd("pele_de_lobo", 1));
            Assert.IsFalse(inv.Remove("pele_de_lobo", max * 2 + 1), "remover mais do que existe não pode alterar nada");
            Assert.AreEqual(max * 2, inv.CountOf("pele_de_lobo"));
            Assert.IsTrue(inv.Remove("pele_de_lobo", max + 3));
            Assert.AreEqual(max - 3, inv.CountOf("pele_de_lobo"));
        }

        [Test]
        public void Inventory_RejectsUnknownItems_AndInvalidDirectSets()
        {
            var inv = new Inventory(D, 4);
            Assert.Throws<System.ArgumentException>(() => inv.Add("nao_existe", 1));
            Assert.IsFalse(inv.TrySetSlot(0, "nao_existe", 1));
            Assert.IsFalse(inv.TrySetSlot(9, "pele_de_lobo", 1), "slot fora do inventário");
            Assert.IsFalse(inv.TrySetSlot(0, "pele_de_lobo", 0), "contagem zero");
            Assert.IsFalse(inv.TrySetSlot(0, "espada_enferrujada", 2), "equipamento não empilha");
            Assert.IsTrue(inv.TrySetSlot(0, "pele_de_lobo", 1));
            Assert.IsFalse(inv.TrySetSlot(0, "pele_de_lobo", 1), "slot ocupado");
        }

        [Test]
        public void XpCurve_IsIncreasing_AndEndsAtMaxLevel()
        {
            long previous = 0;
            for (int lvl = 1; lvl < D.Progression.MaxLevel; lvl++)
            {
                long need = LevelCurve.XpToNext(D, lvl);
                Assert.Greater(need, previous - 1, "nível " + lvl);
                Assert.Greater(need, 0, "nível " + lvl);
                previous = need;
            }
            Assert.AreEqual(0L, LevelCurve.XpToNext(D, D.Progression.MaxLevel));
        }

        [Test]
        public void LevelDifference_PenalisesOverlevelledPlayers_AndCapsBonus()
        {
            Assert.AreEqual(1.0, LevelCurve.LevelDifferenceFactor(D, 5, 5), 1e-9);
            Assert.Less(LevelCurve.LevelDifferenceFactor(D, 20, 5), 1.0);
            Assert.GreaterOrEqual(LevelCurve.LevelDifferenceFactor(D, 55, 1), D.Progression.LevelDifference.MinFactor - 1e-9);
            Assert.LessOrEqual(LevelCurve.LevelDifferenceFactor(D, 1, 50), D.Progression.LevelDifference.MaxBonus + 1e-9);
            Assert.IsTrue(LevelCurve.IsAboveLootCutoff(D, 12, 1));
            Assert.IsFalse(LevelCurve.IsAboveLootCutoff(D, 5, 1));
        }

        [Test]
        public void MonsterStats_ScaleWithLevelAndCategory()
        {
            StatSheet common = StatCalculator.ComputeMonster(D, D.Monsters["lobo_selvagem"]);
            StatSheet elite = StatCalculator.ComputeMonster(D, D.Monsters["sentinela_de_pedra_rachada"]);
            StatSheet boss = StatCalculator.ComputeMonster(D, D.Monsters["colosso_da_raiz"]);
            Assert.Greater(elite.Get("maxHp"), common.Get("maxHp"));
            Assert.Greater(boss.Get("maxHp"), elite.Get("maxHp"));
        }
    }
}
