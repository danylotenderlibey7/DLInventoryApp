using DLInventoryApp.Models;

namespace DLInventoryApp.ViewModels.Discussions
{
    public class DiscussionPostVm
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = null!;
        public string Html { get; set; } = "";
        public string? AuthorId { get; set; }
        public string AuthorName { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }
}
