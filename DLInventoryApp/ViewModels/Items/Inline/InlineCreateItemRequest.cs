namespace DLInventoryApp.ViewModels.Items.Inline
{
    public class InlineCreateItemRequest
    {
        public string? CustomId { get; set; }

        public List<InlineFieldValue> Fields { get; set; } = new();
    }
}