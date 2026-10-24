using System;
using Mmorpg.Core.Data;

namespace Mmorpg.Core.Items
{
    /// <summary>
    /// Inventário de capacidade fixa com empilhamento. Todas as operações são atômicas
    /// (ou fazem tudo, ou não alteram nada), para que um servidor possa validar e aplicar com segurança.
    /// </summary>
    public sealed class Inventory
    {
        private readonly GameData _data;
        private readonly ItemStack[] _slots;

        public Inventory(GameData data, int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException("capacity");
            _data = data;
            _slots = new ItemStack[capacity];
        }

        public int Capacity { get { return _slots.Length; } }

        public ItemStack Get(int slot)
        {
            if (slot < 0 || slot >= _slots.Length) return null;
            return _slots[slot];
        }

        public int FreeSlots
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _slots.Length; i++) if (_slots[i] == null) n++;
                return n;
            }
        }

        public int CountOf(string itemId)
        {
            int n = 0;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] != null && _slots[i].ItemId == itemId) n += _slots[i].Count;
            return n;
        }

        private int MaxStack(string itemId)
        {
            ItemDef def;
            if (!_data.Items.TryGetValue(itemId, out def)) throw new ArgumentException("Item desconhecido: " + itemId);
            return Math.Max(1, def.MaxStack);
        }

        /// <summary>True se todos os 'count' itens couberem.</summary>
        public bool CanAdd(string itemId, int count)
        {
            if (count <= 0) return true;
            int max = MaxStack(itemId);
            long room = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null) room += max;
                else if (_slots[i].ItemId == itemId) room += Math.Max(0, max - _slots[i].Count);
            }
            return room >= count;
        }

        /// <summary>Adiciona o que couber e devolve a sobra (0 = tudo entrou).</summary>
        public int Add(string itemId, int count)
        {
            if (count <= 0) return 0;
            int max = MaxStack(itemId);
            int remaining = count;
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                ItemStack s = _slots[i];
                if (s != null && s.ItemId == itemId && s.Count < max)
                {
                    int take = Math.Min(max - s.Count, remaining);
                    s.Count += take;
                    remaining -= take;
                }
            }
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (_slots[i] == null)
                {
                    int take = Math.Min(max, remaining);
                    _slots[i] = new ItemStack(itemId, take);
                    remaining -= take;
                }
            }
            return remaining;
        }

        /// <summary>Remove exatamente 'count' itens ou nada (false se não houver o suficiente).</summary>
        public bool Remove(string itemId, int count)
        {
            if (count <= 0) return true;
            if (CountOf(itemId) < count) return false;
            int remaining = count;
            for (int i = _slots.Length - 1; i >= 0 && remaining > 0; i--)
            {
                ItemStack s = _slots[i];
                if (s == null || s.ItemId != itemId) continue;
                int take = Math.Min(s.Count, remaining);
                s.Count -= take;
                remaining -= take;
                if (s.Count == 0) _slots[i] = null;
            }
            return true;
        }

        /// <summary>Tira uma unidade de um slot específico (usado ao equipar). Devolve o id ou null.</summary>
        public string TakeOneFromSlot(int slot)
        {
            ItemStack s = Get(slot);
            if (s == null) return null;
            string id = s.ItemId;
            s.Count -= 1;
            if (s.Count <= 0) _slots[slot] = null;
            return id;
        }

        /// <summary>Coloca diretamente em um slot vazio (restauração de save). Falha se ocupado ou inválido.</summary>
        public bool TrySetSlot(int slot, string itemId, int count)
        {
            if (slot < 0 || slot >= _slots.Length || _slots[slot] != null) return false;
            ItemDef def;
            if (!_data.Items.TryGetValue(itemId, out def)) return false;
            if (count < 1 || count > Math.Max(1, def.MaxStack)) return false;
            _slots[slot] = new ItemStack(itemId, count);
            return true;
        }
    }
}
