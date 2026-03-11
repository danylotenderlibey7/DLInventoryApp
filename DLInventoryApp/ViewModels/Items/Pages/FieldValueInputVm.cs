using DLInventoryApp.Models;

namespace DLInventoryApp.ViewModels.Items.Pages
{
    public class FieldValueInputVm
    {
        public int CustomFieldId { get; set; }
        public string Name { get; set; } = null!; 
        public string? Description { get; set; }
        public CustomFieldType Type { get; set; }
        public string? TextValue { get; set; }
        public decimal? NumberValue { get; set; }
        public string? LinkValue { get; set; }
        public bool BoolValue { get; set; }
    }
}
