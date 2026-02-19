using DLInventoryApp.Models;

namespace DLInventoryApp.ViewModels.Items
{
    public class FieldValueInputVm
    {
        public int CustomFieldId { get; set; }
        public string Name { get; set; } = null!;
        public CustomFieldType Type { get; set; }
        public string? TextValue { get; set; }
        public decimal? NumberValue { get; set; }
        public DateTime? DateValue { get; set; }
        public bool BoolValue { get; set; }
        public bool IsRequired { get; set; }
    }
}
