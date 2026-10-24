using System.Collections.Generic;
using Mmorpg.Core.Characters;
using Mmorpg.Core.Common;
using Mmorpg.Core.Data;
using Mmorpg.Core.Stats;

namespace Mmorpg.Core.Combat
{
    public enum CombatantKind { Player, Monster }

    public enum AiState { Idle, Combat, Evade }

    public sealed class StatusInstance
    {
        public StatusDef Def;
        public int SourceId;
        public int SourceLevel;
        public bool SourceIsPlayer;
        public string SkillId;
        public double AppliedAt;
        public double ExpiresAt;
        public double NextTickAt;
        /// <summary>Dano/cura por tick (dot/hot).</summary>
        public double Value;
        public DamageType DamageType;
        /// <summary>Quanto o escudo ainda absorve.</summary>
        public double ShieldRemaining;
    }

    public sealed class CastState
    {
        public SkillDef Skill;
        public double StartedAt;
        public double EndsAt;
        public int TargetId;
        public Vec2 Ground;
    }

    /// <summary>Qualquer entidade que luta: jogador ou monstro. É só estado; as regras ficam em <see cref="CombatWorld"/>.</summary>
    public sealed class Combatant
    {
        public readonly int Id;
        public readonly CombatantKind Kind;
        public string Name;
        public string Faction;
        public int Level;

        public Vec2 Position;
        public Vec2 Facing = new Vec2(0, 1);
        /// <summary>Direção desejada de movimento (comprimento 0..1). Definida pelo comando do jogador; consumida a cada tick.</summary>
        public Vec2 MoveIntent;

        public StatSheet BaseStats;
        /// <summary>Atributos efetivos (base + buffs/debuffs ativos).</summary>
        public StatSheet Stats;

        public double Hp;
        public bool Dead;
        public double DeathTime;
        /// <summary>Verdadeiro enquanto o monstro volta para casa (não recebe dano nem controle).</summary>
        public bool Invulnerable;

        public string ResourceId;
        public double Resource;
        public double LastResourceGain;

        public readonly List<StatusInstance> Statuses = new List<StatusInstance>();
        public readonly Dictionary<string, double> CooldownUntil = new Dictionary<string, double>();
        public readonly Dictionary<string, double> ControlImmuneUntil = new Dictionary<string, double>();
        public double GlobalCooldownUntil;
        public CastState Cast;
        public double LastCombatTime = -1e9;

        // ---- jogador
        public PlayerCharacter Character;
        public ClassDef Class;

        // ---- monstro
        public MonsterDef MonsterDef;
        public MonsterCategoryDef Category;
        public Vec2 Home;
        public AiState AiState;
        public double EvadeStartedAt;
        public int TargetId;
        public readonly Dictionary<int, double> Threat = new Dictionary<int, double>();
        public readonly Dictionary<int, double> Contribution = new Dictionary<int, double>();
        public string SpawnZoneId;
        public int PhaseIndex = -1;
        public double PhaseDamageMultiplier = 1.0;
        public double PhaseCooldownMultiplier = 1.0;

        public Combatant(int id, CombatantKind kind)
        {
            Id = id;
            Kind = kind;
        }

        public bool IsPlayer { get { return Kind == CombatantKind.Player; } }

        public bool IsMonster { get { return Kind == CombatantKind.Monster; } }

        public double MaxHp { get { return Stats.Get("maxHp"); } }

        public double HpPercent { get { double m = MaxHp; return m <= 0 ? 0 : Hp / m * 100.0; } }

        public bool HasControl(string key)
        {
            for (int i = 0; i < Statuses.Count; i++)
                if (Statuses[i].Def.ControlKey == key) return true;
            return false;
        }

        public double ShieldTotal
        {
            get
            {
                double t = 0;
                for (int i = 0; i < Statuses.Count; i++)
                    if (Statuses[i].Def.Kind == StatusKind.Shield) t += Statuses[i].ShieldRemaining;
                return t;
            }
        }

        public override string ToString() { return Name + "#" + Id; }
    }
}
