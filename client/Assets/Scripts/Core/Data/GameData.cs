using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Mmorpg.Core.Data
{
    // ------------------------------------------------------------ fonte de dados
    /// <summary>Origem dos arquivos JSON. O nome vem sem extensão (ex.: "skills").</summary>
    public interface IDataSource
    {
        string ReadAllText(string name);
    }

    /// <summary>Lê os JSON de uma pasta (testes, servidor, ferramentas).</summary>
    public sealed class FileDataSource : IDataSource
    {
        private readonly string _directory;

        public FileDataSource(string directory) { _directory = directory; }

        public string ReadAllText(string name)
        {
            return File.ReadAllText(Path.Combine(_directory, name + ".json"), Encoding.UTF8);
        }
    }

    // ---------------------------------------------------------- relatório de erros
    public enum Severity { Error, Warning }

    public sealed class ValidationIssue
    {
        public Severity Severity { get; private set; }
        public string Where { get; private set; }
        public string Message { get; private set; }

        public ValidationIssue(Severity severity, string where, string message)
        {
            Severity = severity;
            Where = where;
            Message = message;
        }

        public override string ToString()
        {
            return (Severity == Severity.Error ? "[ERRO] " : "[AVISO] ") + Where + ": " + Message;
        }
    }

    public sealed class ValidationReport
    {
        private readonly List<ValidationIssue> _issues = new List<ValidationIssue>();

        public IReadOnlyList<ValidationIssue> Issues { get { return _issues; } }

        public void Error(string where, string message) { _issues.Add(new ValidationIssue(Severity.Error, where, message)); }

        public void Warning(string where, string message) { _issues.Add(new ValidationIssue(Severity.Warning, where, message)); }

        public int ErrorCount { get { return _issues.Count(i => i.Severity == Severity.Error); } }

        public int WarningCount { get { return _issues.Count(i => i.Severity == Severity.Warning); } }

        public bool HasErrors { get { return ErrorCount > 0; } }

        public string ToText()
        {
            var sb = new StringBuilder();
            foreach (var i in _issues) sb.AppendLine(i.ToString());
            sb.Append(ErrorCount + " erro(s), " + WarningCount + " aviso(s)");
            return sb.ToString();
        }
    }

    public sealed class GameDataException : Exception
    {
        public ValidationReport Report { get; private set; }

        public GameDataException(ValidationReport report) : base("Dados de jogo inválidos:\n" + report.ToText())
        {
            Report = report;
        }
    }

    // ------------------------------------------------------------------ GameData
    /// <summary>Todos os dados de balanceamento carregados e indexados. Somente leitura em jogo.</summary>
    public sealed class GameData
    {
        public RulesDef Rules { get; internal set; }
        public AttributesFile Attributes { get; internal set; }
        public CombatConfig Combat { get; internal set; }
        public ProgressionConfig Progression { get; internal set; }

        public Dictionary<string, RarityDef> Rarities { get; private set; } = new Dictionary<string, RarityDef>();
        public Dictionary<string, StatusDef> Statuses { get; private set; } = new Dictionary<string, StatusDef>();
        public Dictionary<string, ClassDef> Classes { get; private set; } = new Dictionary<string, ClassDef>();
        public Dictionary<string, SkillDef> Skills { get; private set; } = new Dictionary<string, SkillDef>();
        public Dictionary<string, ItemDef> Items { get; private set; } = new Dictionary<string, ItemDef>();
        public Dictionary<string, ShopDef> Shops { get; private set; } = new Dictionary<string, ShopDef>();
        public Dictionary<string, SetDef> Sets { get; private set; } = new Dictionary<string, SetDef>();
        public Dictionary<string, MonsterCategoryDef> MonsterCategories { get; private set; } = new Dictionary<string, MonsterCategoryDef>();
        public Dictionary<string, MonsterDef> Monsters { get; private set; } = new Dictionary<string, MonsterDef>();
        public Dictionary<string, LootTableDef> LootTables { get; private set; } = new Dictionary<string, LootTableDef>();
        public Dictionary<string, RegionDef> Regions { get; private set; } = new Dictionary<string, RegionDef>();
        public Dictionary<string, ResourceDef> Resources { get; private set; } = new Dictionary<string, ResourceDef>();
        public Dictionary<string, DerivedStatDef> DerivedStats { get; private set; } = new Dictionary<string, DerivedStatDef>();
        public List<string> PrimaryStatIds { get; private set; } = new List<string>();

        internal static void Index<T>(IEnumerable<T> list, Func<T, string> idOf, Dictionary<string, T> target,
            string file, ValidationReport report)
        {
            foreach (var item in list)
            {
                string id = idOf(item);
                if (string.IsNullOrEmpty(id))
                {
                    report.Error(file, "entrada sem 'id'");
                    continue;
                }
                if (target.ContainsKey(id))
                {
                    report.Error(file, "id duplicado: " + id);
                    continue;
                }
                target[id] = item;
            }
        }

        internal void BuildIndexes(RaritiesFile rarities, StatusesFile statuses, ClassesFile classes, SkillsFile skills,
            ItemsFile items, ShopsFile shops, SetsFile sets, MonstersFile monsters, LootFile loot, RegionsFile regions, ValidationReport report)
        {
            Index(rarities.Rarities, x => x.Id, Rarities, "rarities", report);
            Index(statuses.Statuses, x => x.Id, Statuses, "statuses", report);
            Index(classes.Classes, x => x.Id, Classes, "classes", report);
            Index(skills.Skills, x => x.Id, Skills, "skills", report);
            Index(items.Items, x => x.Id, Items, "items", report);
            Index(shops.Shops, x => x.Id, Shops, "shops", report);
            Index(sets.Sets, x => x.Id, Sets, "sets", report);
            Index(monsters.Categories, x => x.Id, MonsterCategories, "monsters.categories", report);
            Index(monsters.Monsters, x => x.Id, Monsters, "monsters", report);
            Index(loot.Tables, x => x.Id, LootTables, "loot_tables", report);
            Index(regions.Regions, x => x.Id, Regions, "regions", report);
            Index(Attributes.Resources, x => x.Id, Resources, "attributes.resources", report);
            Index(Attributes.Derived, x => x.Id, DerivedStats, "attributes.derived", report);
            PrimaryStatIds = Attributes.Primary.Select(p => p.Id).ToList();
        }

        /// <summary>True se o id é um atributo primário ou derivado conhecido.</summary>
        public bool IsKnownStat(string id)
        {
            return PrimaryStatIds.Contains(id) || DerivedStats.ContainsKey(id);
        }
    }
}
