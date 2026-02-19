using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.ViewModels.Inventories;
using DLInventoryApp.ViewModels.Items;
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
        public IActionResult Create()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateInventoryVm vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }
            var userId = _userManager.GetUserId(User);
            var entity = new Inventory
            {
                Title = vm.Title,
                Description = vm.Description,
                IsPublic = vm.IsPublic,
                CategoryId = vm.CategoryId,
                OwnerId = userId,
                CreatedAt = DateTime.UtcNow
            };
            _context.Inventories.Add(entity);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(My));
        }
        [Authorize]
        public async Task<IActionResult> Details(Guid id)
        {
            var userId = _userManager.GetUserId(User);
            var vm = await _context.Inventories
                .Where(inv => inv.Id == id && inv.OwnerId == userId)
                .Select(inv => new InventoryDetailsVm
                {
                    Id = inv.Id,
                    Title = inv.Title,
                    Description = inv.Description,
                    IsPublic = inv.IsPublic,
                    CategoryName = inv.Category != null ? inv.Category.Name : null,
                    ItemsCount = inv.Items.Count(),
                    CreatedAt = inv.CreatedAt,
                    UpdatedAt = inv.UpdatedAt
                }).SingleOrDefaultAsync();
            if (vm == null)
                return NotFound();
            return View(vm);
        }
    }
}
