using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.ViewModels.CustomId;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Controllers
{
    [Authorize]
    [Route("Inventories/{inventoryId:guid}/CustomId")]
    public class CustomIdController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAccessService _accessService; 
        private readonly ICustomIdGenerator _customIdGenerator;
        public CustomIdController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, 
            IAccessService accessService, ICustomIdGenerator customIdGenerator)
        {
            _context = context;
            _userManager = userManager;
            _accessService = accessService;
            _customIdGenerator = customIdGenerator;
        }
        public async Task<IActionResult> Index(Guid inventoryId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canWrite = await _accessService.CanWriteInventory(inventoryId, userId);
            var invTitle = await _context.Inventories
                .Where(x => x.Id == inventoryId)
                .Select(x => x.Title)
                .SingleOrDefaultAsync();
            if (invTitle == null) return NotFound();
            var elements = await _context.CustomIdElements
                .Where(e => e.InventoryId == inventoryId)
                .OrderBy(e => e.Order)
                .Select(e => new CustomIdElementRowVm
                {
                    Id = e.Id,
                    Order = e.Order,
                    Type = e.Type,
                    Text = e.Text,
                    Format = e.Format
                }).ToListAsync();
            string preview = "";
            try
            {
                var result = await _customIdGenerator.PreviewAsync(inventoryId);
                preview = result.CustomId;
            }
            catch
            {
                preview = "(no template)";
            }
            var vm = new CustomIdIndexVm
            {
                InventoryId = inventoryId,
                InventoryTitle = invTitle,
                CanWrite = canWrite,
                Preview = preview,
                Elements = elements
            };
            return View(vm);
        }
        [HttpGet("Add")]
        public async Task<IActionResult> Add(Guid inventoryId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canWrite = await _accessService.CanWriteInventory(inventoryId, userId);
            if (!canWrite) return NotFound();
            var maxOrder = await _context.CustomIdElements
                .Where(e => e.InventoryId == inventoryId)
                .MaxAsync(e => (int?)e.Order) ?? 0;
            var vm = new UpsertCustomIdElementVm
            {
                InventoryId = inventoryId,
                Order = maxOrder + 1,
                Type = CustomIdElementType.FixedText
            };
            return View(vm);
        }
        [HttpPost("Add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(Guid inventoryId, UpsertCustomIdElementVm vm)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canWrite = await _accessService.CanWriteInventory(inventoryId, userId);
            if (!canWrite) return NotFound();
            if (inventoryId != vm.InventoryId) return NotFound();
            if (!ModelState.IsValid) return View(vm);
            var entity = new InventoryCustomIdElement
            {
                InventoryId = inventoryId,
                Order = vm.Order,
                Type = vm.Type,
                Text = vm.Text,
                Format = vm.Format
            };
            _context.CustomIdElements.Add(entity);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(nameof(vm.Order), "Order must be unique inside this inventory.");
                return View(vm);
            }
            return RedirectToAction(nameof(Index), new { inventoryId });
        }
        [HttpGet("{id:int}/Edit")]
        public async Task<IActionResult> Edit(Guid inventoryId, int id)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canWrite = await _accessService.CanWriteInventory(inventoryId, userId);
            if (!canWrite) return NotFound();
            var entity = await _context.CustomIdElements
                .Where(e => e.InventoryId == inventoryId && e.Id == id)
                .SingleOrDefaultAsync();
            if (entity == null) return NotFound();
            var vm = new UpsertCustomIdElementVm
            {
                InventoryId = inventoryId,
                Id = entity.Id,
                Order = entity.Order,
                Type = entity.Type,
                Text = entity.Text,
                Format = entity.Format
            };
            return View(vm);
        }

        [HttpPost("{id:int}/Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid inventoryId, int id, UpsertCustomIdElementVm vm)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canWrite = await _accessService.CanWriteInventory(inventoryId, userId);
            if (!canWrite) return NotFound();
            if (inventoryId != vm.InventoryId) return NotFound();
            if (vm.Id == null || vm.Id.Value != id) return NotFound();
            if (!ModelState.IsValid) return View(vm);
            var entity = await _context.CustomIdElements
                .Where(e => e.InventoryId == inventoryId && e.Id == id)
                .SingleOrDefaultAsync();
            if (entity == null) return NotFound();
            entity.Order = vm.Order;
            entity.Type = vm.Type;
            entity.Text = vm.Text;
            entity.Format = vm.Format;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(nameof(vm.Order), "Order must be unique inside this inventory.");
                return View(vm);
            }
            return RedirectToAction(nameof(Index), new { inventoryId });
        }
        [HttpPost("{id:int}/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid inventoryId, int id)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canWrite = await _accessService.CanWriteInventory(inventoryId, userId);
            if (!canWrite) return NotFound();
            var entity = await _context.CustomIdElements
                .Where(e => e.InventoryId == inventoryId && e.Id == id)
                .SingleOrDefaultAsync();
            if (entity == null) return NotFound();
            _context.CustomIdElements.Remove(entity);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { inventoryId });
        }
        [HttpPost("Reorder")]
        public async Task<IActionResult> Reorder(Guid inventoryId, [FromBody] List<int> orderedIds)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canWrite = await _accessService.CanWriteInventory(inventoryId, userId);
            if (!canWrite) return NotFound();
            var elements = await _context.CustomIdElements
                .Where(e => e.InventoryId == inventoryId)
                .ToListAsync();
            if (elements.Count != orderedIds.Count) return BadRequest();
            var elementIds = elements.Select(e => e.Id).OrderBy(x => x);
            var incomingIds = orderedIds.OrderBy(x => x);
            if (!elementIds.SequenceEqual(incomingIds)) return BadRequest();
            int temp = 1000;
            for (int i = 0; i < elements.Count(); i++) 
            {
                elements[i].Order = temp + i;
            }
            await _context.SaveChangesAsync();
            for (int i = 0; i < orderedIds.Count(); i++)
            {
                var element = elements.Single(e => e.Id == orderedIds[i]);
                element.Order = i + 1;
            }
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
