using System;
using System.Collections.Generic;
using System.Linq;
using Mmorpg.Core.Characters;
using Mmorpg.Core.Combat;
using Mmorpg.Core.Common;
using Mmorpg.Core.Data;
using Mmorpg.Core.Events;
using Mmorpg.Core.Rewards;
using Mmorpg.Core.World;

namespace Mmorpg.Core.Tests
{
    /// <summary>RNG fixo: 0,5 em tudo (sem variação de dano e sem crítico, pois critChance &lt; 0,5).</summary>
    public sealed class FixedRng : IRng
    {
        private readonly double _value;
        public FixedRng(double value) { _value = value; }
        public double NextDouble() { return _value; }
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(_value * (maxExclusive - minInclusive));
        }
    }

    /// <summary>Monta um mundo de combate pequeno e determinístico para os testes (dados novos a cada instância: pode mutar à vontade).</summary>
    public sealed class TestWorld
    {
        public GameData Data { get; private set; }
        public CombatWorld World { get; private set; }
        public RewardService Rewards { get; private set; }
        public List<GameEvent> Events { get; private set; } = new List<GameEvent>();

        public TestWorld(IRng rng = null, IWorldMap map = null, GameData data = null)
        {
            Data = data ?? TestData.Fresh();
            IRng r = rng ?? new FixedRng(0.5);
            World = new CombatWorld(Data, r, map ?? new OpenMap());
            Rewards = new RewardService(World, r);
        }

        public static void SetLevel(PlayerCharacter pc, int level)
        {
            while (pc.Level < level) pc.AddXp(pc.XpToNext);
            pc.SyncFreeSkills();
        }

        public Combatant Player(string classId, int level = 1, double x = 0, double z = 0, string name = null)
        {
            PlayerCharacter pc = PlayerCharacter.CreateNew(Data, name ?? (classId + "_" + (World.All.Count + 1)), classId);
            SetLevel(pc, level);
            Combatant c = World.AddPlayer(pc, new Vec2(x, z));
            World.RefreshPlayerStats(c, true);
            return c;
        }

        /// <summary>Monstro real (IA e habilidades normais).</summary>
        public Combatant Monster(string monsterId, double x, double z)
        {
            return World.AddMonster(Data.Monsters[monsterId], new Vec2(x, z), null);
        }

        /// <summary>Saco de pancada: não ataca, não anda, não agride sozinho e tem muita vida. Altera a definição (dados são novos por TestWorld).</summary>
        public Combatant Dummy(double x, double z, string monsterId = "broto_ancestral", double hpMultiplier = 1000.0)
        {
            MonsterDef def = Data.Monsters[monsterId];
            def.AggroRange = 0;
            def.SkillIds = new List<string>();
            def.Phases = new List<BossPhaseDef>();
            def.MoveSpeed = 0;
            def.HpMultiplier = hpMultiplier;
            return World.AddMonster(def, new Vec2(x, z), null);
        }

        public void Step(double seconds, double dt = 0.05)
        {
            double left = seconds;
            while (left > 1e-9)
            {
                double d = Math.Min(dt, left);
                World.Update(d);
                Events.AddRange(World.DrainEvents());
                left -= d;
            }
        }

        public void Flush() { Events.AddRange(World.DrainEvents()); }

        public IEnumerable<GameEvent> Of(GameEventType type) { return Events.Where(e => e.Type == type); }

        public int Count(GameEventType type) { return Events.Count(e => e.Type == type); }

        public void ClearEvents() { Flush(); Events.Clear(); }

        public static void ForceStats(Combatant c, string stat, double value)
        {
            c.BaseStats.Set(stat, value);
            c.Stats.Set(stat, value);
        }

        public UseSkillResult Use(Combatant caster, string skillId, Combatant target = null)
        {
            return World.UseSkill(caster, skillId, target != null ? target.Id : 0, target != null ? target.Position : caster.Position);
        }

        /// <summary>Dá ao personagem todas as habilidades da classe sem pagar (para testes de catálogo).</summary>
        public static CharacterSave SaveWithAllSkills(GameData d, string classId, int level, string name)
        {
            ClassDef cls = d.Classes[classId];
            var save = new CharacterSave { Name = name, ClassId = classId, Level = level, Xp = 0, Gold = 0 };
            save.LearnedSkills.AddRange(cls.SkillIds);
            return save;
        }
    }
}
