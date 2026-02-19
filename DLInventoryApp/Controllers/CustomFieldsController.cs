using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.ViewModels.CustomFields;
using DLInventoryApp.ViewModels.Items;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Controllers
{
    [Authorize]
    [Route("Inventories/{inventoryId:guid}/Fields")]
    public class CustomFieldsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        public CustomFieldsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }
        [HttpGet("")]
        public async Task<IActionResult> Index(Guid inventoryId)
        {
            var userId = _userManager.GetUserId(User);
            var title = await _context.Inventories
                .Where(inv => inv.Id == inventoryId && inv.OwnerId == userId)
                .Select(inv => inv.Title)
                .SingleOrDefaultAsync();
            if (title == null)
                return NotFound();
            var fields = await _context.CustomFields
                .Where(f => f.InventoryId == inventoryId)
                .Select(f => new CustomFieldColumnVm
                {
                    Id = f.Id,
                    Name = f.Name,
                    Type = f.Type,
                    Order = f.Order
                })
                .OrderBy(f=>f.Order)
                .ToListAsync();
            var vm = new InventoryFieldsVm
            {
                InventoryId = inventoryId,
                InventoryTitle = title,
                Fields = fields
            };
            return View(vm);
        }
        [HttpGet("Create")]
        public async Task<IActionResult> Create(Guid inventoryId)
        {
            var userId = _userManager.GetUserId(User);
            var inv = await _context.Inventories
                .Where(inv => inv.Id == inventoryId)
                .SingleOrDefaultAsync();
            if (inv == null)
                return NotFound();
            if (inv.OwnerId != userId)
                return NotFound();
            var vm = new CreateCustomFieldVm
            {
                InventoryId = inventoryId,
                InventoryTitle = inv.Title
            };
            return View(vm);
        }
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Guid inventoryId, CreateCustomFieldVm vm)
        {
            if (inventoryId != vm.InventoryId)
                return NotFound();
            if (!ModelState.IsValid)
                return View(vm);
            var userId = _userManager.GetUserId(User);
            var inv = await _context.Inventories
                .Where(x => x.Id == inventoryId)
                .SingleOrDefaultAsync();
            if (inv == null)
                return NotFound();
            if (inv.OwnerId != userId)
                return NotFound();
            vm.InventoryTitle = inv.Title;
            var sameTypeCount = await _context.CustomFields
                .Where(f => f.InventoryId == inventoryId && f.Type == vm.Type)
                .CountAsync();
            if (sameTypeCount >= 3)
            {
                ModelState.AddModelError(nameof(vm.Type), "You can create up to 3 fields of this type in one inventory.");
                return View(vm);
            }
            var maxOrderOrNull = await _context.CustomFields
                .Where(f => f.InventoryId == inventoryId)
                .MaxAsync(f => (int?)f.Order);
            var nextOrder = (maxOrderOrNull ?? -1) + 1;
            var field = new CustomField
            {
                InventoryId = inventoryId,
                Name = (vm.Name ?? string.Empty).Trim(),
                Type = vm.Type,
                Order = nextOrder,
                IsRequired = vm.IsRequired,
                IsUnique = vm.IsUnique
            };
            _context.CustomFields.Add(field);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(nameof(vm.Name), "Field name already exists in this inventory.");
                return View(vm);
            }
            return RedirectToAction("Index", new { inventoryId });
        }
    }
}
