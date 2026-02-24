using System.ComponentModel.DataAnnotations;

namespace DLInventoryApp.Models
{
    public class CustomField
    {
        public int Id { get; set; }
        public Guid InventoryId { get; set; }
        public Inventory Inventory { get; set; } = null!;
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = null!;
        public CustomFieldType Type { get; set; }
        [Range(0, int.MaxValue)]
        public int Order { get; set; }
        public bool IsRequired { get; set; }
        public bool IsUnique { get; set; }
        public List<ItemFieldValue> FieldValues { get; set; } = new();
    }
}
