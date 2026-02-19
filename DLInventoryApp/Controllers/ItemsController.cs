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
    [Route("Inventories/{inventoryId:guid}/Items")]
    public class ItemsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICustomIdGenerator _customIdGenerator;
        private readonly IAccessService _accessService;
        public ItemsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, 
            ICustomIdGenerator customIdGenerator, IAccessService accessService)
        {
            _context = context;
            _userManager = userManager;
            _customIdGenerator = customIdGenerator;
            _accessService = accessService;
        }
        public async Task<IActionResult> Index(Guid inventoryId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var title = await _context.Inventories
                .Where(inv => inv.Id == inventoryId)
                .Select(inv => inv.Title)
                .SingleOrDefaultAsync();
            if (title == null)
                return NotFound();
            var items = await _context.Items
                .Where(it => it.InventoryId == inventoryId)
                .Select(it => new InventoryItemRowVm
                {
                    Id = it.Id,
                    CustomId = it.CustomId,
                    CreatedAt = it.CreatedAt,
                    UpdatedAt = it.UpdatedAt
                })
                .OrderByDescending(vm => vm.UpdatedAt ?? vm.CreatedAt)
                .ToListAsync();
            var itemIds = items.Select(x => x.Id).ToList();
            var allValues = await _context.ItemFieldValues
                .Where(av => itemIds.Contains(av.ItemId))
                .ToListAsync();
            var valuesByItem = allValues
                .GroupBy(v => v.ItemId)
                .ToDictionary(g => g.Key, g => g.ToList());
            var cols = await _context.CustomFields
                .Where(f => f.InventoryId == inventoryId)
                .OrderBy(f => f.Order)
                .Select(f => new CustomFieldColumnVm
                {
                    Id = f.Id,
                    Name = f.Name,
                    Order = f.Order,
                    IsRequired = f.IsRequired,
                    IsUnique = f.IsUnique,
                    Type = f.Type
                }).ToListAsync();
            foreach (var it in items)
            {
                it.Cells = new List<string?>();
                valuesByItem.TryGetValue(it.Id, out var values);
                foreach (var c in cols)
                {
                    var cell = values?.FirstOrDefault(v => v.CustomFieldId == c.Id);
                    string? cellText = null;
                    if (cell != null)
                    {
                        if (cell.TextValue != null)
                            cellText = cell.TextValue;
                        else if (cell.NumberValue != null)
                            cellText = cell.NumberValue.ToString();
                        else if (cell.DateValue != null)
                            cellText = cell.DateValue.Value.ToString("yyyy-MM-dd");
                        else if (cell.BoolValue != null)
                            cellText = cell.BoolValue.Value ? "Yes" : "No";
                    }
                    it.Cells.Add(cellText);
                }
            }
            var vm = new InventoryItemsVm
            {
                InventoryId = inventoryId,
                InventoryTitle = title,
                Items = items,
                Columns = cols
            };
            return View(vm);
        }
        [HttpGet("Create")]
        public async Task<IActionResult> Create(Guid inventoryId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canWrite = await _accessService.CanWriteInventory(inventoryId, userId);
            if (!canWrite) return NotFound();
            var inv = await _context.Inventories
                .Where(inv => inv.Id == inventoryId)
                .SingleOrDefaultAsync();
            if (inv == null)
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
            var fields = await _context.CustomFields
                .Where(f => f.InventoryId == inventoryId)
                .OrderBy(f => f.Order)
                .ToListAsync();
            vm.Fields = fields.Select(f => new FieldValueInputVm
            {
                CustomFieldId = f.Id,
                Name = f.Name,
                Type = f.Type,
                IsRequired = f.IsRequired
            }).ToList();
            return View(vm);
        }
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Guid inventoryId, CreateItemVm vm)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canWrite = await _accessService.CanWriteInventory(inventoryId, userId);
            if (!canWrite) return NotFound();
            if (inventoryId != vm.InventoryId)
                return NotFound();
            if (!ModelState.IsValid)
                return View(vm);
            var inv = await _context.Inventories
                .Where(x => x.Id == inventoryId)
                .SingleOrDefaultAsync();
            if (inv == null)
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
            foreach (var f in vm.Fields)
            {
                var value = new ItemFieldValue
                {
                    ItemId = item.Id,
                    CustomFieldId = f.CustomFieldId,
                    TextValue = f.TextValue,
                    NumberValue = f.NumberValue,
                    DateValue = f.DateValue,
                    BoolValue = f.BoolValue
                };
                _context.ItemFieldValues.Add(value);
            }
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
            return RedirectToAction("Index", new { inventoryId });
        }
        [HttpGet("{itemId:guid}/Edit")]
        public async Task<IActionResult> Edit(Guid inventoryId, Guid itemId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canWrite = await _accessService.CanWriteInventory(inventoryId, userId);
            if (!canWrite) return NotFound();
            var item = await _context.Items
                .Where(it => it.Id == itemId && it.InventoryId == inventoryId)
                .SingleOrDefaultAsync();
            if (item == null) return NotFound();
            var vm = new EditItemVm
            {
                InventoryId = inventoryId,
                ItemId = itemId,
                CustomId = item.CustomId
            };
            await FillEditVm(inventoryId, itemId, vm);
            return View(vm);
        }
        [HttpPost("{itemId:guid}/Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid inventoryId, Guid itemId, EditItemVm vm)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canWrite = await _accessService.CanWriteInventory(inventoryId, userId);
            if (!canWrite) return NotFound();
            if (inventoryId != vm.InventoryId) return NotFound();
            if (itemId != vm.ItemId) return NotFound();
            for (int i = 0; i < vm.Fields.Count; i++)
            {
                var f = vm.Fields[i];
                if (f.IsRequired && f.Type == CustomFieldType.Text)
                {
                    if (string.IsNullOrWhiteSpace(f.TextValue))
                    {
                        ModelState.AddModelError($"Fields[{i}].TextValue", "This field is required.");
                    }
                }
            }
            if (!ModelState.IsValid)
            {
                await FillEditVm(inventoryId, itemId, vm);
                return View(vm);
            }
            var item = await _context.Items
                .Where(it => it.Id == itemId && it.InventoryId == inventoryId)
                .SingleOrDefaultAsync();
            if (item == null) return NotFound();
            item.CustomId = vm.CustomId;
            var dbValues = await _context.ItemFieldValues
                .Where(v => v.ItemId == itemId)
                .ToListAsync();
            foreach (var f in vm.Fields)
            {
                var db = dbValues.FirstOrDefault(v => v.CustomFieldId == f.CustomFieldId);
                if (db == null)
                {
                    db = new ItemFieldValue
                    {
                        ItemId = itemId,
                        CustomFieldId = f.CustomFieldId
                    };
                    _context.ItemFieldValues.Add(db);
                    dbValues.Add(db); 
                }
                db.TextValue = f.TextValue;
                db.NumberValue = f.NumberValue;
                db.DateValue = f.DateValue;
                db.BoolValue = f.BoolValue;
            }
            await _context.SaveChangesAsync();
            return RedirectToAction("Edit", new { inventoryId, itemId });
        }
        [HttpPost("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid inventoryId, List<Guid> itemIds)
        {
            if (itemIds == null || itemIds.Count == 0) return RedirectToAction("Index", new { inventoryId });
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canWrite = await _accessService.CanWriteInventory(inventoryId, userId);
            if (!canWrite) return NotFound();
            var itemsToDelete = await _context.Items
                .Where(it => it.InventoryId == inventoryId && itemIds.Contains(it.Id))
                .ToListAsync();
            _context.Items.RemoveRange(itemsToDelete);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index", new { inventoryId });
        }
        private async Task FillEditVm(Guid inventoryId, Guid itemId, EditItemVm vm)
        {
            var values = await _context.ItemFieldValues
                .Where(v => v.ItemId == itemId)
                .ToListAsync();
            var title = await _context.Inventories
                .Where(inv => inv.Id == inventoryId)
                .Select(inv => inv.Title)
                .SingleOrDefaultAsync();
            var fields = await _context.CustomFields
                .Where(f => f.InventoryId == inventoryId)
                .OrderBy(f => f.Order)
                .ToListAsync();
            vm.Fields = fields.Select(f =>
            {
                var val = values.SingleOrDefault(v => v.CustomFieldId == f.Id);
                return new FieldValueInputVm
                {
                    CustomFieldId = f.Id,
                    Name = f.Name,
                    Type = f.Type,
                    TextValue = val?.TextValue,
                    NumberValue = val?.NumberValue,
                    DateValue = val?.DateValue,
                    BoolValue = val?.BoolValue ?? false,
                    IsRequired = f.IsRequired
                };
            }).ToList();
            vm.InventoryTitle = title ?? "";
        }
    }
}
