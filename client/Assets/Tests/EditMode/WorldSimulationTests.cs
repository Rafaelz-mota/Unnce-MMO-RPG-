using System;
using System.Linq;
using NUnit.Framework;
using Mmorpg.Core.Characters;
using Mmorpg.Core.Combat;
using Mmorpg.Core.Common;
using Mmorpg.Core.Data;
using Mmorpg.Core.World;

namespace Mmorpg.Core.Tests
{
    /// <summary>A região em execução: nascimento, reaparecimento (incluindo a regra de 60 min do chefe) e ressurreição.</summary>
    [TestFixture]
    public class WorldSimulationTests
    {
        private const string Region = "vale_do_despertar";

        private static WorldSimulation NewSim(int seed = 7)
        {
            return new WorldSimulation(TestData.Fresh(), Region, new SystemRng(seed));
        }

        private static Combatant AddPlayer(WorldSimulation sim, string classId, int level)
        {
            PlayerCharacter pc = PlayerCharacter.CreateNew(sim.Data, "Heroi", classId);
            TestWorld.SetLevel(pc, level);
            Combatant c = sim.AddPlayer(pc);
            sim.World.RefreshPlayerStats(c, true);
            return c;
        }

        [Test]
        public void Constructor_RejectsNullDataUnknownAndUnimplementedRegions()
        {
            GameData d = TestData.Fresh();
            Assert.Throws<ArgumentNullException>(() => new WorldSimulation(null, Region, new SystemRng(1)));
            Assert.Throws<ArgumentException>(() => new WorldSimulation(d, "regiao_que_nao_existe", new SystemRng(1)));
            Assert.Throws<ArgumentException>(() => new WorldSimulation(d, null, new SystemRng(1)));
            string pending = d.Regions.Values.First(r => !r.Implemented).Id;
            Assert.Throws<ArgumentException>(() => new WorldSimulation(d, pending, new SystemRng(1)));
        }

        [Test]
        public void StartsWithEveryConfiguredMonster_AndNoneSpawnsInsideAnObstacle()
        {
            WorldSimulation sim = NewSim();
            foreach (SpawnZoneDef z in sim.Region.SpawnZones)
            {
                Assert.AreEqual(z.Count, sim.AliveInZone(z.Id), z.Id);
                Assert.AreEqual(0, sim.PendingRespawns(z.Id), z.Id);
            }
            foreach (Combatant c in sim.World.All.Where(x => x.IsMonster))
                Assert.IsFalse(sim.Map.IsBlocked(c.Position, sim.World.BodyRadius), c.Name + " nasceu dentro de um obstáculo ou fora do mapa");
        }

        [Test]
        public void PlayerSpawn_IsInsideTheMapAndFarFromAggressiveMonsters()
        {
            WorldSimulation sim = NewSim();
            Assert.IsFalse(sim.Map.IsBlocked(sim.Map.PlayerSpawn, sim.World.BodyRadius));
            foreach (Combatant m in sim.World.All.Where(x => x.IsMonster))
                Assert.Greater(Vec2.Distance(sim.Map.PlayerSpawn, m.Position), m.MonsterDef.AggroRange + 5, m.Name + " nasce perto demais do ponto inicial");
        }

        [Test]
        public void KilledMonster_ReturnsAfterTheZoneRespawnTime_CountedFromDeath()
        {
            WorldSimulation sim = NewSim();
            SpawnZoneDef zone = sim.Region.SpawnZones.First(z => z.Id == "zona_brotos");
            Combatant killer = AddPlayer(sim, "guerreiro", 7);
            Combatant victim = sim.World.All.First(c => c.SpawnZoneId == zone.Id);

            sim.World.Kill(victim, killer.Id);
            Assert.AreEqual(zone.Count - 1, sim.AliveInZone(zone.Id));
            Assert.AreEqual(1, sim.PendingRespawns(zone.Id));

            sim.Run(zone.RespawnSeconds - 2.0);
            Assert.AreEqual(zone.Count - 1, sim.AliveInZone(zone.Id), "ainda não deveria ter voltado");
            Assert.AreEqual(1, sim.PendingRespawns(zone.Id));

            sim.Run(4.0);
            Assert.AreEqual(zone.Count, sim.AliveInZone(zone.Id));
            Assert.AreEqual(0, sim.PendingRespawns(zone.Id));
        }

        [Test]
        public void RegionalBoss_RespawnsOneHourAfterDeath_AsTheProjectRuleRequires()
        {
            WorldSimulation sim = NewSim();
            Assert.AreEqual(3600.0, sim.Data.Rules.RegionalBossRespawnSeconds, 1e-9);

            SpawnZoneDef zone = sim.Region.SpawnZones.First(z => sim.Data.Monsters[z.MonsterId].CategoryId == sim.Data.Rules.BossCategoryId);
            Assert.AreEqual(1, zone.Count);
            Assert.AreEqual(sim.Data.Rules.RegionalBossRespawnSeconds, zone.RespawnSeconds, 1e-9);

            Combatant killer = AddPlayer(sim, "guerreiro", 7);
            Combatant boss = sim.World.All.First(c => c.SpawnZoneId == zone.Id);
            sim.World.Kill(boss, killer.Id);
            Assert.AreEqual(0, sim.AliveInZone(zone.Id));

            sim.Run(3590.0, 0.25);
            Assert.AreEqual(0, sim.AliveInZone(zone.Id), "o chefe não pode voltar antes de 60 minutos");
            Assert.AreEqual(1, sim.PendingRespawns(zone.Id));

            sim.Run(20.0, 0.25);
            Assert.AreEqual(1, sim.AliveInZone(zone.Id), "o chefe deve voltar ~60 minutos após a morte");
        }

        [Test]
        public void DeadPlayer_IsRevivedAtTheSpawnPointAfterTheConfiguredDelay()
        {
            WorldSimulation sim = NewSim();
            Combatant p = AddPlayer(sim, "mago", 1);
            Combatant boss = sim.World.All.First(c => c.MonsterDef.Id == "colosso_da_raiz");
            p.Position = boss.Position + new Vec2(2, 0);
            p.Hp = 1;

            for (int i = 0; i < 300 && !p.Dead; i++) sim.Run(0.1);
            Assert.IsTrue(p.Dead, "o chefe deveria derrotar um mago nível 1 com 1 de vida");

            sim.Run(sim.Data.Combat.PlayerRespawnSeconds + 1.0);
            Assert.IsFalse(p.Dead);
            Assert.Greater(p.Hp, 0);
            Assert.AreEqual(sim.Map.PlayerSpawn.X, p.Position.X, 1e-9);
            Assert.AreEqual(sim.Map.PlayerSpawn.Z, p.Position.Z, 1e-9);
        }

        [Test]
        public void RemovingAPlayerWhileDead_CancelsTheRevive_WithoutErrors()
        {
            WorldSimulation sim = NewSim();
            Combatant p = AddPlayer(sim, "guerreiro", 1);
            sim.World.Kill(p, 0);
            Assert.IsTrue(p.Dead);

            sim.RemovePlayer(p);
            sim.Run(sim.Data.Combat.PlayerRespawnSeconds + 2.0);
            Assert.IsNull(sim.World.Get(p.Id));
        }
    }
}
