namespace DLInventoryApp.ViewModels.Items.Inline
{
    public class InlineFieldValue
    {
        public int CustomFieldId { get; set; }

        public string? TextValue { get; set; }
        public decimal? NumberValue { get; set; }
        public string? LinkValue { get; set; }
        public bool? BoolValue { get; set; }
    }
}