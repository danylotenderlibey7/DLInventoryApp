using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.ViewModels.Items;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Controllers
{
    [Authorize]
    [Route("Inventories/{inventoryId:guid}/Items")]
    public class ItemsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICustomIdGenerator _customIdGenerator;
        public ItemsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ICustomIdGenerator customIdGenerator)
        {
            _context = context;
            _userManager = userManager;
            _customIdGenerator = customIdGenerator;
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
            var ids = await _context.Items
                .Where(it => it.InventoryId == inventoryId)
                .Select(it => it.CustomId)
                .ToListAsync();
            var customId = _customIdGenerator.Generate(inv.Title, ids);
            var vm = new CreateItemVm
            {
                InventoryId = inventoryId,
                InventoryTitle = inv.Title,
                CustomId = customId
            };
            return View(vm);
        }
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Guid inventoryId, CreateItemVm vm)
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
            var item = new Item
            {
                Id = Guid.NewGuid(),
                InventoryId = inventoryId,
                CustomId = vm.CustomId,
                CreatedById = userId,
                CreatedAt = DateTime.UtcNow
            };
            _context.Items.Add(item);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                vm.InventoryTitle = inv.Title;
                ModelState.AddModelError(nameof(vm.CustomId), "Custom ID already exists in this inventory.");
                return View(vm);
            }
            return RedirectToAction("Items", "Inventories", new { id = inventoryId });
        }

    }
}
