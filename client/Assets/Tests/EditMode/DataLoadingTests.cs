using NUnit.Framework;
using Mmorpg.Core.Data;

namespace Mmorpg.Core.Tests
{
    [TestFixture]
    public class DataLoadingTests
    {
        [Test]
        public void RealGameData_LoadsWithoutErrors()
        {
            LoadResult r = GameDataLoader.Load(TestData.Source());
            Assert.IsTrue(r.Success, r.Report.ToText());
        }

        [Test]
        public void RealGameData_HasProjectInvariants()
        {
            GameData d = TestData.Data();
            Assert.AreEqual(24, d.Classes.Count);
            Assert.AreEqual(55, d.Rules.MaxLevel);
            Assert.AreEqual(5, d.Rarities.Count);
            Assert.AreEqual(8, d.Regions.Count);
            Assert.AreEqual(3600.0, d.Rules.RegionalBossRespawnSeconds, 0.0001);
            Assert.AreEqual(10000L, d.Rules.PaidSkillGoldCost["6"]);
            Assert.AreEqual(40000L, d.Rules.PaidSkillGoldCost["9"]);
        }
    }
}
