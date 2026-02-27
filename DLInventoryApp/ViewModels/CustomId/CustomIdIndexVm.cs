namespace DLInventoryApp.ViewModels.CustomId
{
    public class CustomIdIndexVm
    {
        public Guid InventoryId { get; set; }
        public string InventoryTitle { get; set; } = "";
        public bool CanWrite { get; set; }
        public string Preview { get; set; } = "";
        public List<CustomIdElementRowVm> Elements { get; set; } = new();
    }
}
