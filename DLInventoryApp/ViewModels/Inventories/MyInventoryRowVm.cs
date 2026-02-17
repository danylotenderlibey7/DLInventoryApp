namespace DLInventoryApp.ViewModels.Inventories
{
    public class MyInventoryRowVm
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public string? CategoryName { get; set; }
        public bool IsPublic { get; set; }
        public int ItemsCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
