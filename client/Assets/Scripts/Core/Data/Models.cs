using System.Collections.Generic;
using Newtonsoft.Json;

// Modelos de dados (DTOs) lidos dos JSON em Assets/Resources/GameData.
// Os nomes dos campos JSON são camelCase; a leitura é case-insensitive.
// Chaves desconhecidas no JSON são tratadas como ERRO (MissingMemberHandling.Error)
// para pegar erros de digitação em valores de balanceamento.
namespace Mmorpg.Core.Data
{
    // ------------------------------------------------------------------ enums
    public enum DamageType { Physical, Magic }

    public enum SkillOwner { Class, Monster }

    public enum SkillType { Damage, Heal, Buff, Debuff, Control, Utility, Mobility }

    public enum Targeting { Enemy, Ally, Self, Ground, SelfArea }

    /// <summary>Cleanse: lado aliado/self remove debuffs e controles; lado inimigo remove buffs, escudos e regenerações (dispel).</summary>
    public enum EffectKind { Damage, Heal, ApplyStatus, Shield, Dash, Cleanse }

    public enum EffectSide { Enemy, Ally, Self }

    public enum DashDirection { Toward, Away }

    public enum StatusKind { Stun, Root, Silence, Slow, Dot, Hot, Shield, Buff, Debuff, Taunt }

    public enum ItemType { Equipment, Material }

    // ------------------------------------------------------------------ rules
    public class FactionDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    /// <summary>Invariantes do projeto (24 classes, nível 55, custos de habilidade...). Usadas pelo validador.</summary>
    public class RulesDef
    {
        public int SchemaVersion { get; set; }
        public int MaxLevel { get; set; }
        public int ExpectedClassCount { get; set; }
        public int ClassesPerFaction { get; set; }
        public List<FactionDef> Factions { get; set; } = new List<FactionDef>();
        public int SkillsPerClass { get; set; }
        public int FreeSkillSlots { get; set; }
        public Dictionary<string, long> PaidSkillGoldCost { get; set; } = new Dictionary<string, long>();
        public List<string> ExpectedRarityIds { get; set; } = new List<string>();
        public List<string> ExpectedMonsterCategoryIds { get; set; } = new List<string>();
        public string BossCategoryId { get; set; }
        public int ExpectedRegionCount { get; set; }
        public double RegionalBossRespawnSeconds { get; set; }
        public List<string> EquipmentSlots { get; set; } = new List<string>();
        public int InventoryCapacity { get; set; }
        public long MaxGold { get; set; }
    }

    // ------------------------------------------------------------- attributes
    public class PrimaryStatDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class DerivedStatDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double Base { get; set; }
        public double PerLevel { get; set; }
        public Dictionary<string, double> PerPrimary { get; set; } = new Dictionary<string, double>();
    }

    public class ResourceDef
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        /// <summary>Se definido, o máximo vem deste atributo derivado; senão usa Max.</summary>
        public string MaxStat { get; set; }
        public double Max { get; set; }
        public string RegenStat { get; set; }
        public double RegenPerSecond { get; set; }
        public bool StartFull { get; set; }
        public double DecayPerSecond { get; set; }
        public double DecayDelaySeconds { get; set; }
        public double GainOnDealDamage { get; set; }
        public double GainOnTakeDamagePerPercent { get; set; }
    }

    public class AttributesFile
    {
        public int SchemaVersion { get; set; }
        public List<PrimaryStatDef> Primary { get; set; } = new List<PrimaryStatDef>();
        public List<DerivedStatDef> Derived { get; set; } = new List<DerivedStatDef>();
        public List<ResourceDef> Resources { get; set; } = new List<ResourceDef>();
    }

    // --------------------------------------------------------------- rarities
    public class RarityDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Order { get; set; }
        public string ColorHex { get; set; }
        /// <summary>Marcador textual (a raridade não pode depender só de cor).</summary>
        public string Tag { get; set; }
        public double StatBudgetMultiplier { get; set; }
        public double SellValueMultiplier { get; set; }
    }

    public class RaritiesFile
    {
        public int SchemaVersion { get; set; }
        public List<RarityDef> Rarities { get; set; } = new List<RarityDef>();
    }

    // --------------------------------------------------------------- statuses
    public class StatModDef
    {
        public double Add { get; set; }
        public double Mult { get; set; } = 1.0;
    }

    public class StatusDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public StatusKind Kind { get; set; }
        public double DefaultDuration { get; set; }
        public double TickInterval { get; set; }
        public int MaxInstancesPerTarget { get; set; } = 3;
        public double MoveSpeedMultiplier { get; set; } = 1.0;
        public Dictionary<string, StatModDef> StatMods { get; set; } = new Dictionary<string, StatModDef>();

        /// <summary>Chave de controle (stun/root/silence/slow/taunt) usada por limites, resistências e imunidades; null se não for controle.</summary>
        [JsonIgnore]
        public string ControlKey
        {
            get
            {
                switch (Kind)
                {
                    case StatusKind.Stun: return "stun";
                    case StatusKind.Root: return "root";
                    case StatusKind.Silence: return "silence";
                    case StatusKind.Slow: return "slow";
                    case StatusKind.Taunt: return "taunt";
                    default: return null;
                }
            }
        }
    }

    public class StatusesFile
    {
        public int SchemaVersion { get; set; }
        public List<StatusDef> Statuses { get; set; } = new List<StatusDef>();
    }

    // ----------------------------------------------------------------- combat
    public class MitigationConfig
    {
        public double K { get; set; }
        public double C { get; set; }
        public double MaxMitigation { get; set; }
    }

    public class AoeFalloffConfig
    {
        public int FreeTargets { get; set; }
        public double PerExtraTarget { get; set; }
        public double MinMultiplier { get; set; }
    }

    public class ControlConfig
    {
        public Dictionary<string, double> MaxDuration { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, double> ImmunityAfter { get; set; } = new Dictionary<string, double>();
        public double MinMoveSpeedMultiplier { get; set; }
    }

    public class ThreatConfig
    {
        public double DamageMultiplier { get; set; }
        public double HealingMultiplier { get; set; }
    }

    public class MonsterScalingConfig
    {
        public double HpBase { get; set; }
        public double HpPerLevel { get; set; }
        public double AttackBase { get; set; }
        public double AttackPerLevel { get; set; }
        public double DefenseBase { get; set; }
        public double DefensePerLevel { get; set; }
        public double ResistBase { get; set; }
        public double ResistPerLevel { get; set; }
        public double CritChance { get; set; }
        public double CritMultiplier { get; set; }
    }

    /// <summary>Parâmetros da IA dos monstros (perseguição, retorno, troca de alvo).</summary>
    public class AiConfig
    {
        /// <summary>Multiplicador de velocidade ao voltar para casa (evade).</summary>
        public double EvadeSpeedMultiplier { get; set; }
        /// <summary>Percentual da vida máxima recuperado por segundo ao voltar para casa.</summary>
        public double EvadeHealPercentPerSecond { get; set; }
        /// <summary>Fração do alcance da habilidade na qual o monstro para de se aproximar (0,1].</summary>
        public double AttackRangeFraction { get; set; }
        /// <summary>O ameaça de outro alvo precisa superar a do alvo atual por esta razão para trocar.</summary>
        public double TargetSwitchThreatRatio { get; set; }
    }

    /// <summary>Constantes de balanceamento do combate. Nada disso fica fixo no código.</summary>
    public class CombatConfig
    {
        public int SchemaVersion { get; set; }
        public double GlobalCooldown { get; set; }
        public double RangeTolerance { get; set; }
        public double CastRangeLeeway { get; set; }
        public bool MoveInterruptsCast { get; set; }
        /// <summary>Velocidade (0..1] enquanto se move conjurando uma habilidade com castWhileMoving.</summary>
        public double CastMoveSpeedMultiplier { get; set; } = 0.6;
        public MitigationConfig Mitigation { get; set; } = new MitigationConfig();
        public double DamageVariance { get; set; }
        public double CritChanceCap { get; set; }
        public AoeFalloffConfig AoeFalloff { get; set; } = new AoeFalloffConfig();
        public double PvpDamageMultiplier { get; set; }
        public ControlConfig Control { get; set; } = new ControlConfig();
        public double OutOfCombatSeconds { get; set; }
        public ThreatConfig Threat { get; set; } = new ThreatConfig();
        public double CorpseSeconds { get; set; }
        public double PlayerRespawnSeconds { get; set; }
        /// <summary>Raio de colisão de personagens e monstros contra obstáculos (metros).</summary>
        public double BodyRadius { get; set; }
        public AiConfig Ai { get; set; } = new AiConfig();
        public MonsterScalingConfig MonsterScaling { get; set; } = new MonsterScalingConfig();
    }

    // ------------------------------------------------------------ progression
    public class XpCurveConfig
    {
        public double A { get; set; }
        public double B { get; set; }
        public double C { get; set; }
    }

    public class MonsterXpConfig
    {
        public double Base { get; set; }
        public double PerLevel { get; set; }
    }

    public class LevelDifferenceConfig
    {
        public int FreeRange { get; set; }
        public double PenaltyPerLevel { get; set; }
        public double MinFactor { get; set; }
        public double BonusPerLevel { get; set; }
        public double MaxBonus { get; set; }
        public int LootCutoffLevels { get; set; }
    }

    public class ContributionConfig
    {
        public double MinShare { get; set; }
        public double HealingWeight { get; set; }
    }

    public class ProgressionConfig
    {
        public int SchemaVersion { get; set; }
        public int MaxLevel { get; set; }
        public XpCurveConfig XpCurve { get; set; } = new XpCurveConfig();
        public MonsterXpConfig MonsterXp { get; set; } = new MonsterXpConfig();
        public LevelDifferenceConfig LevelDifference { get; set; } = new LevelDifferenceConfig();
        public double GoldMultiplier { get; set; } = 1.0;
        public bool HealFullOnLevelUp { get; set; }
        public ContributionConfig Contribution { get; set; } = new ContributionConfig();
    }

    // ---------------------------------------------------------------- classes
    public class ClassDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Faction { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
        public string Resource { get; set; }
        public Dictionary<string, double> PrimaryBase { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, double> PrimaryPerLevel { get; set; } = new Dictionary<string, double>();
        public List<string> WeaponTypes { get; set; } = new List<string>();
        public List<string> StartingItems { get; set; } = new List<string>();
        public List<string> SkillIds { get; set; } = new List<string>();
        public string Description { get; set; }
    }

    public class ClassesFile
    {
        public int SchemaVersion { get; set; }
        public List<ClassDef> Classes { get; set; } = new List<ClassDef>();
    }

    // ----------------------------------------------------------------- skills
    public class SkillEffectDef
    {
        public EffectKind Kind { get; set; }
        public EffectSide? Side { get; set; }
        public DamageType DamageType { get; set; }
        public double Base { get; set; }
        public Dictionary<string, double> Scaling { get; set; } = new Dictionary<string, double>();
        public string StatusId { get; set; }
        /// <summary>0 = usa a duração padrão do status.</summary>
        public double Duration { get; set; }
        public double Chance { get; set; } = 1.0;
        public DashDirection DashDirection { get; set; }
        public double DashDistance { get; set; }
        /// <summary>Damage: número de golpes independentes (cada um rola crítico/variação). Padrão 1.</summary>
        public int Hits { get; set; } = 1;
        /// <summary>Damage: fração do dano causado devolvida como cura ao conjurador (0.3 = 30%).</summary>
        public double Leech { get; set; }
        /// <summary>Damage: se a vida do alvo (%) estiver abaixo disto, o dano é multiplicado por ExecuteMultiplier.</summary>
        public double ExecuteBelowHpPercent { get; set; }
        public double ExecuteMultiplier { get; set; } = 1.0;
        /// <summary>Damage: se o alvo tiver este status, o dano é multiplicado por BonusMultiplier.</summary>
        public string BonusIfStatusId { get; set; }
        public double BonusMultiplier { get; set; } = 1.0;
        /// <summary>Cleanse: quantos status remover (0 = todos os aplicáveis).</summary>
        public int Count { get; set; }

        /// <summary>Lado afetado; se não informado, deduzido do tipo do efeito.</summary>
        public EffectSide ResolvedSide()
        {
            if (Side.HasValue) return Side.Value;
            switch (Kind)
            {
                case EffectKind.Heal: return EffectSide.Ally;
                case EffectKind.Shield: return EffectSide.Ally;
                case EffectKind.Cleanse: return EffectSide.Ally;
                case EffectKind.Dash: return EffectSide.Self;
                default: return EffectSide.Enemy;
            }
        }
    }

    public class SkillDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public SkillOwner OwnerType { get; set; }
        public string OwnerId { get; set; }
        /// <summary>1..9 para habilidades de classe; 0 para habilidades de monstros.</summary>
        public int Slot { get; set; }
        public long GoldCost { get; set; }
        public int RequiresLevel { get; set; } = 1;
        public SkillType Type { get; set; }
        public Targeting Targeting { get; set; }
        public double Range { get; set; }
        public double Radius { get; set; }
        public int MaxTargets { get; set; } = 1;
        public double Cooldown { get; set; }
        public double CastTime { get; set; }
        public double ResourceCost { get; set; }
        public bool UsesGlobalCooldown { get; set; } = true;
        public bool Silenceable { get; set; } = true;
        /// <summary>Pode ser conjurada andando (com velocidade reduzida, CombatConfig.CastMoveSpeedMultiplier). As demais são interrompidas pelo movimento.</summary>
        public bool CastWhileMoving { get; set; }
        public double PvpMultiplier { get; set; } = 1.0;
        public List<SkillEffectDef> Effects { get; set; } = new List<SkillEffectDef>();
        public string Description { get; set; }
        // Identificadores de apresentação (placeholders: os assets ainda não existem).
        public string Animation { get; set; }
        public string Vfx { get; set; }
        /// <summary>Elemento (cor/efeito visual): physical, arcane, fire, ice, wind, earth, light, dark. Não altera o dano.</summary>
        public string Element { get; set; }
        public string Sfx { get; set; }
    }

    public class SkillsFile
    {
        public int SchemaVersion { get; set; }
        public List<SkillDef> Skills { get; set; } = new List<SkillDef>();
    }

    // ------------------------------------------------------------------ items
    public class ItemDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public ItemType Type { get; set; }
        public string Slot { get; set; }
        public string Rarity { get; set; }
        public int RequiredLevel { get; set; } = 1;
        public string WeaponType { get; set; }
        public List<string> AllowedClasses { get; set; } = new List<string>();
        public Dictionary<string, double> Stats { get; set; } = new Dictionary<string, double>();
        public string SetId { get; set; }
        public int MaxStack { get; set; } = 1;
        public long SellValue { get; set; }
        public bool Tradable { get; set; } = true;
        public string Origin { get; set; }
        public string Description { get; set; }
    }

    public class ItemsFile
    {
        public int SchemaVersion { get; set; }
        public List<ItemDef> Items { get; set; } = new List<ItemDef>();
    }

    public class ShopEntryDef
    {
        public string ItemId { get; set; }
        /// <summary>Preço de compra em ouro (sempre maior que o sellValue do item, para não haver ganho em comprar e revender).</summary>
        public long Price { get; set; }
    }

    public class ShopDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<ShopEntryDef> Stock { get; set; } = new List<ShopEntryDef>();
    }

    public class ShopsFile
    {
        public int SchemaVersion { get; set; }
        public List<ShopDef> Shops { get; set; } = new List<ShopDef>();
    }

    public class SetBonusDef
    {
        public int Pieces { get; set; }
        public Dictionary<string, double> Stats { get; set; } = new Dictionary<string, double>();
        public string Description { get; set; }
    }

    public class SetDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<string> Pieces { get; set; } = new List<string>();
        public List<SetBonusDef> Bonuses { get; set; } = new List<SetBonusDef>();
    }

    public class SetsFile
    {
        public int SchemaVersion { get; set; }
        public List<SetDef> Sets { get; set; } = new List<SetDef>();
    }

    // --------------------------------------------------------------- monsters
    public class MonsterCategoryDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Tag { get; set; }
        public string ColorHex { get; set; }
        public double HpMultiplier { get; set; } = 1.0;
        public double DamageMultiplier { get; set; } = 1.0;
        public double XpMultiplier { get; set; } = 1.0;
        public double GoldMultiplier { get; set; } = 1.0;
    }

    public class BossPhaseDef
    {
        public string Id { get; set; }
        /// <summary>A fase vale enquanto a vida estiver em ou abaixo deste percentual (100 = fase inicial).</summary>
        public double HpBelowPercent { get; set; }
        public List<string> SkillIds { get; set; } = new List<string>();
        public double DamageMultiplier { get; set; } = 1.0;
        public double CooldownMultiplier { get; set; } = 1.0;
        public string Announcement { get; set; }
    }

    public class MonsterDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string CategoryId { get; set; }
        public int Level { get; set; }
        public string RegionId { get; set; }
        public double AggroRange { get; set; }
        public double LeashRange { get; set; }
        public double MoveSpeed { get; set; }
        public double HpMultiplier { get; set; } = 1.0;
        public double DamageMultiplier { get; set; } = 1.0;
        public List<string> SkillIds { get; set; } = new List<string>();
        public List<BossPhaseDef> Phases { get; set; } = new List<BossPhaseDef>();
        /// <summary>Multiplicador da duração por tipo de controle (stun/root/silence/slow/taunt); 0 = imune.</summary>
        public Dictionary<string, double> CcResist { get; set; } = new Dictionary<string, double>();
        public string LootTableId { get; set; }
        /// <summary>Escala visual (apenas apresentação).</summary>
        public double Scale { get; set; } = 1.0;
        public string Description { get; set; }
    }

    public class MonstersFile
    {
        public int SchemaVersion { get; set; }
        public List<MonsterCategoryDef> Categories { get; set; } = new List<MonsterCategoryDef>();
        public List<MonsterDef> Monsters { get; set; } = new List<MonsterDef>();
    }

    // ------------------------------------------------------------------- loot
    public class LootEntryDef
    {
        public string ItemId { get; set; }
        public double Chance { get; set; }
        public int Min { get; set; } = 1;
        public int Max { get; set; } = 1;
    }

    public class LootTableDef
    {
        public string Id { get; set; }
        public long GoldMin { get; set; }
        public long GoldMax { get; set; }
        /// <summary>Sobrescreve a contribuição mínima global; null usa progression.contribution.minShare.</summary>
        public double? MinContributionShare { get; set; }
        /// <summary>Máximo de itens (entradas sorteadas) por jogador por morte; 0 = sem limite.</summary>
        public int MaxItemsPerPlayer { get; set; }
        /// <summary>Máximo de itens de cada raridade entre TODOS os jogadores em uma morte.</summary>
        public Dictionary<string, int> RarityCapsPerKill { get; set; } = new Dictionary<string, int>();
        public List<LootEntryDef> Entries { get; set; } = new List<LootEntryDef>();
    }

    public class LootFile
    {
        public int SchemaVersion { get; set; }
        public List<LootTableDef> Tables { get; set; } = new List<LootTableDef>();
    }

    // ---------------------------------------------------------------- regions
    public class BoundsDef
    {
        public double MinX { get; set; }
        public double MaxX { get; set; }
        public double MinZ { get; set; }
        public double MaxZ { get; set; }
    }

    public class PointDef
    {
        public double X { get; set; }
        public double Z { get; set; }
    }

    public class SpawnZoneDef
    {
        public string Id { get; set; }
        public string MonsterId { get; set; }
        public double CenterX { get; set; }
        public double CenterZ { get; set; }
        public double Radius { get; set; }
        public int Count { get; set; }
        public double RespawnSeconds { get; set; }
    }

    public class ObstacleDef
    {
        /// <summary>tree | rock | ruin (apenas apresentação; a colisão é um círculo).</summary>
        public string Kind { get; set; }
        public double X { get; set; }
        public double Z { get; set; }
        public double Radius { get; set; }
    }

    public class RegionDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Order { get; set; }
        public int MinLevel { get; set; }
        public int MaxLevel { get; set; }
        public bool Implemented { get; set; }
        public string BossName { get; set; }
        public string Description { get; set; }
        public BoundsDef Bounds { get; set; }
        public PointDef PlayerSpawn { get; set; }
        public List<SpawnZoneDef> SpawnZones { get; set; } = new List<SpawnZoneDef>();
        public List<ObstacleDef> Obstacles { get; set; } = new List<ObstacleDef>();
    }

    public class RegionsFile
    {
        public int SchemaVersion { get; set; }
        public List<RegionDef> Regions { get; set; } = new List<RegionDef>();
    }
}
