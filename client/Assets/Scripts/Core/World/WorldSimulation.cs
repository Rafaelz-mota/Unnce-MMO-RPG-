using System;
using System.Collections.Generic;
using Mmorpg.Core.Combat;
using Mmorpg.Core.Characters;
using Mmorpg.Core.Common;
using Mmorpg.Core.Data;
using Mmorpg.Core.Rewards;

namespace Mmorpg.Core.World
{
    /// <summary>
    /// Uma região em execução: mapa, simulação de combate, recompensas, nascimento e reaparecimento de monstros
    /// (spawnZones de regions.json) e ressurreição de jogadores. É o objeto que o cliente Unity (protótipo offline)
    /// atualiza a cada frame e que, no futuro, rodará dentro do servidor.
    /// </summary>
    public sealed class WorldSimulation
    {
        private sealed class ZoneState
        {
            public SpawnZoneDef Def;
            public MonsterDef Monster;
            public readonly List<double> PendingRespawns = new List<double>();
        }

        private readonly IRng _rng;
        private readonly Dictionary<string, ZoneState> _zones = new Dictionary<string, ZoneState>();
        private readonly List<KeyValuePair<Combatant, double>> _pendingRevives = new List<KeyValuePair<Combatant, double>>();

        public GameData Data { get; private set; }
        public RegionDef Region { get; private set; }
        public RegionMap Map { get; private set; }
        public CombatWorld World { get; private set; }
        public RewardService Rewards { get; private set; }

        public WorldSimulation(GameData data, string regionId, IRng rng)
        {
            if (data == null) throw new ArgumentNullException("data");
            RegionDef region;
            if (regionId == null || !data.Regions.TryGetValue(regionId, out region))
                throw new ArgumentException("Região desconhecida: " + regionId);
            if (!region.Implemented)
                throw new ArgumentException("Região ainda não implementada nesta versão: " + regionId);

            Data = data;
            Region = region;
            _rng = rng ?? new SystemRng();
            Map = new RegionMap(region);
            World = new CombatWorld(data, _rng, Map);
            Rewards = new RewardService(World, _rng);

            foreach (SpawnZoneDef z in region.SpawnZones)
            {
                var state = new ZoneState { Def = z, Monster = data.Monsters[z.MonsterId] };
                _zones[z.Id] = state;
                for (int i = 0; i < z.Count; i++) SpawnOne(state);
            }

            World.MonsterKilled += OnMonsterKilled;
            World.PlayerKilled += OnPlayerKilled;
        }

        public Combatant AddPlayer(PlayerCharacter pc)
        {
            return World.AddPlayer(pc, Map.PlayerSpawn);
        }

        public void RemovePlayer(Combatant player)
        {
            _pendingRevives.RemoveAll(kv => kv.Key.Id == player.Id);
            World.Remove(player);
        }

        /// <summary>Quantos monstros vivos há na zona (para testes e diagnóstico).</summary>
        public int AliveInZone(string zoneId)
        {
            int n = 0;
            foreach (Combatant c in World.All) if (c.IsMonster && !c.Dead && c.SpawnZoneId == zoneId) n++;
            return n;
        }

        public int PendingRespawns(string zoneId) { return _zones[zoneId].PendingRespawns.Count; }

        public void Update(double dt)
        {
            World.Update(dt);

            foreach (ZoneState z in _zones.Values)
            {
                for (int i = z.PendingRespawns.Count - 1; i >= 0; i--)
                {
                    if (z.PendingRespawns[i] > World.Time) continue;
                    z.PendingRespawns.RemoveAt(i);
                    SpawnOne(z);
                }
            }

            for (int i = _pendingRevives.Count - 1; i >= 0; i--)
            {
                if (_pendingRevives[i].Value > World.Time) continue;
                Combatant p = _pendingRevives[i].Key;
                _pendingRevives.RemoveAt(i);
                if (World.Get(p.Id) != null && p.Dead) World.Revive(p, Map.PlayerSpawn);
            }
        }

        /// <summary>Avança a simulação em passos fixos (útil em testes e ferramentas sem Unity).</summary>
        public void Run(double seconds, double step = 0.05)
        {
            double left = seconds;
            while (left > 1e-9)
            {
                double dt = Math.Min(step, left);
                Update(dt);
                left -= dt;
            }
        }

        // ------------------------------------------------------------------ interno
        private void OnMonsterKilled(Combatant monster, Combatant killer)
        {
            ZoneState z;
            if (monster.SpawnZoneId != null && _zones.TryGetValue(monster.SpawnZoneId, out z))
                z.PendingRespawns.Add(World.Time + z.Def.RespawnSeconds); // conta a partir da morte
        }

        private void OnPlayerKilled(Combatant player, Combatant killer)
        {
            _pendingRevives.Add(new KeyValuePair<Combatant, double>(player, World.Time + Data.Combat.PlayerRespawnSeconds));
        }

        private void SpawnOne(ZoneState z)
        {
            Vec2 center = new Vec2(z.Def.CenterX, z.Def.CenterZ);
            Vec2 pos = center;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                double angle = _rng.NextDouble() * Math.PI * 2.0;
                double r = Math.Sqrt(_rng.NextDouble()) * z.Def.Radius;
                Vec2 p = new Vec2(center.X + Math.Cos(angle) * r, center.Z + Math.Sin(angle) * r);
                if (!Map.IsBlocked(p, World.BodyRadius)) { pos = p; break; }
            }
            World.AddMonster(z.Monster, pos, z.Def.Id);
        }
    }
}
