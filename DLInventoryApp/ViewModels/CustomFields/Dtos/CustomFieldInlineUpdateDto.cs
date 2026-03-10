namespace DLInventoryApp.ViewModels.CustomFields.Dtos
{
    public class CustomFieldInlineUpdateDto
    {
        public string? Name { get; set; }
        public int Type { get; set; }
        public bool IsRequired { get; set; }
        public bool IsUnique { get; set; }
    }
}
