namespace DLInventoryApp.ViewModels.Discussions
{
    public class EditDiscussionPostVm
    {
        public Guid InventoryId { get; set; }
        public Guid PostId { get; set; }
        public string Text { get; set; } = null!;
    }
}
