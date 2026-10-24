using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Mmorpg.Core.Characters
{
    public sealed class ItemStackSave
    {
        public int Slot { get; set; }
        public string ItemId { get; set; }
        public int Count { get; set; }
    }

    /// <summary>
    /// Formato de persistência do personagem. NUNCA confie nele: em um jogo online o servidor guarda e valida
    /// o save (CharacterRestore.Restore recusa ids desconhecidos, contagens inválidas, itens que não podem
    /// ser equipados etc.).
    /// </summary>
    public sealed class CharacterSave
    {
        public int SchemaVersion { get; set; } = 1;
        public string Name { get; set; }
        public string ClassId { get; set; }
        public int Level { get; set; } = 1;
        public long Xp { get; set; }
        public long Gold { get; set; }
        public List<ItemStackSave> Inventory { get; set; } = new List<ItemStackSave>();
        public Dictionary<string, string> Equipment { get; set; } = new Dictionary<string, string>();
        public List<string> LearnedSkills { get; set; } = new List<string>();
    }

    public sealed class SaveValidationException : Exception
    {
        public IList<string> Problems { get; private set; }

        public SaveValidationException(IList<string> problems)
            : base("Save inválido: " + string.Join("; ", problems))
        {
            Problems = problems;
        }
    }

    public static class CharacterSaveJson
    {
        public static string ToJson(CharacterSave save)
        {
            return JsonConvert.SerializeObject(save, Formatting.Indented);
        }

        /// <summary>Lê o JSON; lança <see cref="SaveValidationException"/> se estiver corrompido.</summary>
        public static CharacterSave FromJson(string json)
        {
            try
            {
                var settings = new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error };
                CharacterSave s = JsonConvert.DeserializeObject<CharacterSave>(json, settings);
                if (s == null) throw new SaveValidationException(new List<string> { "arquivo vazio" });
                return s;
            }
            catch (JsonException ex)
            {
                throw new SaveValidationException(new List<string> { "JSON corrompido: " + ex.Message });
            }
        }
    }

    /// <summary>Armazenamento de personagens. A implementação local é para o protótipo; o servidor usará banco de dados.</summary>
    public interface ICharacterStore
    {
        bool Exists(string name);
        void Save(CharacterSave save);
        CharacterSave Load(string name);
    }

    /// <summary>Um arquivo JSON por personagem. Escreve em arquivo temporário e troca (evita save pela metade).</summary>
    public sealed class JsonFileCharacterStore : ICharacterStore
    {
        private readonly string _directory;

        public JsonFileCharacterStore(string directory)
        {
            _directory = directory;
        }

        private string PathFor(string name)
        {
            var sb = new StringBuilder();
            foreach (char ch in name) sb.Append(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? ch : '_');
            return Path.Combine(_directory, sb + ".character.json");
        }

        public bool Exists(string name) { return File.Exists(PathFor(name)); }

        public void Save(CharacterSave save)
        {
            Directory.CreateDirectory(_directory);
            string path = PathFor(save.Name);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, CharacterSaveJson.ToJson(save), Encoding.UTF8);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        public CharacterSave Load(string name)
        {
            return CharacterSaveJson.FromJson(File.ReadAllText(PathFor(name), Encoding.UTF8));
        }
    }
}
