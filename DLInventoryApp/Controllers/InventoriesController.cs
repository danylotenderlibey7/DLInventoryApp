using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.ViewModels.Inventories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Controllers
{
    [Authorize]
    public class InventoriesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        public InventoriesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }
        public async Task<IActionResult> My()
        {
            var userId = _userManager.GetUserId(User);
            var list = await _context.Inventories
                .Where(inv => inv.OwnerId == userId)
                .Select(inv => new MyInventoryRowVm
                {
                    Id = inv.Id,
                    Title = inv.Title,
                    CreatedAt = inv.CreatedAt,
                    UpdatedAt = inv.UpdatedAt,
                    ItemsCount = inv.Items.Count(),
                    CategoryName = inv.Category != null ? inv.Category.Name : null
                })
                .OrderByDescending(vm => vm.UpdatedAt ?? vm.CreatedAt)
                .ToListAsync();

            return View(list);
        }
    }
}
