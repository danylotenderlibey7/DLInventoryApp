namespace DLInventoryApp.Models
{
    public class CustomField
    {
        public int Id { get; set; }
        public Guid InventoryId { get; set; }
        public Inventory Inventory { get; set; } = null!;
        public string Name { get; set; } = null!;
        public CustomFieldType Type { get; set; }
        public int Order { get; set; }
        public bool IsRequired { get; set; }
        public bool IsUnique { get; set; }
        public List<ItemFieldValue> FieldValues { get; set; } = new();
    }
}
