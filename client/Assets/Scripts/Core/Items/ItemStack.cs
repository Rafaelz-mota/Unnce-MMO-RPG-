namespace Mmorpg.Core.Items
{
    public sealed class ItemStack
    {
        public string ItemId;
        public int Count;

        public ItemStack(string itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }

        public ItemStack Clone() { return new ItemStack(ItemId, Count); }

        public override string ToString() { return ItemId + " x" + Count; }
    }
}
