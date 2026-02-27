using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.ViewModels.CustomFields;
using DLInventoryApp.ViewModels.Items;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

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
        private readonly ILikeService _likeService; 
        private readonly ISearchService _searchService;
        public ItemsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, 
            ICustomIdGenerator customIdGenerator, IAccessService accessService, 
            ILikeService likeService, ISearchService searchService)
        {
            _context = context;
            _userManager = userManager;
            _customIdGenerator = customIdGenerator;
            _accessService = accessService;
            _likeService = likeService;
            _searchService = searchService;
        }
        [AllowAnonymous]
        public async Task<IActionResult> Index(Guid inventoryId)
        {
            var userId = _userManager.GetUserId(User);
            bool canWrite = false;
            if (userId != null)
            {
                canWrite = await _accessService.CanWriteInventory(inventoryId, userId);
            }
            var title = await _context.Inventories
                .Where(inv => inv.Id == inventoryId)
                .Select(inv => inv.Title)
                .SingleOrDefaultAsync();
            if (title == null) return NotFound();
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
            var counts = await _context.ItemLikes
                .Where(l => itemIds.Contains(l.ItemId))
                .GroupBy(l => l.ItemId)
                .Select(g => new { ItemId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ItemId, x => x.Count);
            HashSet<Guid> likedSet = new();
            if (userId != null)
            {
                var likedList = await _context.ItemLikes
                    .Where(l => l.UserId == userId && itemIds.Contains(l.ItemId))
                    .Select(l => l.ItemId)
                    .ToListAsync();
                likedSet = likedList.ToHashSet();
            }
            foreach (var it in items)
            {
                it.LikesCount = counts.TryGetValue(it.Id, out var c) ? c : 0;
                it.IsLikedByMe = userId != null && likedSet.Contains(it.Id);
                it.Cells = new List<string?>();
                valuesByItem.TryGetValue(it.Id, out var values);
                foreach (var ccol in cols)
                {
                    var cell = values?.FirstOrDefault(v => v.CustomFieldId == ccol.Id);
                    string? cellText = null;
                    if (cell != null)
                    {
                        if (cell.TextValue != null) cellText = cell.TextValue;
                        else if (cell.NumberValue != null) cellText = cell.NumberValue.ToString();
                        else if (cell.LinkValue != null) cellText = cell.LinkValue;
                        else if (cell.BoolValue != null) cellText = cell.BoolValue.Value ? "Yes" : "No";
                    }
                    it.Cells.Add(cellText);
                }
            }
            var vm = new InventoryItemsVm
            {
                InventoryId = inventoryId,
                InventoryTitle = title,
                Items = items,
                Columns = cols,
                CanWrite = canWrite
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
            //var customId = _customIdGenerator.Generate(inv.Title, ids);
            var vm = new CreateItemVm
            {
                InventoryId = inventoryId,
                InventoryTitle = inv.Title,
                //CustomId = customId,
                CanWrite = canWrite,
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
            if (inventoryId != vm.InventoryId) return NotFound();
            if (!ModelState.IsValid)
            {
                await FillCreateVmAsync(inventoryId, vm);
                return View(vm);
            }
            var inv = await _context.Inventories
                .Where(x => x.Id == inventoryId)
                .SingleOrDefaultAsync();
            if (inv == null) return NotFound();
            int? sequenceNumber = null;
            if (string.IsNullOrWhiteSpace(vm.CustomId))
            {
                var result = await _customIdGenerator.GenerateAsync(inventoryId);
                vm.CustomId = result.CustomId;
                sequenceNumber = result.SequenceNumber;
            }
            else if (!await _customIdGenerator.MatchesTemplateAsync(inventoryId, vm.CustomId))
            {
                await FillCreateVmAsync(inventoryId, vm);
                ModelState.AddModelError(nameof(vm.CustomId), "Custom ID does not match this inventory template.");
                return View(vm);
            }
            var item = new Item
            {
                Id = Guid.NewGuid(),
                InventoryId = inventoryId,
                CustomId = vm.CustomId,
                CreatedById = userId,
                CreatedAt = DateTime.UtcNow,
                SequenceNumber = sequenceNumber
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
                    LinkValue = f.LinkValue,
                    BoolValue = f.BoolValue
                };
                _context.ItemFieldValues.Add(value);
            }
            try
            {
                await _context.SaveChangesAsync();
                await _searchService.IndexItemAsync(item.Id);
            }
            catch (DbUpdateException)
            {
                vm.InventoryTitle = inv.Title;
                await FillCreateVmAsync(inventoryId, vm);
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
                CustomId = item.CustomId,
                CanWrite = canWrite
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
                if (f.IsRequired)
                {
                    if (f.Type == CustomFieldType.SingleLineText || f.Type == CustomFieldType.MultiLineText)
                    {
                        if (string.IsNullOrWhiteSpace(f.TextValue))
                        {
                            ModelState.AddModelError($"Fields[{i}].TextValue", "This field is required.");
                        }
                    }
                    else if (f.Type == CustomFieldType.DocumentLink)
                    {
                        if (string.IsNullOrWhiteSpace(f.LinkValue))
                        {
                            ModelState.AddModelError($"Fields[{i}].LinkValue", "This field is required.");
                        }
                    }
                    else if (f.Type == CustomFieldType.Number)
                    {
                        if (f.NumberValue == null)
                        {
                            ModelState.AddModelError($"Fields[{i}].NumberValue", "This field is required.");
                        }
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
            if (!await _customIdGenerator.MatchesTemplateAsync(inventoryId, vm.CustomId))
            {
                ModelState.AddModelError(nameof(vm.CustomId), "Custom ID does not match this inventory template.");
                await FillEditVm(inventoryId, itemId, vm);
                return View(vm);
            }
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
                db.LinkValue = f.LinkValue;
                db.BoolValue = f.BoolValue;
            }
            await _context.SaveChangesAsync();
            await _searchService.IndexItemAsync(itemId);
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
            foreach (var it in itemsToDelete)
                await _searchService.RemoveItemAsync(it.Id);
            return RedirectToAction("Index", new { inventoryId });
        }
        private async Task FillCreateVmAsync(Guid inventoryId, CreateItemVm vm)
        {
            if (vm == null) return;
            var title = await _context.Inventories
                .Where(inv => inv.Id == inventoryId)
                .Select(inv => inv.Title)
                .SingleOrDefaultAsync();
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
            vm.InventoryTitle = title ?? "";
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
                    LinkValue = val?.LinkValue,
                    BoolValue = val?.BoolValue ?? false,
                    IsRequired = f.IsRequired
                };
            }).ToList();
            vm.InventoryTitle = title ?? "";
        }
        [HttpPost("{itemId:guid}/Like")]
        public async Task<IActionResult> ToggleLike(Guid inventoryId, Guid itemId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            await _likeService.ToggleAsync(itemId, userId);
            return RedirectToAction("Index", new { inventoryId });
        }
    }
}
