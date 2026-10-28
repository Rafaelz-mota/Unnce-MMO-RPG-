using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Mmorpg.Core.Data;
using Mmorpg.Core.Presentation;

namespace Mmorpg.Core.Tests
{
    /// <summary>Catálogo de aparência (presentation.json): cobertura das 24 classes, silhuetas distintas e validação.</summary>
    [TestFixture]
    public class PresentationTests
    {
        private static string Json()
        {
            return File.ReadAllText(Path.Combine(TestData.Directory(), "presentation.json"));
        }

        private static PresentationCatalog Load(out List<string> problems, string json = null)
        {
            problems = new List<string>();
            return PresentationCatalog.Parse(json ?? Json(), TestData.Data(), problems);
        }

        [Test]
        public void RealCatalog_IsValid_AndCoversEveryClass()
        {
            List<string> problems;
            PresentationCatalog c = Load(out problems);
            Assert.IsEmpty(problems, string.Join("\n", problems));
            Assert.AreEqual(TestData.Data().Classes.Count, c.Classes.Count);
            foreach (string id in TestData.Data().Classes.Keys) Assert.IsNotNull(c.ClassLook(id), id);
        }

        [Test]
        public void ClassSilhouettes_AreDistinct_NotOnlyColor()
        {
            List<string> problems;
            PresentationCatalog c = Load(out problems);
            var groups = c.Classes.Values
                .GroupBy(l => string.Join("|", l.Build, l.Height, l.Robe, l.Cape, l.Shoulders, l.Headgear, l.Back, l.OffHand))
                .Where(g => g.Count() > 1)
                .Select(g => string.Join(",", g.Select(x => x.Id)))
                .ToList();
            Assert.IsEmpty(groups, "classes com a mesma silhueta (só mudaria a cor): " + string.Join(" ; ", groups));
        }

        [Test]
        public void EveryEquippableItem_HasAnExplicitLook()
        {
            List<string> problems;
            PresentationCatalog c = Load(out problems);
            var missing = TestData.Data().Items.Values.Where(i => !string.IsNullOrEmpty(i.Slot) && i.Slot != "accessory" && !c.HasCatalogEntry(i.Id)).Select(i => i.Id).ToList();
            Assert.IsEmpty(missing, "itens equipáveis sem aparência: " + string.Join(", ", missing));
        }

        [Test]
        public void ItemWithoutEntry_GetsADerivedDefaultLook()
        {
            List<string> problems;
            PresentationCatalog c = Load(out problems);
            ItemDef sword = TestData.Data().Items["espada_enferrujada"];
            var copy = new ItemDef { Id = "item_novo", Slot = "weapon", WeaponType = "bow" };
            ItemLookDef look = c.ItemLook(copy);
            Assert.AreEqual("bow", look.Shape);
            Assert.AreEqual("hand_l", look.Socket);
            Assert.AreEqual("sword", c.ItemLook(sword).Shape);
        }

        [Test]
        public void Parse_ReportsBadData_WithoutThrowing()
        {
            List<string> problems;
            Load(out problems, "{ nao e json");
            Assert.IsNotEmpty(problems);

            string bad = "{\"schemaVersion\":1,\"classes\":[{\"id\":\"guerreiro\",\"cloth\":\"azul\",\"accent\":\"#FFFFFF\",\"build\":\"gigante\"},{\"id\":\"fantasma\",\"cloth\":\"#000000\",\"accent\":\"#000000\"}],\"items\":[{\"id\":\"espada_enferrujada\",\"shape\":\"laser\",\"color\":\"#FFFFFF\",\"socket\":\"hand_r\"}]}";
            Load(out problems, bad);
            string all = string.Join("\n", problems);
            StringAssert.Contains("cloth inválido", all);
            StringAssert.Contains("build inválido", all);
            StringAssert.Contains("não existe em classes.json", all);
            StringAssert.Contains("shape de arma inválido", all);
            StringAssert.Contains("classe sem aparência", all);
        }
    }
}
