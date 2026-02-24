using System.ComponentModel.DataAnnotations;

namespace DLInventoryApp.Models
{
    public class DiscussionPost
    {
        public Guid Id { get; set; }
        public Guid InventoryId { get; set; }
        public Inventory Inventory { get; set; } = null!;
        public string? AuthorId { get; set; }
        public ApplicationUser? Author { get; set; }
        [Required]
        [MaxLength(2048)]
        public string Text { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
