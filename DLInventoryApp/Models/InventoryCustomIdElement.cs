using System.ComponentModel.DataAnnotations;

namespace DLInventoryApp.Models
{
    public class InventoryCustomIdElement
    {
        public int Id { get; set; }
        public Guid InventoryId { get; set; }
        public Inventory Inventory { get; set; } = null!;
        public int Order { get; set; }
        public CustomIdElementType Type { get; set; }
        [MaxLength(200)]
        public string? Text { get; set; }
        [MaxLength(50)]
        public string? Format { get; set; }
    }
}
