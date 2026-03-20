using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.ViewModels.CustomFields;
using DLInventoryApp.ViewModels.CustomFields.Dtos;
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
            if (title == null) return NotFound();
            var fields = await _context.CustomFields
                .Where(f => f.InventoryId == inventoryId)
                .Select(f => new CustomFieldColumnVm
                {
                    Id = f.Id,
                    Name = f.Name,
                    Description = f.Description,
                    Type = f.Type,
                    Order = f.Order,
                    ShowInTable = f.ShowInTable,
                    Version = f.Version
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
        //[HttpGet("Create")]
        //public async Task<IActionResult> Create(Guid inventoryId)
        //{
        //    var userId = _userManager.GetUserId(User);
        //    var inv = await _context.Inventories
        //        .Where(inv => inv.Id == inventoryId)
        //        .SingleOrDefaultAsync();
        //    if (inv == null) return NotFound();
        //    if (inv.OwnerId != userId) return NotFound();
        //    var vm = new CreateCustomFieldVm
        //    {
        //        InventoryId = inventoryId,
        //        InventoryTitle = inv.Title
        //    };
        //    return View(vm);
        //}
        //[HttpPost("Create")]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Create(Guid inventoryId, CreateCustomFieldVm vm)
        //{
        //    if (inventoryId != vm.InventoryId) return NotFound();
        //    if (!ModelState.IsValid) return View(vm);
        //    var userId = _userManager.GetUserId(User);
        //    var inv = await _context.Inventories
        //        .Where(x => x.Id == inventoryId)
        //        .SingleOrDefaultAsync();
        //    if (inv == null) return NotFound();
        //    if (inv.OwnerId != userId) return NotFound();
        //    vm.InventoryTitle = inv.Title;
        //    var sameTypeCount = await _context.CustomFields
        //        .Where(f => f.InventoryId == inventoryId && f.Type == vm.Type)
        //        .CountAsync();
        //    if (sameTypeCount >= 3)
        //    {
        //        ModelState.AddModelError(nameof(vm.Type), "You can create up to 3 fields of this type in one inventory.");
        //        return View(vm);
        //    }
        //    var maxOrderOrNull = await _context.CustomFields
        //        .Where(f => f.InventoryId == inventoryId)
        //        .MaxAsync(f => (int?)f.Order);
        //    var nextOrder = (maxOrderOrNull ?? -1) + 1;
        //    var field = new CustomField
        //    {
        //        InventoryId = inventoryId,
        //        Name = (vm.Name ?? string.Empty).Trim(),
        //        Description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim(),
        //        Type = vm.Type,
        //        Order = nextOrder,
        //        ShowInTable = vm.ShowInTable
        //    };
        //    _context.CustomFields.Add(field);
        //    try
        //    {
        //        await _context.SaveChangesAsync(); 
        //        await _searchService.ReindexInventoryItemsAsync(inventoryId);
        //    }
        //    catch (DbUpdateException)
        //    {
        //        ModelState.AddModelError(nameof(vm.Name), "Field name already exists in this inventory.");
        //        return View(vm);
        //    }
        //    return RedirectToAction("Index", new { inventoryId });
        //}
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
                Description = null,
                Type = defaultType,
                Order = maxOrder + 1,
                ShowInTable = true
            };
            _context.CustomFields.Add(field);
            try
            {
                await _context.SaveChangesAsync(); 
                await _searchService.ReindexInventoryItemsAsync(inventoryId);
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
                    description = field.Description,
                    type = (int)field.Type,
                    order = field.Order,
                    showInTable = field.ShowInTable,
                    version = field.Version
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
            _context.Entry(field).Property(x => x.Version).OriginalValue = dto.Version;
            var name = (dto.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest(new { ok = false, error = "Name is required." });
            if (!Enum.IsDefined(typeof(CustomFieldType), dto.Type))
                return BadRequest(new { ok = false, error = "Invalid field type." });
            var newType = (CustomFieldType)dto.Type;
            if (newType != field.Type)
            {
                var sameTypeCount = await _context.CustomFields
                    .CountAsync(f => f.InventoryId == inventoryId && f.Type == newType && f.Id != fieldId);
                if (sameTypeCount >= 3)
                    return BadRequest(new { ok = false, error = "You can create up to 3 fields of this type in one inventory." });
            }
            field.Name = name;
            field.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
            field.Type = newType;
            field.ShowInTable = dto.ShowInTable;
            try
            {
                await _context.SaveChangesAsync();
                await _searchService.ReindexInventoryItemsAsync(inventoryId);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { ok = false, error = "Field was updated by someone else. Refresh the page." });
            }
            catch (DbUpdateException)
            {
                return BadRequest(new { ok = false, error = "Field name already exists in this inventory." });
            }
            return Ok(new { ok = true, version = field.Version });
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
            var canManage = await _accessService.CanManageInventory(inventoryId, userId);
            if (!canManage) return Forbid();
            if (orderedIds == null || orderedIds.Count == 0) return BadRequest(new { ok = false, error = "Empty reorder payload." });
            var fields = await _context.CustomFields
                .Where(f => f.InventoryId == inventoryId)
                .ToListAsync();
            if (fields.Count != orderedIds.Count) return BadRequest(new { ok = false, error = "Invalid reorder payload." });
            var current = fields.Select(f => f.Id).OrderBy(x => x).ToList();
            var incoming = orderedIds.OrderBy(x => x).ToList();
            if (!current.SequenceEqual(incoming)) return BadRequest(new { ok = false, error = "Invalid reorder payload." });
            try
            {
                var tempBase = 1000;
                for (int i = 0; i < orderedIds.Count; i++)
                {
                    var field = fields.Single(x => x.Id == orderedIds[i]);
                    field.Order = tempBase + i + 1;
                }
                await _context.SaveChangesAsync();
                for (int i = 0; i < orderedIds.Count; i++)
                {
                    var field = fields.Single(x => x.Id == orderedIds[i]);
                    field.Order = i + 1;
                }
                await _context.SaveChangesAsync();
                return Ok(new
                {
                    ok = true,
                    versions = fields.Select(f => new
                    {
                        id = f.Id,
                        version = f.Version
                    }).ToList()
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { ok = false, error = "Fields were modified by another user. Reload the page." });
            }
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
            var canManage = await _accessService.CanManageInventory(inventoryId, userId);
            if (!canManage) return Forbid();
            var field = await _context.CustomFields
                .Where(f => f.InventoryId == inventoryId && f.Id == fieldId)
                .SingleOrDefaultAsync();
            if (field == null) return NotFound();
            _context.CustomFields.Remove(field);
            await _context.SaveChangesAsync();
            await _searchService.ReindexInventoryItemsAsync(inventoryId);
            return Ok(new { ok = true });
        }
    }
}
