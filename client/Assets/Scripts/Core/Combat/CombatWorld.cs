using System;
using System.Collections.Generic;
using Mmorpg.Core.Characters;
using Mmorpg.Core.Common;
using Mmorpg.Core.Data;
using Mmorpg.Core.Events;
using Mmorpg.Core.Stats;
using Mmorpg.Core.World;

namespace Mmorpg.Core.Combat
{
    /// <summary>
    /// Simulação autoritativa de combate (passo fixo/variável via <see cref="Update"/>). Não depende de Unity:
    /// o mesmo código roda no cliente (protótipo offline) e, no futuro, no servidor. Cada ação do jogador chega
    /// como comando (UseSkill, intenção de movimento) e é validada aqui; o resultado sai como <see cref="GameEvent"/>.
    /// </summary>
    public sealed partial class CombatWorld
    {
        private readonly List<Combatant> _all = new List<Combatant>();
        private readonly Dictionary<int, Combatant> _byId = new Dictionary<int, Combatant>();
        private readonly List<GameEvent> _events = new List<GameEvent>();
        private int _nextId = 1;

        public GameData Data { get; private set; }
        public IRng Rng { get; private set; }
        public IWorldMap Map { get; private set; }
        public double Time { get; private set; }
        /// <summary>PvP entre facções diferentes. Desligado na vertical slice (só PvE).</summary>
        public bool PvpEnabled { get; set; }

        /// <summary>(monstro, quem deu o golpe final). Disparado uma vez por morte.</summary>
        public event Action<Combatant, Combatant> MonsterKilled;
        public event Action<Combatant, Combatant> PlayerKilled;
        /// <summary>Disparado quando uma entidade sai do mundo (corpo removido ou jogador saiu).</summary>
        public event Action<Combatant> Removed;

        public CombatWorld(GameData data, IRng rng, IWorldMap map)
        {
            if (data == null) throw new ArgumentNullException("data");
            Data = data;
            Rng = rng ?? new SystemRng();
            Map = map ?? new OpenMap();
        }

        public IList<Combatant> All { get { return _all; } }

        public Combatant Get(int id)
        {
            Combatant c;
            return _byId.TryGetValue(id, out c) ? c : null;
        }

        public double BodyRadius { get { return Data.Combat.BodyRadius; } }

        // ------------------------------------------------------------------ eventos
        internal void Emit(GameEvent e)
        {
            e.Time = Time;
            _events.Add(e);
        }

        /// <summary>Entrega e limpa os eventos acumulados desde a última chamada.</summary>
        public List<GameEvent> DrainEvents()
        {
            var copy = new List<GameEvent>(_events);
            _events.Clear();
            return copy;
        }

        // ------------------------------------------------------------------ criação
        public Combatant AddPlayer(PlayerCharacter pc, Vec2 position)
        {
            var c = new Combatant(_nextId++, CombatantKind.Player)
            {
                Name = pc.Name,
                Faction = pc.Class.Faction,
                Level = pc.Level,
                Character = pc,
                Class = pc.Class,
                Position = position,
                ResourceId = pc.Class.Resource
            };
            RefreshPlayerStats(c, true);
            ResetResource(c);
            Register(c);
            Emit(new GameEvent { Type = GameEventType.Spawned, TargetId = c.Id, Text = c.Name, Position = position });
            return c;
        }

        public Combatant AddMonster(MonsterDef def, Vec2 position, string zoneId)
        {
            var c = new Combatant(_nextId++, CombatantKind.Monster)
            {
                Name = def.Name,
                Faction = "monsters",
                Level = def.Level,
                MonsterDef = def,
                Category = Data.MonsterCategories[def.CategoryId],
                Position = position,
                Home = position,
                SpawnZoneId = zoneId
            };
            c.BaseStats = StatCalculator.ComputeMonster(Data, def);
            c.Stats = c.BaseStats;
            c.Hp = c.MaxHp;
            Register(c);
            Emit(new GameEvent { Type = GameEventType.Spawned, TargetId = c.Id, Text = c.Name, Position = position });
            return c;
        }

        private void Register(Combatant c)
        {
            _all.Add(c);
            _byId[c.Id] = c;
        }

        public void Remove(Combatant c)
        {
            if (!_byId.Remove(c.Id)) return;
            _all.Remove(c);
            Emit(new GameEvent { Type = GameEventType.Despawned, TargetId = c.Id, Text = c.Name });
            if (Removed != null) Removed(c);
        }

        // ------------------------------------------------------------------ atributos
        /// <summary>Recalcula atributos do jogador (equipamento, nível, conjuntos) e reaplica buffs ativos.</summary>
        public void RefreshPlayerStats(Combatant c, bool fullHeal)
        {
            c.Level = c.Character.Level;
            c.BaseStats = c.Character.ComputeStats();
            RecomputeEffective(c);
            if (fullHeal) c.Hp = c.MaxHp;
            if (c.Hp > c.MaxHp) c.Hp = c.MaxHp;
            ClampResource(c);
        }

        internal void RecomputeEffective(Combatant c)
        {
            var defs = new List<StatusDef>();
            foreach (StatusInstance s in c.Statuses) if (s.Def.StatMods.Count > 0) defs.Add(s.Def);
            c.Stats = StatCalculator.ApplyMods(c.BaseStats, defs);
            if (c.Hp > c.MaxHp) c.Hp = c.MaxHp;
            ClampResource(c);
        }

        public double ResourceMax(Combatant c)
        {
            if (c.ResourceId == null) return 0;
            ResourceDef def = Data.Resources[c.ResourceId];
            return def.MaxStat != null ? c.Stats.Get(def.MaxStat) : def.Max;
        }

        private void ResetResource(Combatant c)
        {
            if (c.ResourceId == null) return;
            ResourceDef def = Data.Resources[c.ResourceId];
            c.Resource = def.StartFull ? ResourceMax(c) : 0;
        }

        private void ClampResource(Combatant c)
        {
            if (c.ResourceId == null) return;
            c.Resource = MathUtil.Clamp(c.Resource, 0, ResourceMax(c));
        }

        /// <summary>Volta à vida com vida/recurso cheios (respawn do jogador).</summary>
        public void Revive(Combatant c, Vec2 position)
        {
            c.Dead = false;
            c.Position = position;
            c.Statuses.Clear();
            c.Cast = null;
            c.MoveIntent = Vec2.Zero;
            c.LastCombatTime = -1e9;
            RecomputeEffective(c);
            c.Hp = c.MaxHp;
            ResetResource(c);
            Emit(new GameEvent { Type = GameEventType.Respawned, TargetId = c.Id, Position = position, Text = c.Name });
        }

        // ------------------------------------------------------------------ consultas
        public bool IsHostile(Combatant a, Combatant b)
        {
            if (a == null || b == null || a.Id == b.Id) return false;
            if (a.Kind != b.Kind) return true;
            if (a.IsMonster) return false;
            return PvpEnabled && a.Faction != b.Faction;
        }

        public static double Distance(Combatant a, Combatant b) { return Vec2.Distance(a.Position, b.Position); }

        /// <summary>Alvo hostil vivo mais próximo dentro de 'maxDistance' (usado por IA e atalhos de UI).</summary>
        public Combatant FindNearestHostile(Combatant from, double maxDistance)
        {
            Combatant best = null;
            double bestD = maxDistance;
            for (int i = 0; i < _all.Count; i++)
            {
                Combatant o = _all[i];
                if (o.Dead || !IsHostile(from, o)) continue;
                double d = Distance(from, o);
                if (d <= bestD) { bestD = d; best = o; }
            }
            return best;
        }

        public double SlowMultiplier(Combatant c)
        {
            double m = 1.0;
            for (int i = 0; i < c.Statuses.Count; i++)
                if (c.Statuses[i].Def.Kind == StatusKind.Slow) m *= c.Statuses[i].Def.MoveSpeedMultiplier;
            return Math.Max(m, Data.Combat.Control.MinMoveSpeedMultiplier);
        }

        public bool InCombat(Combatant c) { return Time - c.LastCombatTime <= Data.Combat.OutOfCombatSeconds; }

        // ------------------------------------------------------------------ comandos de jogador
        /// <summary>Define a intenção de movimento. O servidor limita o comprimento a 1 (impede "speed hack" por vetor grande).</summary>
        public void SetMoveIntent(Combatant c, double x, double z)
        {
            if (c == null || c.Dead || !c.IsPlayer) return;
            if (double.IsNaN(x) || double.IsNaN(z) || double.IsInfinity(x) || double.IsInfinity(z)) return;
            var v = new Vec2(x, z);
            double len = v.Length;
            if (len > 1.0) v = v * (1.0 / len);
            c.MoveIntent = v;
        }

        // ------------------------------------------------------------------ passo de simulação
        public void Update(double dt)
        {
            if (dt <= 0 || double.IsNaN(dt)) return;
            Time += dt;
            Combatant[] snapshot = _all.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                Combatant c = snapshot[i];
                if (!_byId.ContainsKey(c.Id)) continue;
                if (c.Dead)
                {
                    if (c.IsMonster && Time - c.DeathTime >= Data.Combat.CorpseSeconds) Remove(c);
                    continue;
                }
                UpdateStatuses(c);
                if (c.Dead) continue;
                UpdateResource(c, dt);
                UpdateRegen(c, dt);
                UpdateCast(c);
                if (c.Dead) continue;
                if (c.IsMonster) MonsterAi.Update(this, c, dt);
                else UpdatePlayerMovement(c, dt);
            }
        }

        private void UpdateResource(Combatant c, double dt)
        {
            if (c.ResourceId == null) return;
            ResourceDef def = Data.Resources[c.ResourceId];
            double regen = def.RegenStat != null ? c.Stats.Get(def.RegenStat) : def.RegenPerSecond;
            c.Resource += regen * dt;
            if (def.DecayPerSecond > 0 && Time - c.LastResourceGain >= def.DecayDelaySeconds) c.Resource -= def.DecayPerSecond * dt;
            ClampResource(c);
        }

        private void UpdateRegen(Combatant c, double dt)
        {
            if (!c.IsPlayer || InCombat(c) || c.Hp >= c.MaxHp) return;
            c.Hp = Math.Min(c.MaxHp, c.Hp + c.Stats.Get("hpRegen") * dt);
        }

        private void UpdatePlayerMovement(Combatant c, double dt)
        {
            if (c.MoveIntent.SqrLength < 1e-9) return;
            if (c.HasControl("stun") || c.HasControl("root")) return;
            if (c.Cast != null)
            {
                if (Data.Combat.MoveInterruptsCast) InterruptCast(c, "movimento");
            }
            double mag = Math.Min(1.0, c.MoveIntent.Length);
            Vec2 dir = c.MoveIntent.Normalized();
            double speed = c.Stats.Get("moveSpeed") * SlowMultiplier(c) * mag;
            Vec2 target = c.Position + dir * (speed * dt);
            c.Position = Map.Move(c.Position, target, BodyRadius);
            c.Facing = dir;
        }

        // ------------------------------------------------------------------ morte
        internal void Kill(Combatant target, int killerId)
        {
            if (target.Dead) return;
            target.Dead = true;
            target.Hp = 0;
            target.DeathTime = Time;
            target.MoveIntent = Vec2.Zero;
            target.Cast = null;
            target.Statuses.Clear();
            target.Invulnerable = false;
            target.Threat.Clear();
            RecomputeEffective(target);
            Combatant killer = Get(killerId);
            Emit(new GameEvent { Type = GameEventType.Died, SourceId = killerId, TargetId = target.Id, Text = target.Name, Position = target.Position });
            if (target.IsMonster)
            {
                if (MonsterKilled != null) MonsterKilled(target, killer);
            }
            else if (PlayerKilled != null) PlayerKilled(target, killer);
        }
    }
}
