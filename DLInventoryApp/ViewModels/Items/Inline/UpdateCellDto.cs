namespace DLInventoryApp.ViewModels.Items.Inline
{
    public class UpdateCellDto
    {
        public int CustomFieldId { get; set; }
        public object? Value { get; set; }
        public byte[] Version { get; set; } = Array.Empty<byte>();
    }
}
