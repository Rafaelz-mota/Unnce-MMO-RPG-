using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Mmorpg.Core.Data
{
    public sealed class LoadResult
    {
        /// <summary>Dados indexados (não nulo se todos os arquivos foram lidos; veja Report para erros de validação).</summary>
        public GameData Data { get; internal set; }

        public ValidationReport Report { get; internal set; }

        /// <summary>True somente se leu tudo e a validação não encontrou erros.</summary>
        public bool Success { get { return Data != null && !Report.HasErrors; } }
    }

    /// <summary>Lê e valida os JSON de balanceamento. Nunca lança por dado inválido: devolve o relatório.</summary>
    public static class GameDataLoader
    {
        public const int SupportedSchemaVersion = 1;

        public static JsonSerializerSettings CreateSettings()
        {
            var settings = new JsonSerializerSettings
            {
                // Chave desconhecida no JSON = erro (pega "cooldwon" e similares).
                MissingMemberHandling = MissingMemberHandling.Error,
                NullValueHandling = NullValueHandling.Ignore
            };
            settings.Converters.Add(new StringEnumConverter());
            return settings;
        }

        public static LoadResult Load(IDataSource source)
        {
            var report = new ValidationReport();
            var settings = CreateSettings();

            RulesDef rules = Read<RulesDef>(source, "rules", settings, report);
            AttributesFile attributes = Read<AttributesFile>(source, "attributes", settings, report);
            RaritiesFile rarities = Read<RaritiesFile>(source, "rarities", settings, report);
            StatusesFile statuses = Read<StatusesFile>(source, "statuses", settings, report);
            CombatConfig combat = Read<CombatConfig>(source, "combat", settings, report);
            ProgressionConfig progression = Read<ProgressionConfig>(source, "progression", settings, report);
            ClassesFile classes = Read<ClassesFile>(source, "classes", settings, report);
            SkillsFile skills = Read<SkillsFile>(source, "skills", settings, report);
            ItemsFile items = Read<ItemsFile>(source, "items", settings, report);
            SetsFile sets = Read<SetsFile>(source, "sets", settings, report);
            MonstersFile monsters = Read<MonstersFile>(source, "monsters", settings, report);
            LootFile loot = Read<LootFile>(source, "loot_tables", settings, report);
            RegionsFile regions = Read<RegionsFile>(source, "regions", settings, report);

            if (report.HasErrors)
            {
                return new LoadResult { Data = null, Report = report };
            }

            CheckSchema("rules", rules.SchemaVersion, report);
            CheckSchema("attributes", attributes.SchemaVersion, report);
            CheckSchema("rarities", rarities.SchemaVersion, report);
            CheckSchema("statuses", statuses.SchemaVersion, report);
            CheckSchema("combat", combat.SchemaVersion, report);
            CheckSchema("progression", progression.SchemaVersion, report);
            CheckSchema("classes", classes.SchemaVersion, report);
            CheckSchema("skills", skills.SchemaVersion, report);
            CheckSchema("items", items.SchemaVersion, report);
            CheckSchema("sets", sets.SchemaVersion, report);
            CheckSchema("monsters", monsters.SchemaVersion, report);
            CheckSchema("loot_tables", loot.SchemaVersion, report);
            CheckSchema("regions", regions.SchemaVersion, report);
            if (report.HasErrors)
            {
                return new LoadResult { Data = null, Report = report };
            }

            var data = new GameData
            {
                Rules = rules,
                Attributes = attributes,
                Combat = combat,
                Progression = progression
            };
            data.BuildIndexes(rarities, statuses, classes, skills, items, sets, monsters, loot, regions, report);
            GameDataValidator.Validate(data, report);
            return new LoadResult { Data = data, Report = report };
        }

        /// <summary>Carrega e lança <see cref="GameDataException"/> se houver qualquer erro.</summary>
        public static GameData LoadOrThrow(IDataSource source)
        {
            LoadResult result = Load(source);
            if (!result.Success) throw new GameDataException(result.Report);
            return result.Data;
        }

        private static void CheckSchema(string file, int version, ValidationReport report)
        {
            if (version != SupportedSchemaVersion)
            {
                report.Error(file + ".json", "schemaVersion " + version + " não suportado (esperado " + SupportedSchemaVersion + ")");
            }
        }

        private static T Read<T>(IDataSource source, string name, JsonSerializerSettings settings, ValidationReport report)
            where T : class
        {
            string text;
            try
            {
                text = source.ReadAllText(name);
            }
            catch (Exception ex)
            {
                report.Error(name + ".json", "não foi possível ler o arquivo: " + ex.Message);
                return null;
            }

            try
            {
                T value = JsonConvert.DeserializeObject<T>(text, settings);
                if (value == null)
                {
                    report.Error(name + ".json", "conteúdo vazio ou nulo");
                }
                return value;
            }
            catch (JsonException ex)
            {
                report.Error(name + ".json", "JSON inválido: " + ex.Message);
                return null;
            }
        }
    }
}
