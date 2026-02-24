using DLInventoryApp.Data;
using DLInventoryApp.Hubs;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.ViewModels.Discussions;
using Humanizer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Controllers
{
    [Authorize]
    [Route("Inventories/{inventoryId:guid}/Discussion")]
    public class DiscussionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<DiscussionHub> _hub;
        private readonly IMarkdownService _markdown;
        public DiscussionController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, 
            IHubContext<DiscussionHub> hub, IMarkdownService markdown)
        {
            _context = context;
            _userManager = userManager;
            _hub = hub;
            _markdown = markdown;
        }
        [AllowAnonymous]
        public async Task<IActionResult> Index(Guid inventoryId)
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.Identity?.IsAuthenticated == true && User.IsInRole("Admin");
            string? ownerId = null;
            if (userId != null)
            {
                ownerId = await _context.Inventories
                    .Where(i => i.Id == inventoryId)
                    .Select(i => i.OwnerId)
                    .SingleOrDefaultAsync();
                if (ownerId == null) return NotFound();
            }
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
                    })
                    .ToListAsync();
            foreach (var p in posts)
            {
                p.Html = _markdown.ToSafeHtml(p.Text);
                p.CanEdit = userId != null && p.AuthorId == userId;
                p.CanDelete = (userId != null) && (isAdmin || p.AuthorId == userId || ownerId == userId);
            }
            var vm = new DiscussionIndexVm
            {
                InventoryId = inventoryId,
                Posts = posts
            };
            return View(vm);
        }
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Guid inventoryId, string text)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();
            var invExists = await _context.Inventories.AnyAsync(i => i.Id == inventoryId);
            if (!invExists) return NotFound();
            if (string.IsNullOrWhiteSpace(text))
                return BadRequest(new { error = "Text is required." });
            var post = new DiscussionPost
            {
                Id = Guid.NewGuid(),
                InventoryId = inventoryId,
                AuthorId = userId,
                Text = text,
                CreatedAt = DateTime.UtcNow
            };
            _context.DiscussionPosts.Add(post);
            await _context.SaveChangesAsync();
            var authorName = User.Identity?.Name ?? "User";
            var dto = new DiscussionPostDto
            {
                Id = post.Id,
                Text = post.Text,
                Html = _markdown.ToSafeHtml(post.Text),
                AuthorId = post.AuthorId,
                AuthorName = User.Identity?.Name ?? "User",
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt
            };
            await _hub.Clients.Group($"inventory-{inventoryId}").SendAsync("PostAdded", dto);
            return Ok(dto);
        }
        [HttpPost("Delete/{postId:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid inventoryId, Guid postId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var post = await _context.DiscussionPosts
                .Where(p => p.InventoryId == inventoryId && p.Id == postId)
                .Select(p => new { p.Id, p.AuthorId })
                .SingleOrDefaultAsync();
            if (post == null) return NotFound();
            var ownerId = await _context.Inventories
                .Where(i => i.Id == inventoryId)
                .Select(i => i.OwnerId)
                .SingleOrDefaultAsync();
            if (ownerId == null) return NotFound();
            var isAdmin = User.IsInRole("Admin");
            var canDelete = isAdmin || ownerId == userId || (post.AuthorId != null && post.AuthorId == userId);
            if (!canDelete) return NotFound();
            _context.DiscussionPosts.Remove(new DiscussionPost { Id = postId });
            await _context.SaveChangesAsync();
            await _hub.Clients.Group($"inventory-{inventoryId}")
                .SendAsync("PostDeleted", postId);
            return Ok(new { ok = true });
        }
        [HttpGet("Edit/{postId:guid}")]
        public async Task<IActionResult> Edit(Guid inventoryId, Guid postId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var post = await _context.DiscussionPosts
                .Where(p => p.InventoryId == inventoryId && p.Id == postId)
                .Select(p => new { p.Id, p.InventoryId, p.AuthorId, p.Text })
                .SingleOrDefaultAsync();
            if (post == null) return NotFound();
            if (post.AuthorId != userId) return NotFound();
            var vm = new EditDiscussionPostVm
            {
                InventoryId = post.InventoryId,
                PostId = post.Id,
                Text = post.Text
            };
            return View(vm);
        }
        [HttpPost("Edit/{postId:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid inventoryId, Guid postId, string text)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();
            if (string.IsNullOrWhiteSpace(text)) return BadRequest(new { error = "Text is required." });
            var post = await _context.DiscussionPosts
                .Where(p => p.InventoryId == inventoryId && p.Id == postId)
                .SingleOrDefaultAsync();
            if (post == null) return NotFound();
            if (post.AuthorId != userId) return NotFound();
            post.Text = text;
            post.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            var dto = new DiscussionPostDto
            {
                Id = post.Id,
                Text = post.Text,
                Html = _markdown.ToSafeHtml(post.Text),
                AuthorId = post.AuthorId,
                AuthorName = User.Identity?.Name ?? "User",
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt
            };
            await _hub.Clients.Group($"inventory-{inventoryId}").SendAsync("PostEdited", dto);
            return Ok(dto);
        }
    }
}