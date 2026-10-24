using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Mmorpg.Core.Characters;
using Mmorpg.Core.Data;

namespace Mmorpg.Core.Tests
{
    [TestFixture]
    public class CharacterTests
    {
        private GameData D { get { return TestData.Data(); } }

        [Test]
        public void CreateNew_ValidatesNameAndClass()
        {
            Assert.Throws<System.ArgumentException>(() => PlayerCharacter.CreateNew(D, "", "guerreiro"));
            Assert.Throws<System.ArgumentException>(() => PlayerCharacter.CreateNew(D, new string('x', 25), "guerreiro"));
            Assert.Throws<System.ArgumentException>(() => PlayerCharacter.CreateNew(D, "Heroi", "classe_inexistente"));
        }

        [Test]
        public void CreateNew_StartsWithFreeSkillsUnlockedByLevelOnly()
        {
            PlayerCharacter pc = PlayerCharacter.CreateNew(D, "Heroi", "guerreiro");
            Assert.IsTrue(pc.IsSkillLearned("guerreiro_golpe_poderoso"));
            Assert.IsFalse(pc.IsSkillLearned("guerreiro_investida"), "exige nível 2");
            Assert.IsFalse(pc.IsSkillLearned("guerreiro_muralha_de_aco"), "habilidade paga");
            TestWorld.SetLevel(pc, 7);
            Assert.IsTrue(pc.IsSkillLearned("guerreiro_investida"));
            Assert.IsTrue(pc.IsSkillLearned("guerreiro_impacto_atordoante"));
            Assert.IsFalse(pc.IsSkillLearned("guerreiro_muralha_de_aco"), "paga nunca é automática");
        }

        [Test]
        public void AddXp_LevelsUp_CarriesOverflow_AndStopsAtMaxLevel()
        {
            PlayerCharacter pc = PlayerCharacter.CreateNew(D, "Heroi", "mago");
            long need = pc.XpToNext;
            Assert.AreEqual(0, pc.AddXp(need - 1));
            Assert.AreEqual(1, pc.Level);
            Assert.AreEqual(1, pc.AddXp(1));
            Assert.AreEqual(2, pc.Level);
            Assert.AreEqual(0L, pc.Xp);

            long huge = 1000000000L;
            int gained = pc.AddXp(huge);
            Assert.Greater(gained, 1, "vários níveis de uma vez");
            Assert.AreEqual(55, pc.Level);
            Assert.AreEqual(0L, pc.Xp);
            Assert.IsTrue(pc.IsMaxLevel);
            Assert.AreEqual(0, pc.AddXp(100), "no nível máximo XP não acumula");
            Assert.AreEqual(55, pc.Level);
        }

        [Test]
        public void AddXp_IgnoresNonPositiveAmounts()
        {
            PlayerCharacter pc = PlayerCharacter.CreateNew(D, "Heroi", "mago");
            Assert.AreEqual(0, pc.AddXp(0));
            Assert.AreEqual(0, pc.AddXp(-500));
            Assert.AreEqual(0L, pc.Xp);
        }

        [Test]
        public void Gold_IsCappedAndCannotGoNegative()
        {
            PlayerCharacter pc = PlayerCharacter.CreateNew(D, "Heroi", "mago");
            Assert.AreEqual(0L, pc.AddGold(-5));
            Assert.AreEqual(100L, pc.AddGold(100));
            Assert.IsFalse(pc.TrySpendGold(101));
            Assert.AreEqual(100L, pc.Gold);
            Assert.IsFalse(pc.TrySpendGold(-1), "gasto negativo não pode criar ouro");
            Assert.IsTrue(pc.TrySpendGold(40));
            Assert.AreEqual(60L, pc.Gold);
            pc.AddGold(long.MaxValue / 2);
            Assert.AreEqual(D.Rules.MaxGold, pc.Gold);
        }

        [Test]
        public void LearnPaidSkill_ChargesExactCost_ChecksLevelAndGold()
        {
            PlayerCharacter pc = PlayerCharacter.CreateNew(D, "Heroi", "guerreiro");
            Assert.AreEqual(LearnSkillResult.NotClassSkill, pc.TryLearnSkill("mago_raio_arcano"));
            Assert.AreEqual(LearnSkillResult.UnknownSkill, pc.TryLearnSkill("nao_existe"));
            Assert.AreEqual(LearnSkillResult.FreeSkill, pc.TryLearnSkill("guerreiro_investida"));
            Assert.AreEqual(LearnSkillResult.LevelTooLow, pc.TryLearnSkill("guerreiro_muralha_de_aco"));
            TestWorld.SetLevel(pc, 10);
            Assert.AreEqual(LearnSkillResult.NotEnoughGold, pc.TryLearnSkill("guerreiro_muralha_de_aco"));
            pc.AddGold(10001);
            Assert.AreEqual(LearnSkillResult.Ok, pc.TryLearnSkill("guerreiro_muralha_de_aco"));
            Assert.AreEqual(1L, pc.Gold, "custou exatamente 10.000");
            Assert.AreEqual(LearnSkillResult.AlreadyLearned, pc.TryLearnSkill("guerreiro_muralha_de_aco"));
        }

        [Test]
        public void Save_RoundTrips_ThroughJson()
        {
            PlayerCharacter pc = PlayerCharacter.CreateNew(D, "Aldric", "guerreiro");
            TestWorld.SetLevel(pc, 10);
            pc.AddXp(17);
            pc.AddGold(12345);
            pc.Inventory.Add("pele_de_lobo", 7);
            pc.Inventory.Add("capuz_de_couro", 1);
            pc.Equip(Enumerable.Range(0, pc.Inventory.Capacity).First(i => pc.Inventory.Get(i) != null && pc.Inventory.Get(i).ItemId == "capuz_de_couro"));
            pc.TryLearnSkill("guerreiro_muralha_de_aco");

            string json = CharacterSaveJson.ToJson(pc.ToSave());
            PlayerCharacter back = PlayerCharacter.Restore(D, CharacterSaveJson.FromJson(json));

            Assert.AreEqual(pc.Name, back.Name);
            Assert.AreEqual(pc.Class.Id, back.Class.Id);
            Assert.AreEqual(pc.Level, back.Level);
            Assert.AreEqual(pc.Xp, back.Xp);
            Assert.AreEqual(pc.Gold, back.Gold);
            Assert.AreEqual(7, back.Inventory.CountOf("pele_de_lobo"));
            Assert.AreEqual("capuz_de_couro", back.EquippedItemId("head"));
            Assert.AreEqual("espada_enferrujada", back.EquippedItemId("weapon"));
            CollectionAssert.AreEquivalent(pc.LearnedSkills.ToList(), back.LearnedSkills.ToList());
            Assert.AreEqual(pc.ComputeStats().Get("maxHp"), back.ComputeStats().Get("maxHp"), 1e-9);
        }

        private CharacterSave Valid()
        {
            PlayerCharacter pc = PlayerCharacter.CreateNew(D, "Aldric", "guerreiro");
            TestWorld.SetLevel(pc, 10);
            return pc.ToSave();
        }

        private void AssertRejected(CharacterSave save, string because)
        {
            var ex = Assert.Throws<SaveValidationException>(() => PlayerCharacter.Restore(D, save), because);
            Assert.Greater(ex.Problems.Count, 0, because);
        }

        [Test]
        public void TamperedSaves_AreRejected()
        {
            CharacterSave s;
            s = Valid(); s.Level = 99; AssertRejected(s, "nível acima do máximo");
            s = Valid(); s.Level = 0; AssertRejected(s, "nível zero");
            s = Valid(); s.Gold = -1; AssertRejected(s, "ouro negativo");
            s = Valid(); s.Gold = D.Rules.MaxGold + 1; AssertRejected(s, "ouro acima do teto");
            s = Valid(); s.ClassId = "classe_falsa"; AssertRejected(s, "classe inexistente");
            s = Valid(); s.Name = ""; AssertRejected(s, "nome vazio");
            s = Valid(); s.SchemaVersion = 99; AssertRejected(s, "schema desconhecido");
            s = Valid(); s.Xp = 999999999; AssertRejected(s, "xp incompatível com o nível");
            s = Valid(); s.Inventory.Add(new ItemStackSave { Slot = 5, ItemId = "item_falso", Count = 1 }); AssertRejected(s, "item inexistente");
            s = Valid(); s.Inventory.Add(new ItemStackSave { Slot = 5, ItemId = "pele_de_lobo", Count = 100000 }); AssertRejected(s, "pilha acima do máximo");
            s = Valid(); s.Inventory.Add(new ItemStackSave { Slot = 500, ItemId = "pele_de_lobo", Count = 1 }); AssertRejected(s, "slot fora do inventário");
            s = Valid(); s.Equipment["head"] = "espada_enferrujada"; AssertRejected(s, "item no slot errado");
            s = Valid(); s.Equipment["weapon"] = "cajado_do_colosso"; AssertRejected(s, "item que a classe não pode equipar");
            s = Valid(); s.LearnedSkills.Add("mago_raio_arcano"); AssertRejected(s, "habilidade de outra classe");
        }

        [Test]
        public void TamperedSave_WithSkillAboveLevel_IsRejected()
        {
            PlayerCharacter pc = PlayerCharacter.CreateNew(D, "Aldric", "guerreiro");
            CharacterSave s = pc.ToSave();
            s.LearnedSkills.Add("guerreiro_muralha_de_aco"); // exige nível 10
            AssertRejected(s, "habilidade acima do nível");
        }

        [Test]
        public void CorruptJsonFile_IsReportedAsSaveError()
        {
            Assert.Throws<SaveValidationException>(() => CharacterSaveJson.FromJson("{ isto nao e json"));
            Assert.Throws<SaveValidationException>(() => CharacterSaveJson.FromJson("{\"name\":\"x\",\"campoInventado\":1}"));
        }

        [Test]
        public void FileStore_SavesAtomically_AndCanReloadAfterOverwrite()
        {
            string dir = Path.Combine(Path.GetTempPath(), "mmorpg-test-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var store = new JsonFileCharacterStore(dir);
                PlayerCharacter pc = PlayerCharacter.CreateNew(D, "Ana/../x", "mago");
                Assert.IsFalse(store.Exists(pc.Name));
                store.Save(pc.ToSave());
                Assert.IsTrue(store.Exists(pc.Name));
                pc.AddGold(50);
                store.Save(pc.ToSave());
                CharacterSave loaded = store.Load(pc.Name);
                Assert.AreEqual(50L, loaded.Gold);
                Assert.AreEqual(0, Directory.GetFiles(dir, "*.tmp").Length, "não pode sobrar arquivo temporário");
                Assert.AreEqual(1, Directory.GetFiles(dir).Length);
                foreach (string f in Directory.GetFiles(dir)) Assert.IsTrue(Path.GetDirectoryName(f) == dir, "nome de personagem não pode escapar da pasta");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
