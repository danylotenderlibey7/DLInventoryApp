using System.ComponentModel.DataAnnotations;

namespace DLInventoryApp.Models
{
    public class ItemFieldValue
    {
        public Guid ItemId { get; set; }
        public Item Item { get; set; } = null!;
        public int CustomFieldId { get; set; }
        public CustomField CustomField { get; set; } = null!;
        public string? TextValue { get; set; }
        public decimal? NumberValue { get; set; }
        [MaxLength(2048)]
        public string? LinkValue { get; set; }
        public bool? BoolValue { get; set; }
    }
}
