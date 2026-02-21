namespace DLInventoryApp.Models
{
    public class ItemLike
    {
        public Guid ItemId { get; set; }
        public Item Item { get; set; } = null!;
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
