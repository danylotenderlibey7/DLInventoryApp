namespace DLInventoryApp.ViewModels.Discussions
{
    public class DiscussionIndexVm
    {
        public Guid InventoryId { get; set; }
        public List<DiscussionPostVm> Posts { get; set; } = new();
    }
}
