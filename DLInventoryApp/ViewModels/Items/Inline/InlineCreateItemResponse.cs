namespace DLInventoryApp.ViewModels.Items.Inline
{
    public class InlineCreateItemResponse
    {
        public Guid ItemId { get; set; }
        public string CustomId { get; set; } = "";
        public string CreatedAt { get; set; } = "";
        public string UpdatedAt { get; set; } = "";
        public List<string?> Cells { get; set; } = new();
        public int LikesCount { get; set; }
        public bool IsLikedByMe { get; set; }
    }
}