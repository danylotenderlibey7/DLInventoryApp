using System.ComponentModel.DataAnnotations;

namespace DLInventoryApp.Models
{
    public class Inventory
    {
        public Guid Id { get; set; }
        [Required]
        public string OwnerId { get; set; } = null!;
        public ApplicationUser Owner { get; set; } = null!;
        [Required]
        [MaxLength(250)]
        public string Title { get; set; } = null!;
        [MaxLength(1000)]
        public string Description { get; set; } = "";
        public bool IsPublic { get; set; } = false;
        public int? CategoryId { get; set; } = null!;
        public Category? Category { get; set; } = null!;
        [MaxLength(2048)]
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        [Timestamp]
        public byte[]? Version { get; set; }
        public List<Item> Items { get; set; } = new();
        public List<CustomField> CustomFields { get; set; } = new();
        public ICollection<InventoryWriteAccess> WriteAccesses { get; set; } = new List<InventoryWriteAccess>();
    }
}
