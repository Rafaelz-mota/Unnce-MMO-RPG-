using System.Linq;
using NUnit.Framework;
using Mmorpg.Core.Characters;
using Mmorpg.Core.Data;
using Mmorpg.Core.Items;
using Mmorpg.Core.Shop;

namespace Mmorpg.Core.Tests
{
    [TestFixture]
    public class ShopTests
    {
        private const string Shop = "mercador_do_vale";

        private static PlayerCharacter NewPlayer(GameData d, string cls, int level = 1, long gold = 0)
        {
            PlayerCharacter pc = PlayerCharacter.CreateNew(d, "t_" + cls, cls);
            TestWorld.SetLevel(pc, level);
            pc.AddGold(gold);
            return pc;
        }

        [Test]
        public void Buy_ChargesTheExactPrice_AndAddsTheItem()
        {
            GameData d = TestData.Data();
            PlayerCharacter pc = NewPlayer(d, "guerreiro", 1, 1000);
            long price = d.Shops[Shop].Stock.Single(e => e.ItemId == "loja_sword_t1").Price;
            long spent;
            Assert.AreEqual(ShopResult.Ok, ShopService.Buy(d, pc, Shop, "loja_sword_t1", 1, out spent));
            Assert.AreEqual(price, spent);
            Assert.AreEqual(1000 - price, pc.Gold);
            Assert.AreEqual(1, pc.Inventory.CountOf("loja_sword_t1"));
        }

        [Test]
        public void Buy_RefusesWithoutChangingAnythingWhenInvalid()
        {
            GameData d = TestData.Data();
            PlayerCharacter pc = NewPlayer(d, "guerreiro", 1, 10);
            long spent;
            Assert.AreEqual(ShopResult.NotEnoughGold, ShopService.Buy(d, pc, Shop, "loja_sword_t1", 1, out spent));
            Assert.AreEqual(ShopResult.UnknownShop, ShopService.Buy(d, pc, "nao_existe", "loja_sword_t1", 1, out spent));
            Assert.AreEqual(ShopResult.NotInStock, ShopService.Buy(d, pc, Shop, "espada_enferrujada", 1, out spent));
            Assert.AreEqual(ShopResult.InvalidCount, ShopService.Buy(d, pc, Shop, "loja_sword_t1", 0, out spent));
            Assert.AreEqual(ShopResult.InvalidCount, ShopService.Buy(d, pc, Shop, "loja_sword_t1", -3, out spent));
            Assert.AreEqual(10, pc.Gold);
        }

        [Test]
        public void Buy_RefusesWhenInventoryIsFull_WithoutTakingGold()
        {
            GameData d = TestData.Data();
            PlayerCharacter pc = NewPlayer(d, "guerreiro", 1, 100000);
            long spent;
            while (pc.Inventory.FreeSlots > 0) pc.Inventory.Add("loja_sword_t1", 1);
            long before = pc.Gold;
            Assert.AreEqual(ShopResult.InventoryFull, ShopService.Buy(d, pc, Shop, "loja_sword_t1", 1, out spent));
            Assert.AreEqual(before, pc.Gold);
        }

        [Test]
        public void Sell_PaysTheSellValue_AndRemovesTheItem_AndNeverBeatsBuying()
        {
            GameData d = TestData.Data();
            PlayerCharacter pc = NewPlayer(d, "guerreiro", 1, 1000);
            long spent, earned;
            ShopService.Buy(d, pc, Shop, "loja_sword_t1", 1, out spent);
            int slot = Enumerable.Range(0, pc.Inventory.Capacity).First(s => pc.Inventory.Get(s) != null && pc.Inventory.Get(s).ItemId == "loja_sword_t1");
            Assert.AreEqual(ShopResult.Ok, ShopService.Sell(d, pc, slot, 1, out earned));
            Assert.AreEqual(d.Items["loja_sword_t1"].SellValue, earned);
            Assert.Less(earned, spent, "comprar e revender não pode dar lucro");
            Assert.AreEqual(0, pc.Inventory.CountOf("loja_sword_t1"));
            Assert.AreEqual(ShopResult.EmptySlot, ShopService.Sell(d, pc, slot, 1, out earned));
        }

        [Test]
        public void ShopSells_AllArmorSlotsWeaponsNecklacesAndRings()
        {
            GameData d = TestData.Data();
            var items = d.Shops[Shop].Stock.Select(e => d.Items[e.ItemId]).ToList();
            foreach (string slot in new[] { "weapon", "head", "chest", "legs", "feet", "necklace", "ring" })
                Assert.IsTrue(items.Any(i => i.Slot == slot), "sem itens do slot " + slot);
            foreach (string wt in d.Classes.Values.SelectMany(c => c.WeaponTypes).Distinct())
                Assert.IsTrue(items.Any(i => i.WeaponType == wt), "sem arma do tipo " + wt);
        }

        [Test]
        public void EveryClass_CanBuyAndEquipAFullSetAtEveryTier()
        {
            GameData d = TestData.Data();
            foreach (ClassDef c in d.Classes.Values)
            {
                PlayerCharacter pc = NewPlayer(d, c.Id, 50, 5000000);
                foreach (string slot in new[] { "weapon", "head", "chest", "legs", "feet", "necklace", "ring" })
                {
                    ItemDef pick = d.Shops[Shop].Stock.Select(e => d.Items[e.ItemId])
                        .Where(i => i.Slot == slot && i.RequiredLevel == 50 && ItemRules.CanEquip(d, c, 50, i) == EquipResult.Ok)
                        .OrderByDescending(i => i.SellValue).FirstOrDefault();
                    Assert.IsNotNull(pick, c.Id + " não encontra item de nível 50 para " + slot);
                    long spent;
                    Assert.AreEqual(ShopResult.Ok, ShopService.Buy(d, pc, Shop, pick.Id, 1, out spent), c.Id + " " + pick.Id);
                    int inv = Enumerable.Range(0, pc.Inventory.Capacity).First(s => pc.Inventory.Get(s) != null && pc.Inventory.Get(s).ItemId == pick.Id);
                    Assert.AreEqual(EquipResult.Ok, pc.Equip(inv), c.Id + " " + pick.Id);
                }
            }
        }

        [Test]
        public void Prices_GrowWithTier_AndGearRequiresItsLevel()
        {
            GameData d = TestData.Data();
            PlayerCharacter low = NewPlayer(d, "guerreiro", 1, 0);
            long prev = 0;
            for (int t = 1; t <= 6; t++)
            {
                long p = d.Shops[Shop].Stock.Single(e => e.ItemId == "loja_sword_t" + t).Price;
                Assert.Greater(p, prev);
                prev = p;
            }
            low.Inventory.Add("loja_sword_t6", 1);
            Assert.AreEqual(EquipResult.LevelTooLow, low.Equip(Enumerable.Range(0, low.Inventory.Capacity).First(s => low.Inventory.Get(s) != null)));
        }
    }
}
