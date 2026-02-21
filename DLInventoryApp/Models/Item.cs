using System.ComponentModel.DataAnnotations;

namespace DLInventoryApp.Models
{
    public class Item
    {
        public Guid Id { get; set; }
        public Guid InventoryId { get; set; }
        public Inventory Inventory { get; set; } = null!;
        [Required]
        [MaxLength(20)]
        public string CustomId { get; set; } = null!;
        [Required]
        public string CreatedById { get; set; } = null!;
        public ApplicationUser CreatedBy { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        [Timestamp]
        public byte[]? Version { get; set; }
        public List<ItemFieldValue> FieldValues { get; set; } = new();
        public List<ItemLike> Likes { get; set; } = new();
    }
}
