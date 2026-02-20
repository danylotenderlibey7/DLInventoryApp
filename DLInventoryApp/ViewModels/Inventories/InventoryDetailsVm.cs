namespace DLInventoryApp.ViewModels.Inventories
{
    public class InventoryDetailsVm
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string? CategoryName { get; set; }
        public bool IsPublic { get; set; }
        public int ItemsCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsOwner { get; set; }
        public List<string> Tags { get; set; } = new();
    }
}
