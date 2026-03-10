using DLInventoryApp.Data;
using DLInventoryApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        public IndexModel(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }
        public string Username { get; set; }
        public string Email { get; set; }
        public string UserInitial { get; set; }
        public bool IsBlocked { get; set; }
        public int InventoriesCount { get; set; }
        public int ItemsCount { get; set; }
        public List<MyInventoryVm> Inventories { get; set; } = new();
        public class MyInventoryVm
        {
            public Guid Id { get; set; }
            public string Title { get; set; } = null!;
            public string CategoryName { get; set; } = null!;
            public bool IsPublic { get; set; }
            public int ItemsCount { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            Username = user.UserName ?? "User";
            Email = user.Email ?? "No email";
            IsBlocked = user.IsBlocked;
            UserInitial = !string.IsNullOrWhiteSpace(Username) ? Username.Trim()[0].ToString().ToUpper() : "U";
            var userId = user.Id;
            Inventories = await _context.Inventories
                .AsNoTracking()
                .Where(i => i.OwnerId == userId)
                .OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt)
                .Select(i => new MyInventoryVm
                {
                    Id = i.Id,
                    Title = i.Title,
                    CategoryName = i.Category != null ? i.Category.Name : "-",
                    IsPublic = i.IsPublic,
                    ItemsCount = i.Items.Count,
                    CreatedAt = i.CreatedAt,
                    UpdatedAt = i.UpdatedAt
                }).ToListAsync();
            InventoriesCount = Inventories.Count;
            ItemsCount = Inventories.Sum(i => i.ItemsCount);
            return Page();
        }
    }
}