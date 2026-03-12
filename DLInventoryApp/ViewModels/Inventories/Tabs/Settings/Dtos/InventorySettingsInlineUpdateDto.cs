namespace DLInventoryApp.ViewModels.Inventories.Tabs.Settings.Dtos
{
    public class InventorySettingsInlineUpdateDto
    {
        public string Field { get; set; } = "";
        public object? Value { get; set; }
        public uint Version { get; set; }
    }
}