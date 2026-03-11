namespace DLInventoryApp.ViewModels.Items.Tabs
{
    public class InventoryItemRowVm
    {
        public Guid Id { get; set; }
        public string CustomId { get; set; } = null!;
        public string CreatedByName { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<string?> Cells { get; set; } = new(); 
        public int LikesCount { get; set; }
        public bool IsLikedByMe { get; set; }
        public byte[]? Version { get; set; }
    }
}
