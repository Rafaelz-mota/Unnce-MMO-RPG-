using System;
using System.Collections.Generic;
using System.Linq;
using Mmorpg.Core.Common;
using Mmorpg.Core.Data;
using Mmorpg.Core.Items;
using Mmorpg.Core.Progression;
using Mmorpg.Core.Stats;

namespace Mmorpg.Core.Characters
{
    public enum LearnSkillResult
    {
        Ok,
        UnknownSkill,
        NotClassSkill,
        AlreadyLearned,
        FreeSkill,
        LevelTooLow,
        NotEnoughGold
    }

    /// <summary>
    /// Estado persistente de um personagem: classe, nível, XP, ouro, inventário, equipamentos e habilidades.
    /// Todas as mutações passam por métodos que validam as regras (é o que o servidor executará).
    /// </summary>
    public sealed class PlayerCharacter
    {
        private readonly GameData _data;
        private readonly Dictionary<string, string> _equipped = new Dictionary<string, string>();
        private readonly HashSet<string> _learned = new HashSet<string>();

        public string Name { get; private set; }
        public ClassDef Class { get; private set; }
        public int Level { get; private set; }
        /// <summary>XP acumulado dentro do nível atual.</summary>
        public long Xp { get; private set; }
        public long Gold { get; private set; }
        public Inventory Inventory { get; private set; }

        private PlayerCharacter(GameData data, string name, ClassDef cls)
        {
            _data = data;
            Name = name;
            Class = cls;
            Level = 1;
            Inventory = new Inventory(data, data.Rules.InventoryCapacity);
        }

        public static PlayerCharacter CreateNew(GameData data, string name, string classId)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 24) throw new ArgumentException("Nome inválido (1 a 24 caracteres).");
            ClassDef cls;
            if (!data.Classes.TryGetValue(classId, out cls)) throw new ArgumentException("Classe desconhecida: " + classId);

            var pc = new PlayerCharacter(data, name.Trim(), cls);
            foreach (string itemId in cls.StartingItems)
            {
                int slot = pc.Inventory.Add(itemId, 1) == 0 ? FirstSlotOf(pc.Inventory, itemId) : -1;
                if (slot >= 0) pc.Equip(slot); // se não puder equipar, fica no inventário
            }
            pc.SyncFreeSkills();
            return pc;
        }

        private static int FirstSlotOf(Inventory inv, string itemId)
        {
            for (int i = 0; i < inv.Capacity; i++)
                if (inv.Get(i) != null && inv.Get(i).ItemId == itemId) return i;
            return -1;
        }

        public GameData Data { get { return _data; } }

        public long XpToNext { get { return LevelCurve.XpToNext(_data, Level); } }

        public bool IsMaxLevel { get { return Level >= _data.Progression.MaxLevel; } }

        // ------------------------------------------------------------------ XP / ouro
        /// <summary>Soma XP e sobe de nível quantas vezes for preciso. Devolve quantos níveis subiu.</summary>
        public int AddXp(long amount)
        {
            if (amount <= 0 || IsMaxLevel) return 0;
            int gained = 0;
            Xp += amount;
            while (!IsMaxLevel && Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                gained++;
            }
            if (IsMaxLevel) Xp = 0;
            if (gained > 0) SyncFreeSkills();
            return gained;
        }

        /// <summary>Soma ouro respeitando o teto de rules.maxGold. Devolve o quanto realmente entrou.</summary>
        public long AddGold(long amount)
        {
            if (amount <= 0) return 0;
            long room = _data.Rules.MaxGold - Gold;
            long added = Math.Min(amount, Math.Max(0, room));
            Gold += added;
            return added;
        }

        public bool TrySpendGold(long amount)
        {
            if (amount < 0 || Gold < amount) return false;
            Gold -= amount;
            return true;
        }

        // ------------------------------------------------------------------ habilidades
        public bool IsSkillLearned(string skillId) { return _learned.Contains(skillId); }

        public IEnumerable<string> LearnedSkills { get { return _learned; } }

        /// <summary>Habilidades da classe que existem nos dados, na ordem dos slots.</summary>
        public IList<SkillDef> ClassSkills()
        {
            return Class.SkillIds.Select(id => _data.Skills[id]).OrderBy(s => s.Slot).ToList();
        }

        /// <summary>Aprende automaticamente as habilidades gratuitas (slots 1-5) cujo nível já foi atingido.</summary>
        public void SyncFreeSkills()
        {
            foreach (SkillDef s in ClassSkills())
                if (s.Slot <= _data.Rules.FreeSkillSlots && s.RequiresLevel <= Level) _learned.Add(s.Id);
        }

        /// <summary>Compra uma habilidade paga (slots 6-9) com Gold. O custo vem do JSON.</summary>
        public LearnSkillResult TryLearnSkill(string skillId)
        {
            SkillDef s;
            if (!_data.Skills.TryGetValue(skillId, out s)) return LearnSkillResult.UnknownSkill;
            if (s.OwnerType != SkillOwner.Class || s.OwnerId != Class.Id) return LearnSkillResult.NotClassSkill;
            if (_learned.Contains(skillId)) return LearnSkillResult.AlreadyLearned;
            if (s.Slot <= _data.Rules.FreeSkillSlots) return LearnSkillResult.FreeSkill;
            if (Level < s.RequiresLevel) return LearnSkillResult.LevelTooLow;
            if (!TrySpendGold(s.GoldCost)) return LearnSkillResult.NotEnoughGold;
            _learned.Add(skillId);
            return LearnSkillResult.Ok;
        }

        // ------------------------------------------------------------------ equipamentos
        public string EquippedItemId(string slot)
        {
            string id;
            return _equipped.TryGetValue(slot, out id) ? id : null;
        }

        public IEnumerable<KeyValuePair<string, string>> Equipped { get { return _equipped; } }

        public EquipResult Equip(int inventorySlot)
        {
            ItemStack stack = Inventory.Get(inventorySlot);
            if (stack == null) return EquipResult.EmptySlot;
            ItemDef def = _data.Items[stack.ItemId];
            EquipResult check = ItemRules.CanEquip(_data, Class, Level, def);
            if (check != EquipResult.Ok) return check;

            string previous = EquippedItemId(def.Slot);
            Inventory.TakeOneFromSlot(inventorySlot);
            if (previous != null && Inventory.Add(previous, 1) != 0)
            {
                Inventory.Add(def.Id, 1); // desfaz: não havia onde guardar o item antigo
                return EquipResult.InventoryFull;
            }
            _equipped[def.Slot] = def.Id;
            return EquipResult.Ok;
        }

        public EquipResult Unequip(string slot)
        {
            string id = EquippedItemId(slot);
            if (id == null) return EquipResult.EmptySlot;
            if (Inventory.Add(id, 1) != 0) return EquipResult.InventoryFull;
            _equipped.Remove(slot);
            return EquipResult.Ok;
        }

        public IList<ItemDef> EquippedItems()
        {
            return _equipped.Values.Select(id => _data.Items[id]).ToList();
        }

        /// <summary>Bônus de conjunto ativos: todas as faixas cujo número de peças já foi atingido.</summary>
        public IList<SetBonusDef> ActiveSetBonuses()
        {
            var result = new List<SetBonusDef>();
            foreach (var group in EquippedItems().Where(i => !string.IsNullOrEmpty(i.SetId)).GroupBy(i => i.SetId))
            {
                SetDef set;
                if (!_data.Sets.TryGetValue(group.Key, out set)) continue;
                int count = group.Count();
                result.AddRange(set.Bonuses.Where(b => b.Pieces <= count));
            }
            return result;
        }

        public StatSheet ComputeStats()
        {
            return StatCalculator.ComputePlayer(_data, Class, Level, EquippedItems(), ActiveSetBonuses());
        }

        // ------------------------------------------------------------------ persistência
        public CharacterSave ToSave()
        {
            var save = new CharacterSave { Name = Name, ClassId = Class.Id, Level = Level, Xp = Xp, Gold = Gold };
            for (int i = 0; i < Inventory.Capacity; i++)
            {
                ItemStack s = Inventory.Get(i);
                if (s != null) save.Inventory.Add(new ItemStackSave { Slot = i, ItemId = s.ItemId, Count = s.Count });
            }
            foreach (var kv in _equipped) save.Equipment[kv.Key] = kv.Value;
            save.LearnedSkills.AddRange(_learned.OrderBy(x => x, StringComparer.Ordinal));
            return save;
        }

        /// <summary>Reconstrói e VALIDA um save. Lança <see cref="SaveValidationException"/> listando todos os problemas.</summary>
        public static PlayerCharacter Restore(GameData data, CharacterSave save)
        {
            var problems = new List<string>();
            if (save == null) throw new SaveValidationException(new List<string> { "save nulo" });
            if (save.SchemaVersion != 1) problems.Add("schemaVersion " + save.SchemaVersion + " não suportado");
            if (string.IsNullOrWhiteSpace(save.Name) || save.Name.Length > 24) problems.Add("nome inválido");
            ClassDef cls = null;
            if (save.ClassId == null || !data.Classes.TryGetValue(save.ClassId, out cls)) problems.Add("classe desconhecida: " + save.ClassId);
            if (save.Level < 1 || save.Level > data.Rules.MaxLevel) problems.Add("nível fora de 1.." + data.Rules.MaxLevel);
            if (save.Gold < 0 || save.Gold > data.Rules.MaxGold) problems.Add("ouro fora de 0.." + data.Rules.MaxGold);
            if (problems.Count > 0) throw new SaveValidationException(problems);

            if (save.Xp < 0 || (save.Level < data.Progression.MaxLevel && save.Xp >= LevelCurve.XpToNext(data, save.Level))
                || (save.Level == data.Progression.MaxLevel && save.Xp != 0))
                problems.Add("xp incompatível com o nível");

            var pc = new PlayerCharacter(data, save.Name.Trim(), cls);
            pc.Level = save.Level;
            pc.Xp = save.Xp;
            pc.Gold = save.Gold;

            foreach (ItemStackSave s in save.Inventory)
            {
                if (s == null || s.ItemId == null || !data.Items.ContainsKey(s.ItemId)) { problems.Add("item desconhecido no inventário"); continue; }
                if (!pc.Inventory.TrySetSlot(s.Slot, s.ItemId, s.Count))
                    problems.Add("slot de inventário inválido/duplicado ou contagem inválida: " + s.ItemId + " slot " + s.Slot + " x" + s.Count);
            }

            foreach (var kv in save.Equipment)
            {
                ItemDef item;
                if (!data.Items.TryGetValue(kv.Value ?? "", out item)) { problems.Add("item desconhecido equipado: " + kv.Value); continue; }
                if (item.Slot != kv.Key) { problems.Add(kv.Value + " não pertence ao slot " + kv.Key); continue; }
                EquipResult r = ItemRules.CanEquip(data, cls, save.Level, item);
                if (r != EquipResult.Ok) { problems.Add(kv.Value + " não pode ser equipado: " + r); continue; }
                pc._equipped[kv.Key] = kv.Value;
            }

            foreach (string skillId in save.LearnedSkills)
            {
                SkillDef sk;
                if (!data.Skills.TryGetValue(skillId, out sk) || sk.OwnerType != SkillOwner.Class || sk.OwnerId != cls.Id)
                    problems.Add("habilidade inválida para a classe: " + skillId);
                else if (sk.RequiresLevel > save.Level)
                    problems.Add("habilidade acima do nível: " + skillId);
                else pc._learned.Add(skillId);
            }

            if (problems.Count > 0) throw new SaveValidationException(problems);
            pc.SyncFreeSkills();
            return pc;
        }
    }
}
