namespace DLInventoryApp.ViewModels.Items.Inline
{
    public sealed class SetLikesRequest
    {
        public List<Guid> ItemIds { get; set; } = new();
        public bool Like { get; set; }
    }
}
