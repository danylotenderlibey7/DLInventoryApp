using DLInventoryApp.Models;

namespace DLInventoryApp.ViewModels.CustomFields
{
    public class CustomFieldColumnVm
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public CustomFieldType Type { get; set; }
        public int Order { get; set; }
        public bool IsRequired { get; set; }
        public bool IsUnique { get; set; }
    }
}
