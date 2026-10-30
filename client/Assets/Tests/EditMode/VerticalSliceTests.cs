using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Mmorpg.Core.Characters;
using Mmorpg.Core.Combat;
using Mmorpg.Core.Common;
using Mmorpg.Core.Data;
using Mmorpg.Core.Events;
using Mmorpg.Core.Items;
using Mmorpg.Core.Session;

namespace Mmorpg.Core.Tests
{
    /// <summary>
    /// Testes de ponta a ponta da vertical slice: usam só a <see cref="GameSession"/> (a mesma porta de entrada da
    /// camada Unity) para jogar, evoluir, saquear, derrotar o chefe e salvar. Não dependem de Unity.
    /// </summary>
    [TestFixture]
    public class VerticalSliceTests
    {
        private const string Region = "vale_do_despertar";

        /// <summary>Armazenamento em memória que passa pelo mesmo JSON do arquivo real.</summary>
        private sealed class MemoryStore : ICharacterStore
        {
            public readonly Dictionary<string, string> Files = new Dictionary<string, string>();
            public int SaveCount;
            public bool Exists(string name) { return Files.ContainsKey(name); }
            public void Save(CharacterSave save) { SaveCount++; Files[save.Name] = CharacterSaveJson.ToJson(save); }
            public CharacterSave Load(string name) { return CharacterSaveJson.FromJson(Files[name]); }
        }

        private sealed class FailingStore : ICharacterStore
        {
            private readonly Exception _error;
            public FailingStore(Exception error) { _error = error; }
            public bool Exists(string name) { return true; }
            public void Save(CharacterSave save) { throw _error; }
            public CharacterSave Load(string name) { throw _error; }
        }

        private static GameSession NewSession(string classId, int level, ICharacterStore store = null, int seed = 11)
        {
            GameData data = TestData.Fresh();
            PlayerCharacter pc = PlayerCharacter.CreateNew(data, "Heroi", classId);
            TestWorld.SetLevel(pc, level);
            GameSession s = GameSession.Start(data, pc, Region, new SystemRng(seed), store);
            s.Sim.World.RefreshPlayerStats(s.Player, true);
            return s;
        }

        private static void Play(GameSession s, HeadlessBot bot, double seconds, Func<bool> stopWhen = null)
        {
            double t = 0;
            while (t < seconds)
            {
                bot.Think();
                for (int i = 0; i < 2; i++) { s.Tick(0.05); t += 0.05; }
                if (stopWhen != null && stopWhen()) return;
            }
        }

        // ------------------------------------------------------------------ início da partida
        [Test]
        public void EveryClass_CanStartASession_AndSurvivesTwoSecondsOfSimulation()
        {
            GameData data = TestData.Fresh();
            foreach (ClassDef cls in data.Classes.Values)
            {
                PlayerCharacter pc = PlayerCharacter.CreateNew(data, "Teste", cls.Id);
                GameSession s = GameSession.Start(data, pc, Region, new SystemRng(3), null);
                for (int i = 0; i < 40; i++) s.Tick(0.05);
                Assert.IsFalse(s.Player.Dead, cls.Id);
                Assert.IsTrue(s.Player.Hp > 0, cls.Id);
                Assert.AreEqual(data.Rules.SkillsPerClass, s.Hotbar().Count, cls.Id);
            }
        }

        [Test]
        public void EveryClass_HasNineDistinctSkills_WithFiveFreeAndFourPaid()
        {
            GameData data = TestData.Fresh();
            var allNames = new System.Collections.Generic.HashSet<string>();
            foreach (ClassDef cls in data.Classes.Values)
            {
                var sk = new System.Collections.Generic.List<SkillDef>();
                foreach (string id in cls.SkillIds) sk.Add(data.Skills[id]);
                Assert.AreEqual(9, sk.Count, cls.Id);
                var elements = new System.Collections.Generic.HashSet<string>();
                var shapes = new System.Collections.Generic.HashSet<string>();
                foreach (SkillDef x in sk)
                {
                    Assert.IsTrue(allNames.Add(x.Name), "nome repetido: " + x.Name);
                    Assert.AreEqual(x.Slot <= 5, x.GoldCost == 0, x.Id);
                    elements.Add(x.Element);
                    shapes.Add(x.Targeting + "/" + x.Effects.Count + "/" + x.Type);
                }
                Assert.GreaterOrEqual(shapes.Count, 4, cls.Id + " tem habilidades parecidas demais");
            }
        }

        [Test]
        public void Start_RejectsNullArguments()
        {
            GameData data = TestData.Fresh();
            PlayerCharacter pc = PlayerCharacter.CreateNew(data, "X", "guerreiro");
            Assert.Throws<ArgumentNullException>(() => GameSession.Start(null, pc, Region, null, null));
            Assert.Throws<ArgumentNullException>(() => GameSession.Start(data, null, Region, null, null));
            Assert.Throws<ArgumentException>(() => GameSession.Start(data, pc, "regiao_inexistente", null, null));
        }

        // ------------------------------------------------------------------ comandos
        [Test]
        public void UseHotbarSlot_OutOfRangeOrEmpty_IsRefusedWithAMessage()
        {
            GameSession s = NewSession("guardiao", 1); // classe ainda sem habilidades nos dados
            Assert.AreEqual(UseSkillResult.UnknownSkill, s.UseHotbarSlot(0));
            Assert.AreEqual(UseSkillResult.UnknownSkill, s.UseHotbarSlot(10));
            Assert.AreEqual(UseSkillResult.UnknownSkill, s.UseHotbarSlot(1));
            Assert.IsTrue(s.Log.Any(l => l.Contains("ainda não existe")), "deveria avisar que a habilidade não existe");
        }

        [Test]
        public void UseHotbarSlot_WithoutTarget_ReportsNoTarget_AndLockedSkillIsNotLearned()
        {
            GameSession s = NewSession("guerreiro", 1);
            // Nenhum monstro perto do ponto de nascimento: não há o que atacar.
            Assert.AreEqual(UseSkillResult.NoTarget, s.UseHotbarSlot(1));
            Assert.AreEqual(UseSkillResult.NoTarget, s.UseHotbarSlot(5), "slot 5 é gratuito e já está liberado");
            Assert.AreEqual(UseSkillResult.NotLearned, s.UseHotbarSlot(6), "slot 6 é pago e está bloqueado");
        }

        [Test]
        public void DevUnlockAll_IsOffByDefault_AndWhenOnGivesMaxLevelAndAllClassSkills()
        {
            GameSession s = NewSession("mago", 1);
            Assert.IsFalse(s.DevUnlockAll(), "desligado por padrão");
            Assert.AreEqual(1, s.Character.Level);
            Assert.IsFalse(s.Character.IsSkillLearned("mago_chuva_de_meteoros"));

            s.DevToolsEnabled = true;
            Assert.IsTrue(s.DevUnlockAll());
            Assert.AreEqual(s.Data.Progression.MaxLevel, s.Character.Level);
            Assert.AreEqual(s.Data.Progression.MaxLevel, s.Player.Level, "o combatente também sobe de nível");
            foreach (SkillDef k in s.Character.ClassSkills())
                Assert.IsTrue(s.Character.IsSkillLearned(k.Id), k.Id);
            Assert.GreaterOrEqual(s.Character.Gold, 100000);
        }

        [Test]
        public void Tick_ClampsHugeSteps_AndIgnoresInvalidOnes()
        {
            GameSession s = NewSession("guerreiro", 1);
            double t0 = s.Sim.World.Time;
            s.Tick(double.NaN);
            s.Tick(-1);
            s.Tick(0);
            s.Tick(double.PositiveInfinity);
            Assert.AreEqual(t0, s.Sim.World.Time, 1e-9);
            s.Tick(1000.0);
            Assert.AreEqual(t0 + GameSession.MaxTickSeconds, s.Sim.World.Time, 1e-9);
        }

        [Test]
        public void SetMove_MovesThePlayer_AndLongVectorsCannotSpeedHack()
        {
            GameSession s = NewSession("guerreiro", 1);
            Vec2 start = s.Player.Position;
            s.SetMove(new Vec2(0, 1000)); // vetor absurdo: o mundo limita a 1
            for (int i = 0; i < 20; i++) s.Tick(0.05);
            double moved = Vec2.Distance(start, s.Player.Position);
            double maxPossible = s.Player.Stats.Get("moveSpeed") * 1.0 + 1e-6;
            Assert.IsTrue(moved > 0.5, "deveria ter andado");
            Assert.IsTrue(moved <= maxPossible, "andou " + moved + " em 1 s, máximo " + maxPossible);
        }

        [Test]
        public void CycleTarget_SelectsNearbyMonsters_AndClearingWorks()
        {
            GameSession s = NewSession("guerreiro", 1);
            s.TabTargetRange = 3.0;
            Assert.AreEqual(0, s.CycleTarget(), "nada a 3 m do ponto de nascimento");
            s.TabTargetRange = 30.0;
            Combatant brotos = s.Sim.World.All.First(c => c.IsMonster && c.MonsterDef.Id == "broto_ancestral");
            s.Player.Position = brotos.Position + new Vec2(2, 0);
            int id = s.CycleTarget();
            Assert.AreNotEqual(0, id);
            Assert.AreEqual(id, s.SelectedTargetId);
            s.ClearTarget();
            Assert.AreEqual(0, s.SelectedTargetId);
            s.SelectTarget(s.Player.Id); // alvo inválido (o próprio jogador)
            Assert.AreEqual(0, s.SelectedTargetId);
        }

        // ------------------------------------------------------------------ jogar de verdade
        [Test]
        public void PlayingTheSlice_GivesXpGoldAndLoot_AndTheSaveRestoresExactly()
        {
            var store = new MemoryStore();
            GameSession s = NewSession("guerreiro", 1, store, 21);
            var bot = new HeadlessBot(s.Sim, s.Player);

            // O log narra o primeiro abate (checado logo que acontece: o log guarda só as últimas linhas).
            Play(s, bot, 300.0, () => s.Log.Any(l => l.Contains("XP")));
            Assert.IsTrue(s.Log.Any(l => l.Contains("XP")),
                "o log deveria mostrar o XP do primeiro abate; final do log: " + string.Join(" | ", s.Log.Skip(Math.Max(0, s.Log.Count - 8))));
            Assert.IsTrue(s.Log.Any(l => l.Contains("Você derrotou")), "o log deveria narrar o abate");

            Play(s, bot, 600.0);

            var audit = s.Sim.Rewards.Audit;
            int kills = audit.Count(a => a.Kind == "xp");
            Assert.IsTrue(kills >= 3, "o bot deveria ter derrotado pelo menos 3 monstros em 15 min, derrotou " + kills);
            Assert.IsTrue(s.Character.Level >= 2 || s.Character.Xp > 0, "deveria ter ganho experiência");
            Assert.IsTrue(s.Character.Gold > 0, "deveria ter ganho ouro");
            Assert.IsTrue(store.SaveCount >= 1 || s.Character.Level == 1, "subir de nível deveria salvar automaticamente");

            Assert.IsTrue(s.Save());
            PlayerCharacter back = PlayerCharacter.Restore(s.Data, CharacterSaveJson.FromJson(store.Files["Heroi"]));
            Assert.AreEqual(s.Character.Level, back.Level);
            Assert.AreEqual(s.Character.Xp, back.Xp);
            Assert.AreEqual(s.Character.Gold, back.Gold);
            foreach (string itemId in s.Data.Items.Keys)
                Assert.AreEqual(s.Character.Inventory.CountOf(itemId), back.Inventory.CountOf(itemId), itemId);
        }

        [Test]
        public void RegionalBoss_FallsToAPreparedPlayer_PaysItsGuaranteedLoot_AndQueuesTheOneHourRespawn()
        {
            GameSession s = NewSession("guerreiro", 7, null, 5);
            Combatant boss = s.Sim.World.All.First(c => c.IsMonster && c.MonsterDef.Id == "colosso_da_raiz");

            // O objetivo é validar o encadeamento (chefe morre -> recompensa -> reaparecimento), não o equilíbrio
            // de um personagem solo: por isso o jogador recebe atributos de teste.
            TestWorld.ForceStats(s.Player, "maxHp", 100000);
            TestWorld.ForceStats(s.Player, "physAttack", 400);
            s.Player.Hp = s.Player.MaxHp;
            s.Player.Position = boss.Position + new Vec2(0, -3);

            var bot = new HeadlessBot(s.Sim, s.Player) { AllowBosses = true, MaxLevelAbove = 5 };
            Play(s, bot, 240.0, () => boss.Dead);

            Assert.IsTrue(boss.Dead, "o Colosso da Raiz deveria ter caído");
            Assert.IsFalse(s.Player.Dead);

            var mine = s.Sim.Rewards.Audit.Where(a => a.MonsterId == "colosso_da_raiz").ToList();
            Assert.IsTrue(mine.Any(a => a.Kind == "xp"), "sem XP do chefe");
            Assert.IsTrue(mine.Any(a => a.Kind == "gold"), "sem ouro do chefe");
            Assert.IsTrue(mine.Any(a => a.Kind == "item" && a.ItemId == "fragmento_de_raiz_ancestral"), "faltou o saque garantido (fragmento)");
            Assert.IsTrue(mine.Any(a => a.Kind == "item" && a.ItemId == "lasca_de_madeira_antiga"), "faltou o saque garantido (lasca)");
            Assert.IsTrue(s.Character.Inventory.CountOf("fragmento_de_raiz_ancestral") >= 1);
            Assert.IsTrue(s.Character.Inventory.CountOf("lasca_de_madeira_antiga") >= 3);
            Assert.IsTrue(s.Log.Any(l => l.Contains("Você derrotou Colosso da Raiz")), "o log deveria narrar a vitória");

            Assert.AreEqual(1, s.Sim.PendingRespawns("arena_colosso"));
            Assert.AreEqual(0, s.Sim.AliveInZone("arena_colosso"));
        }

        [Test]
        public void DeadPlayer_RespawnsAtTheStart_AndTheSessionKeepsRunning()
        {
            GameSession s = NewSession("mago", 1);
            Combatant lobo = s.Sim.World.All.First(c => c.IsMonster && c.MonsterDef.Id == "lobo_selvagem");
            s.Player.Position = lobo.Position + new Vec2(1, 0);
            TestWorld.ForceStats(s.Player, "maxHp", 5);
            s.Player.Hp = 5;
            for (int i = 0; i < 200 && !s.Player.Dead; i++) s.Tick(0.05);
            Assert.IsTrue(s.Player.Dead, "um mago de 5 de vida ao lado de um lobo deveria cair");
            Assert.IsTrue(s.Log.Any(l => l.Contains("derrotado")));
            for (double t = 0; t < s.Data.Combat.PlayerRespawnSeconds + 1.0; t += 0.1) s.Tick(0.1);
            Assert.IsFalse(s.Player.Dead);
            Assert.IsTrue(Vec2.Distance(s.Player.Position, s.Sim.Map.PlayerSpawn) < 1.0);
            Assert.IsTrue(s.Log.Any(l => l.Contains("renasceu")));
        }

        // ------------------------------------------------------------------ economia de habilidades e equipamentos
        [Test]
        public void LearnSkill_ChargesTheExactJsonCost_AndRefusesRepeatsAndPoverty()
        {
            GameSession s = NewSession("guerreiro", 10);
            SkillDef paid = s.Character.ClassSkills().First(x => x.Slot == 6);
            Assert.AreEqual(10000, paid.GoldCost);

            Assert.AreEqual(LearnSkillResult.NotEnoughGold, s.LearnSkill(6));
            s.Character.AddGold(10500);
            Assert.AreEqual(LearnSkillResult.Ok, s.LearnSkill(6));
            Assert.AreEqual(500, s.Character.Gold);
            Assert.IsTrue(s.Character.IsSkillLearned(paid.Id));
            Assert.AreEqual(LearnSkillResult.AlreadyLearned, s.LearnSkill(6));
            Assert.AreEqual(LearnSkillResult.UnknownSkill, s.LearnSkill(9), "o slot 9 do guerreiro ainda não existe nos dados");
            Assert.IsTrue(s.Log.Any(l => l.Contains("aprendeu")));
        }

        [Test]
        public void EquipAndUnequip_KeepTheCombatStatsInSyncWithTheCharacter()
        {
            GameSession s = NewSession("guerreiro", 1);
            string slot = s.Character.Equipped.First().Key;

            Assert.AreEqual(EquipResult.Ok, s.Unequip(slot));
            Assert.IsNull(s.Character.EquippedItemId(slot));
            Assert.AreEqual(s.Character.ComputeStats().Get("maxHp"), s.Player.BaseStats.Get("maxHp"), 1e-9);
            Assert.AreEqual(s.Character.ComputeStats().Get("physAttack"), s.Player.BaseStats.Get("physAttack"), 1e-9);

            int bagSlot = Enumerable.Range(0, s.Character.Inventory.Capacity)
                .First(i => s.Character.Inventory.Get(i) != null);
            Assert.AreEqual(EquipResult.Ok, s.Equip(bagSlot));
            Assert.IsNotNull(s.Character.EquippedItemId(slot));
            Assert.AreEqual(s.Character.ComputeStats().Get("physAttack"), s.Player.BaseStats.Get("physAttack"), 1e-9);

            Assert.AreEqual(EquipResult.EmptySlot, s.Equip(s.Character.Inventory.Capacity - 1));
            Assert.IsTrue(s.Log.Any(l => l.Contains("Nada para equipar")));
        }

        // ------------------------------------------------------------------ erros de persistência
        [Test]
        public void SaveFailures_AreReportedInTheLog_NeverThrown()
        {
            GameSession disk = NewSession("guerreiro", 1, new FailingStore(new IOException("disco cheio")));
            Assert.IsFalse(disk.Save());
            Assert.IsTrue(disk.LastSaveError.Contains("disco cheio"));
            Assert.IsTrue(disk.Log.Any(l => l.Contains("Não foi possível salvar")));

            GameSession perm = NewSession("guerreiro", 1, new FailingStore(new UnauthorizedAccessException("sem permissão")));
            Assert.IsFalse(perm.Save());

            GameSession none = NewSession("guerreiro", 1, null);
            Assert.IsFalse(none.Save(), "sem armazenamento configurado não há o que salvar");
        }

        [Test]
        public void AutosaveAndSaveOnDemand_WriteTheCharacter()
        {
            var store = new MemoryStore();
            GameSession s = NewSession("guerreiro", 1, store);
            s.AutosaveSeconds = 5.0;
            for (int i = 0; i < 120; i++) s.Tick(0.05); // 6 s
            Assert.IsTrue(store.SaveCount >= 1, "o salvamento automático deveria ter disparado");
            s.AutosaveSeconds = 0;
            int before = store.SaveCount;
            for (int i = 0; i < 400; i++) s.Tick(0.05);
            Assert.AreEqual(before, store.SaveCount, "autosave desligado não deve salvar");
        }

        [Test]
        public void TryLoadCharacter_RejectsTamperedAndCorruptSaves_WithoutThrowing()
        {
            GameData data = TestData.Fresh();
            var store = new MemoryStore();
            string error;

            Assert.IsNull(GameSession.TryLoadCharacter(data, store, "Fantasma", out error));
            Assert.IsNull(error, "personagem inexistente não é erro");
            Assert.IsNull(GameSession.TryLoadCharacter(data, null, "X", out error));
            Assert.IsNull(GameSession.TryLoadCharacter(data, store, "  ", out error));

            store.Save(new CharacterSave { Name = "Trapaceiro", ClassId = "guerreiro", Level = 99, Gold = 999999999999L });
            Assert.IsNull(GameSession.TryLoadCharacter(data, store, "Trapaceiro", out error));
            Assert.IsNotNull(error);
            Assert.IsTrue(error.Contains("Save inválido"));

            store.Files["Quebrado"] = "{ isto não é json";
            Assert.IsNull(GameSession.TryLoadCharacter(data, store, "Quebrado", out error));
            Assert.IsNotNull(error);

            PlayerCharacter ok = PlayerCharacter.CreateNew(data, "Legitimo", "mago");
            store.Save(ok.ToSave());
            PlayerCharacter loaded = GameSession.TryLoadCharacter(data, store, "Legitimo", out error);
            Assert.IsNotNull(loaded);
            Assert.IsNull(error);
            Assert.AreEqual("mago", loaded.Class.Id);
        }
    }
}
