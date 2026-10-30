using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Mmorpg.Core.Data;

namespace Mmorpg.Core.Tests
{
    /// <summary>Fonte de dados em memória que parte dos JSON reais e permite corromper arquivos de propósito.</summary>
    internal sealed class PatchedSource : IDataSource
    {
        private readonly IDataSource _inner = TestData.Source();
        private readonly Dictionary<string, Func<string, string>> _patches = new Dictionary<string, Func<string, string>>();
        private readonly HashSet<string> _missing = new HashSet<string>();

        public PatchedSource Patch(string file, Func<string, string> edit) { _patches[file] = edit; return this; }
        public PatchedSource Replace(string file, string from, string to)
        {
            return Patch(file, text =>
            {
                if (text.IndexOf(from, StringComparison.Ordinal) < 0) throw new InvalidOperationException("trecho não encontrado em " + file + ": " + from);
                return text.Replace(from, to);
            });
        }
        public PatchedSource Remove(string file) { _missing.Add(file); return this; }

        public string ReadAllText(string name)
        {
            if (_missing.Contains(name)) throw new System.IO.FileNotFoundException(name + ".json");
            string text = _inner.ReadAllText(name);
            Func<string, string> p;
            return _patches.TryGetValue(name, out p) ? p(text) : text;
        }
    }

    [TestFixture]
    public class DataValidationTests
    {
        private static string ErrorsOf(LoadResult r)
        {
            return string.Join("\n", r.Report.Issues.Where(i => i.Severity == Severity.Error).Select(i => i.ToString()));
        }

        [Test]
        public void RealData_IsValid_AndHasNoErrors()
        {
            LoadResult r = GameDataLoader.Load(TestData.Source());
            Assert.IsTrue(r.Success, r.Report.ToText());
            Assert.AreEqual(0, r.Report.ErrorCount);
        }

        [Test]
        public void MissingFile_IsReported_NotThrown()
        {
            LoadResult r = GameDataLoader.Load(new PatchedSource().Remove("monsters"));
            Assert.IsFalse(r.Success);
            Assert.IsNull(r.Data);
            Assert.IsTrue(ErrorsOf(r).Contains("monsters.json"), ErrorsOf(r));
        }

        [Test]
        public void MalformedJson_IsReported_NotThrown()
        {
            LoadResult r = GameDataLoader.Load(new PatchedSource().Patch("items", t => t.Substring(0, t.Length / 2)));
            Assert.IsFalse(r.Success);
            Assert.IsTrue(ErrorsOf(r).Contains("items.json"), ErrorsOf(r));
        }

        [Test]
        public void TypoInBalanceKey_IsRejected()
        {
            // "cooldown" digitado errado não pode virar um valor 0 silencioso.
            LoadResult r = GameDataLoader.Load(new PatchedSource().Replace("skills", "\"cooldown\": 12,", "\"cooldwon\": 12,"));
            Assert.IsFalse(r.Success);
            Assert.IsTrue(ErrorsOf(r).Contains("skills.json"), ErrorsOf(r));
        }

        [Test]
        public void UnsupportedSchemaVersion_IsRejected()
        {
            LoadResult r = GameDataLoader.Load(new PatchedSource().Replace("rarities", "\"schemaVersion\": 1", "\"schemaVersion\": 2"));
            Assert.IsFalse(r.Success);
            Assert.IsTrue(ErrorsOf(r).Contains("schemaVersion"), ErrorsOf(r));
        }

        [Test]
        public void ChangingPaidSkillCost_IsRejectedByProjectInvariant()
        {
            // O prompt fixa 10.000 / 20.000 / 30.000 / 40.000. Alterar só o custo de uma habilidade viola isso.
            LoadResult r = GameDataLoader.Load(new PatchedSource().Replace("skills", "\"goldCost\": 10000", "\"goldCost\": 9999"));
            Assert.IsFalse(r.Success);
            Assert.IsTrue(ErrorsOf(r).Contains("goldCost"), ErrorsOf(r));
        }

        [Test]
        public void ChangingBossRespawn_IsRejected()
        {
            LoadResult r = GameDataLoader.Load(new PatchedSource().Replace("regions", "\"respawnSeconds\": 3600", "\"respawnSeconds\": 600"));
            Assert.IsFalse(r.Success);
            Assert.IsTrue(ErrorsOf(r).Contains("3600"), ErrorsOf(r));
        }

        [Test]
        public void LootTableReferencingUnknownItem_IsRejected()
        {
            LoadResult r = GameDataLoader.Load(new PatchedSource().Replace("loot_tables", "\"pele_de_lobo\"", "\"item_que_nao_existe\""));
            Assert.IsFalse(r.Success);
            Assert.IsTrue(ErrorsOf(r).Contains("item_que_nao_existe"), ErrorsOf(r));
        }

        [Test]
        public void DuplicateId_IsRejected()
        {
            LoadResult r = GameDataLoader.Load(new PatchedSource().Replace("rarities", "\"id\": \"rare\"", "\"id\": \"common\""));
            Assert.IsFalse(r.Success);
            Assert.IsTrue(ErrorsOf(r).Contains("duplicado"), ErrorsOf(r));
        }

        [Test]
        public void LoadOrThrow_ThrowsWithReport_OnInvalidData()
        {
            var ex = Assert.Throws<GameDataException>(() => GameDataLoader.LoadOrThrow(new PatchedSource().Remove("rules")));
            Assert.IsTrue(ex.Report.HasErrors);
        }

        [Test]
        public void ProjectInvariants_Hold()
        {
            GameData d = TestData.Data();
            Assert.AreEqual(24, d.Classes.Count);
            Assert.AreEqual(12, d.Classes.Values.Count(c => c.Faction == "concordia"));
            Assert.AreEqual(12, d.Classes.Values.Count(c => c.Faction == "legiao"));
            Assert.AreEqual(5, d.Rarities.Count);
            Assert.AreEqual(8, d.Regions.Count);
            Assert.AreEqual(1, d.Regions.Values.Count(r => r.Implemented), "só o Vale do Despertar está implementado");
            // As 7 categorias de monstro do prompt mais o chefe regional.
            foreach (string id in new[] { "common", "uncommon", "rare", "elite", "champion", "legendary", "unique", "regionalBoss" })
                Assert.IsTrue(d.MonsterCategories.ContainsKey(id), id);
        }

        [Test]
        public void EveryClassSkill_FollowsSlotRules()
        {
            GameData d = TestData.Data();
            foreach (SkillDef s in d.Skills.Values.Where(x => x.OwnerType == SkillOwner.Class))
            {
                Assert.IsTrue(s.Slot >= 1 && s.Slot <= 9, s.Id);
                long expected = s.Slot <= 5 ? 0 : d.Rules.PaidSkillGoldCost[s.Slot.ToString()];
                Assert.AreEqual(expected, s.GoldCost, s.Id);
            }
        }
    }
}
