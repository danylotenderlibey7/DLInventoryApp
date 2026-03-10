using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.ViewModels.CustomFields;
using DLInventoryApp.ViewModels.CustomFields.Dtos;
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
        private readonly IAccessService _accessService;
        private readonly ISearchService _searchService;
        public CustomFieldsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, 
            IAccessService accessService, ISearchService searchService)
        {
            _context = context;
            _userManager = userManager;
            _accessService = accessService;
            _searchService = searchService;
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
        [HttpPost("Add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(Guid inventoryId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();
            var invBase = await _context.Inventories
                .Where(x => x.Id == inventoryId)
                .Select(x => new { x.Id, x.OwnerId })
                .SingleOrDefaultAsync();
            if (invBase == null) return NotFound();
            var canManage = await _accessService.CanManageInventory(inventoryId, userId);
            if (!canManage)  return Forbid();
            var defaultType = CustomFieldType.SingleLineText;

            var sameTypeCount = await _context.CustomFields
                .CountAsync(f => f.InventoryId == inventoryId && f.Type == defaultType);
            if (sameTypeCount >= 3)
            {
                return BadRequest(new { error = "You can create up to 3 fields of this type in one inventory." });
            }
            var maxOrder = await _context.CustomFields
                .Where(f => f.InventoryId == inventoryId)
                .MaxAsync(f => (int?)f.Order) ?? -1;
            var existingNames = await _context.CustomFields
                .Where(f => f.InventoryId == inventoryId)
                .Select(f => f.Name)
                .ToListAsync();
            const string baseName = "New field";
            var newName = baseName;
            var suffix = 2;
            while (existingNames.Contains(newName))
            {
                newName = $"{baseName} {suffix}";
                suffix++;
            }
            var field = new CustomField
            {
                InventoryId = inventoryId,
                Name = newName,
                Type = defaultType,
                Order = maxOrder + 1,
                IsRequired = false,
                IsUnique = false
            };
            _context.CustomFields.Add(field);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return BadRequest(new { error = "Could not create field. Field name must be unique." });
            }
            return Ok(new
            {
                ok = true,
                field = new
                {
                    id = field.Id,
                    name = field.Name,
                    type = (int)field.Type,
                    order = field.Order,
                    isRequired = field.IsRequired,
                    isUnique = field.IsUnique
                }
            });
        }
        [HttpPost("{fieldId:int}/InlineUpdate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InlineUpdate(Guid inventoryId, int fieldId, [FromBody] CustomFieldInlineUpdateDto dto)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();
            var invBase = await _context.Inventories
                .Where(x => x.Id == inventoryId)
                .Select(x => new { x.Id, x.OwnerId })
                .SingleOrDefaultAsync();
            if (invBase == null) return NotFound();
            var canManage = await _accessService.CanManageInventory(inventoryId, userId);
            if (!canManage) return Forbid();
            var field = await _context.CustomFields
                .Where(f => f.InventoryId == inventoryId && f.Id == fieldId)
                .SingleOrDefaultAsync();
            if (field == null) return NotFound();
            var name = (dto.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(new { error = "Name is required." });
            }
            if (!Enum.IsDefined(typeof(CustomFieldType), dto.Type))
            {
                return BadRequest(new { error = "Invalid field type." });
            }
            var newType = (CustomFieldType)dto.Type;
            if (newType != field.Type)
            {
                var sameTypeCount = await _context.CustomFields
                    .CountAsync(f => f.InventoryId == inventoryId && f.Type == newType && f.Id != fieldId);
                if (sameTypeCount >= 3)
                {
                    return BadRequest(new { error = "You can create up to 3 fields of this type in one inventory." });
                }
            }
            field.Name = name;
            field.Type = newType;
            field.IsRequired = dto.IsRequired;
            field.IsUnique = dto.IsUnique;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return BadRequest(new { error = "Field name already exists in this inventory." });
            }
            return Ok(new { ok = true });
        }
        [HttpPost("Reorder")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reorder(Guid inventoryId, [FromBody] List<int> orderedIds)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();
            var invBase = await _context.Inventories
                .Where(x => x.Id == inventoryId)
                .Select(x => new { x.Id, x.OwnerId })
                .SingleOrDefaultAsync();
            if (invBase == null) return NotFound();
            var isAdmin = User.IsInRole("Admin");
            var canManage = await _accessService.CanManageInventory(inventoryId, userId);
            if (!canManage) return Forbid();
            var fields = await _context.CustomFields
                .Where(f => f.InventoryId == inventoryId)
                .ToListAsync();
            if (fields.Count != orderedIds.Count) return BadRequest();
            var current = fields.Select(f => f.Id).OrderBy(x => x);
            var incoming = orderedIds.OrderBy(x => x);
            if (!current.SequenceEqual(incoming)) return BadRequest();
            int temp = 1000;
            for (int i = 0; i < fields.Count; i++) fields[i].Order = temp + i;
            await _context.SaveChangesAsync();
            for (int i = 0; i < orderedIds.Count; i++)
            {
                var f = fields.Single(x => x.Id == orderedIds[i]);
                f.Order = i + 1;
            }
            await _context.SaveChangesAsync();
            return NoContent();
        }
        [HttpPost("{fieldId:int}/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid inventoryId, int fieldId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();
            var invBase = await _context.Inventories
                .Where(x => x.Id == inventoryId)
                .Select(x => new { x.Id, x.OwnerId })
                .SingleOrDefaultAsync();
            if (invBase == null) return NotFound();
            var isAdmin = User.IsInRole("Admin");
            var canManage = await _accessService.CanManageInventory(inventoryId, userId);
            if (!canManage) return Forbid();
            var field = await _context.CustomFields
                .Where(f => f.InventoryId == inventoryId && f.Id == fieldId)
                .SingleOrDefaultAsync();
            if (field == null) return NotFound();
            _context.CustomFields.Remove(field);
            await _context.SaveChangesAsync();
            return Ok(new { ok = true });
        }
    }
}
