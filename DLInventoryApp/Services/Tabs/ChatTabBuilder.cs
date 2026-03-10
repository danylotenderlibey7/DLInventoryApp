using DLInventoryApp.Data;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.ViewModels.Discussions;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Services.Tabs
{
    public class ChatTabBuilder
    {
        private readonly ApplicationDbContext _context;
        private readonly IMarkdownService _markdown;
        public ChatTabBuilder(ApplicationDbContext context, IMarkdownService markdown)
        {
            _context = context;
            _markdown = markdown;
        }
        public async Task<DiscussionIndexVm> BuildAsync(Guid inventoryId, string? currentUserId, bool isAdmin, string inventoryOwnerId)
        {
            var posts = await _context.DiscussionPosts
                .Where(p => p.InventoryId == inventoryId)
                .OrderBy(p => p.CreatedAt)
                .Select(p => new DiscussionPostVm
                {
                    Id = p.Id,
                    Text = p.Text,
                    AuthorId = p.AuthorId,
                    AuthorName = p.Author != null ? p.Author.UserName! : "Deleted User",
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                }).ToListAsync();
            foreach (var p in posts)
            {
                p.Html = _markdown.ToSafeHtml(p.Text);
                p.CanEdit = currentUserId != null && p.AuthorId == currentUserId;
                p.CanDelete = currentUserId != null && (isAdmin || p.AuthorId == currentUserId || inventoryOwnerId == currentUserId);
            }
            return new DiscussionIndexVm
            {
                InventoryId = inventoryId,
                Posts = posts
            };
        }
    }
}