namespace DLInventoryApp.ViewModels.Discussions
{
    public class DiscussionPostDto
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = null!; 
        public string Html { get; set; } = null!;
        public string AuthorName { get; set; } = null!;
        public string? AuthorId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
