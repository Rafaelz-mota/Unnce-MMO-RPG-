using Mmorpg.Core.Data;

namespace Mmorpg.Core.Items
{
    public enum EquipResult
    {
        Ok,
        EmptySlot,
        NotEquipment,
        UnknownSlot,
        LevelTooLow,
        ClassNotAllowed,
        WeaponTypeNotAllowed,
        InventoryFull
    }

    public static class ItemRules
    {
        /// <summary>Verifica se uma classe/nível pode equipar o item (as regras vêm de items.json e classes.json).</summary>
        public static EquipResult CanEquip(GameData d, ClassDef cls, int level, ItemDef item)
        {
            if (item.Type != ItemType.Equipment || string.IsNullOrEmpty(item.Slot)) return EquipResult.NotEquipment;
            if (!d.Rules.EquipmentSlots.Contains(item.Slot)) return EquipResult.UnknownSlot;
            if (level < item.RequiredLevel) return EquipResult.LevelTooLow;
            if (item.AllowedClasses.Count > 0 && !item.AllowedClasses.Contains(cls.Id)) return EquipResult.ClassNotAllowed;
            if (item.Slot == "weapon" && !string.IsNullOrEmpty(item.WeaponType) && !cls.WeaponTypes.Contains(item.WeaponType))
                return EquipResult.WeaponTypeNotAllowed;
            return EquipResult.Ok;
        }
    }
}
