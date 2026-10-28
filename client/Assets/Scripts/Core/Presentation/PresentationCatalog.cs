using System;
using System.Collections.Generic;
using System.Linq;
using Mmorpg.Core.Data;
using Newtonsoft.Json;

namespace Mmorpg.Core.Presentation
{
    /// <summary>Aparência de uma classe (silhueta, paleta, acessórios). Só apresentação: não afeta regras.</summary>
    public class ClassLookDef
    {
        public string Id { get; set; }
        /// <summary>Caminho em Resources de um prefab humanoide real (opcional). Vazio ou ausente = visual provisório de primitivas.</summary>
        public string Prefab { get; set; }
        public string Cloth { get; set; }
        public string Accent { get; set; }
        public string Skin { get; set; } = "#E6BD99";
        /// <summary>slim, normal ou heavy.</summary>
        public string Build { get; set; } = "normal";
        public double Height { get; set; } = 1.0;
        public bool Robe { get; set; }
        public bool Cape { get; set; }
        /// <summary>none, round, spiked ou large.</summary>
        public string Shoulders { get; set; } = "none";
        /// <summary>none, helm, hat, hood, crown, horns, goggles, headband, plume, mask, skull ou circlet.</summary>
        public string Headgear { get; set; } = "none";
        /// <summary>none, quiver, backpack ou tome.</summary>
        public string Back { get; set; } = "none";
        /// <summary>none ou shield.</summary>
        public string OffHand { get; set; } = "none";
    }

    /// <summary>Aparência de um item equipável.</summary>
    public class ItemLookDef
    {
        public string Id { get; set; }
        /// <summary>Caminho em Resources de um prefab real (opcional); sem ele, usa-se a forma provisória em Shape.</summary>
        public string Prefab { get; set; }
        public string Shape { get; set; }
        public string Color { get; set; }
        /// <summary>Opcional: acabamento brilhante (raridades altas).</summary>
        public string Glow { get; set; }
        /// <summary>hand_r ou hand_l (armas).</summary>
        public string Socket { get; set; }
    }

    public class PresentationFile
    {
        public int SchemaVersion { get; set; }
        public List<ClassLookDef> Classes { get; set; } = new List<ClassLookDef>();
        public List<ItemLookDef> Items { get; set; } = new List<ItemLookDef>();
    }

    /// <summary>
    /// Catálogo que liga classes e itens à aparência. Os dados ficam em presentation.json (não no código), e é
    /// validado contra classes.json e items.json. Itens sem entrada usam uma aparência padrão derivada do tipo.
    /// </summary>
    public sealed class PresentationCatalog
    {
        public static readonly string[] Builds = { "slim", "normal", "heavy" };
        public static readonly string[] ShoulderStyles = { "none", "round", "spiked", "large" };
        public static readonly string[] Headgears = { "none", "helm", "hat", "hood", "crown", "horns", "goggles", "headband", "plume", "mask", "skull", "circlet" };
        public static readonly string[] Backs = { "none", "quiver", "backpack", "tome" };
        public static readonly string[] OffHands = { "none", "shield" };
        public static readonly string[] WeaponShapes = { "sword", "dagger", "staff", "bow", "scepter", "axe", "mace", "spear", "wand", "stick" };
        public static readonly string[] ArmorShapes = { "cap", "hood", "helm", "tunic", "plate", "pants", "boots", "none" };
        public static readonly string[] Sockets = { "hand_r", "hand_l", "head", "chest", "legs", "feet" };

        public const int SupportedSchemaVersion = 1;

        private readonly Dictionary<string, ClassLookDef> _classes = new Dictionary<string, ClassLookDef>();
        private readonly Dictionary<string, ItemLookDef> _items = new Dictionary<string, ItemLookDef>();

        public IReadOnlyDictionary<string, ClassLookDef> Classes { get { return _classes; } }
        public IReadOnlyDictionary<string, ItemLookDef> Items { get { return _items; } }

        /// <summary>Lê e valida. Nunca lança por dado inválido: devolve a lista de problemas.</summary>
        public static PresentationCatalog Parse(string json, GameData data, List<string> problems)
        {
            var catalog = new PresentationCatalog();
            PresentationFile file;
            try
            {
                var settings = GameDataLoader.CreateSettings();
                file = JsonConvert.DeserializeObject<PresentationFile>(json, settings);
            }
            catch (Exception ex)
            {
                problems.Add("presentation.json inválido: " + ex.Message);
                return catalog;
            }
            if (file == null) { problems.Add("presentation.json vazio"); return catalog; }
            if (file.SchemaVersion != SupportedSchemaVersion) problems.Add("presentation.json: schemaVersion " + file.SchemaVersion + " não suportado");

            foreach (ClassLookDef c in file.Classes)
            {
                if (string.IsNullOrEmpty(c.Id)) { problems.Add("classe sem id"); continue; }
                if (catalog._classes.ContainsKey(c.Id)) { problems.Add("classe repetida: " + c.Id); continue; }
                catalog._classes[c.Id] = c;
                string w = "classe " + c.Id;
                if (data != null && !data.Classes.ContainsKey(c.Id)) problems.Add(w + ": não existe em classes.json");
                if (!IsHex(c.Cloth)) problems.Add(w + ": cloth inválido");
                if (!IsHex(c.Accent)) problems.Add(w + ": accent inválido");
                if (!IsHex(c.Skin)) problems.Add(w + ": skin inválido");
                if (!Builds.Contains(c.Build)) problems.Add(w + ": build inválido " + c.Build);
                if (!ShoulderStyles.Contains(c.Shoulders)) problems.Add(w + ": shoulders inválido " + c.Shoulders);
                if (!Headgears.Contains(c.Headgear)) problems.Add(w + ": headgear inválido " + c.Headgear);
                if (!Backs.Contains(c.Back)) problems.Add(w + ": back inválido " + c.Back);
                if (!OffHands.Contains(c.OffHand)) problems.Add(w + ": offHand inválido " + c.OffHand);
                if (c.Height < 0.8 || c.Height > 1.3) problems.Add(w + ": height fora de [0.8, 1.3]");
            }
            if (data != null)
                foreach (string id in data.Classes.Keys)
                    if (!catalog._classes.ContainsKey(id)) problems.Add("classe sem aparência em presentation.json: " + id);

            foreach (ItemLookDef i in file.Items)
            {
                if (string.IsNullOrEmpty(i.Id)) { problems.Add("item sem id"); continue; }
                if (catalog._items.ContainsKey(i.Id)) { problems.Add("item repetido: " + i.Id); continue; }
                catalog._items[i.Id] = i;
                string w = "item " + i.Id;
                ItemDef def = null;
                if (data != null && !data.Items.TryGetValue(i.Id, out def)) problems.Add(w + ": não existe em items.json");
                if (def != null && string.IsNullOrEmpty(def.Slot)) problems.Add(w + ": não é equipável");
                if (!IsHex(i.Color)) problems.Add(w + ": color inválido");
                if (!string.IsNullOrEmpty(i.Glow) && !IsHex(i.Glow)) problems.Add(w + ": glow inválido");
                if (def != null && def.Slot == "weapon")
                {
                    if (!WeaponShapes.Contains(i.Shape)) problems.Add(w + ": shape de arma inválido " + i.Shape);
                    if (i.Socket != "hand_r" && i.Socket != "hand_l") problems.Add(w + ": socket de arma deve ser hand_r ou hand_l");
                }
                else if (def != null && !ArmorShapes.Contains(i.Shape)) problems.Add(w + ": shape de armadura inválido " + i.Shape);
            }
            return catalog;
        }

        public ClassLookDef ClassLook(string classId)
        {
            ClassLookDef c;
            return classId != null && _classes.TryGetValue(classId, out c) ? c : null;
        }

        /// <summary>Aparência do item: a do catálogo, ou uma padrão derivada do tipo de arma/slot (marcada como provisória pelo chamador).</summary>
        public ItemLookDef ItemLook(ItemDef item)
        {
            if (item == null) return null;
            ItemLookDef l;
            if (_items.TryGetValue(item.Id, out l)) return l;
            var d = new ItemLookDef { Id = item.Id, Color = "#9A9A9F", Socket = "hand_r" };
            switch (item.Slot)
            {
                case "weapon":
                    d.Shape = WeaponShapes.Contains(item.WeaponType) ? item.WeaponType : "stick";
                    d.Socket = item.WeaponType == "bow" ? "hand_l" : "hand_r";
                    break;
                case "head": d.Shape = "cap"; break;
                case "chest": d.Shape = "tunic"; break;
                case "legs": d.Shape = "pants"; break;
                case "feet": d.Shape = "boots"; break;
                default: d.Shape = "none"; break;
            }
            return d;
        }

        public bool HasCatalogEntry(string itemId) { return _items.ContainsKey(itemId); }

        public static bool IsHex(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length != 7 || s[0] != '#') return false;
            for (int i = 1; i < 7; i++)
                if (!Uri.IsHexDigit(s[i])) return false;
            return true;
        }
    }
}
