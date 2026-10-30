using System;
using Mmorpg.Core.Characters;
using Mmorpg.Core.Data;

namespace Mmorpg.Core.Shop
{
    public enum ShopResult
    {
        Ok,
        UnknownShop,
        NotInStock,
        InvalidCount,
        NotEnoughGold,
        InventoryFull,
        EmptySlot,
        NotTradable,
        GoldFull
    }

    /// <summary>
    /// Compra e venda. Toda a decisão (preço, ouro, espaço, itens vendáveis) é tomada aqui a partir dos dados,
    /// nunca pelo cliente: é o código que o servidor autoritativo executará. As operações são atômicas
    /// (ou acontecem por inteiro ou nada muda).
    /// </summary>
    public static class ShopService
    {
        public static ShopResult Buy(GameData data, PlayerCharacter pc, string shopId, string itemId, int count, out long spent)
        {
            spent = 0;
            ShopDef shop;
            if (data == null || pc == null || shopId == null || !data.Shops.TryGetValue(shopId, out shop)) return ShopResult.UnknownShop;
            if (count < 1 || count > 999) return ShopResult.InvalidCount;
            ShopEntryDef entry = null;
            for (int i = 0; i < shop.Stock.Count; i++)
                if (shop.Stock[i].ItemId == itemId) { entry = shop.Stock[i]; break; }
            if (entry == null) return ShopResult.NotInStock;

            long total;
            try { total = checked(entry.Price * count); }
            catch (OverflowException) { return ShopResult.InvalidCount; }
            if (pc.Gold < total) return ShopResult.NotEnoughGold;
            if (!pc.Inventory.CanAdd(itemId, count)) return ShopResult.InventoryFull;

            if (!pc.TrySpendGold(total)) return ShopResult.NotEnoughGold;
            pc.Inventory.Add(itemId, count);
            spent = total;
            return ShopResult.Ok;
        }

        /// <summary>Vende toda a pilha ou 'count' unidades do slot do inventário pelo sellValue do item.</summary>
        public static ShopResult Sell(GameData data, PlayerCharacter pc, int inventorySlot, int count, out long earned)
        {
            earned = 0;
            if (data == null || pc == null) return ShopResult.EmptySlot;
            var stack = pc.Inventory.Get(inventorySlot);
            if (stack == null) return ShopResult.EmptySlot;
            if (count < 1 || count > stack.Count) return ShopResult.InvalidCount;
            ItemDef def = data.Items[stack.ItemId];
            if (!def.Tradable || def.SellValue <= 0) return ShopResult.NotTradable;

            long total = def.SellValue * count;
            if (pc.Gold + total > data.Rules.MaxGold) return ShopResult.GoldFull;
            string id = stack.ItemId;
            for (int i = 0; i < count; i++) pc.Inventory.TakeOneFromSlot(inventorySlot);
            pc.AddGold(total);
            earned = total;
            return ShopResult.Ok;
        }

        public static string Describe(ShopResult r)
        {
            switch (r)
            {
                case ShopResult.Ok: return "Negócio fechado.";
                case ShopResult.UnknownShop: return "Loja desconhecida.";
                case ShopResult.NotInStock: return "A loja não vende esse item.";
                case ShopResult.InvalidCount: return "Quantidade inválida.";
                case ShopResult.NotEnoughGold: return "Ouro insuficiente.";
                case ShopResult.InventoryFull: return "Inventário cheio.";
                case ShopResult.EmptySlot: return "Não há item nesse espaço.";
                case ShopResult.NotTradable: return "Esse item não pode ser vendido.";
                case ShopResult.GoldFull: return "Você não pode carregar tanto ouro.";
                default: return r.ToString();
            }
        }
    }
}
