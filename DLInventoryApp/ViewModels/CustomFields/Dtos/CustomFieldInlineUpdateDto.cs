namespace DLInventoryApp.ViewModels.CustomFields.Dtos
{
    public class CustomFieldInlineUpdateDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int Type { get; set; }
        public bool ShowInTable { get; set; }
        public uint Version { get; set; }
    }
}
