using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.Services.Models;
using DLInventoryApp.ViewModels.Items.Inline;
using DLInventoryApp.ViewModels.Items.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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
        private readonly ISearchService _searchService;
        public ItemsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, 
            ICustomIdGenerator customIdGenerator, IAccessService accessService, ISearchService searchService)
        {
            _context = context;
            _userManager = userManager;
            _customIdGenerator = customIdGenerator;
            _accessService = accessService;
            _searchService = searchService;
        }
        [HttpGet("Create")]
        public async Task<IActionResult> Create(Guid inventoryId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canEditItems = await _accessService.CanEditItems(inventoryId, userId);
            if (!canEditItems) return NotFound();
            var inv = await _context.Inventories
                .Where(i => i.Id == inventoryId)
                .Select(i => new { i.Title })
                .SingleOrDefaultAsync();
            if (inv == null) return NotFound();
            CustomIdResult? preview = null;
            try
            {
                preview = await _customIdGenerator.PreviewAsync(inventoryId);
            }
            catch
            {
                preview = null;
            }
            var vm = new CreateItemVm
            {
                InventoryId = inventoryId,
                InventoryTitle = inv.Title,
                CustomId = null,
                PreviewCustomId = preview?.CustomId,
                CanEditItem = canEditItems,
            };
            var fields = await _context.CustomFields
                .Where(f => f.InventoryId == inventoryId)
                .OrderBy(f => f.Order)
                .ToListAsync();
            vm.Fields = fields.Select(f => new FieldValueInputVm
            {
                CustomFieldId = f.Id,
                Name = f.Name,
                Description = f.Description,
                Type = f.Type
            }).ToList();
            return View(vm);
        }
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Guid inventoryId, CreateItemVm vm)
        {
            var userIdForm = _userManager.GetUserId(User);
            if (userIdForm == null) return Challenge();
            var canEditItemsForm = await _accessService.CanEditItems(inventoryId, userIdForm);
            if (!canEditItemsForm) return NotFound();
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
            int? sequenceNumberForm = null;
            bool autoForm = string.IsNullOrWhiteSpace(vm.CustomId);
            int attemptsForm = autoForm ? 3 : 1;
            for (int i = 0; i < attemptsForm; i++)
            {
                sequenceNumberForm = null;
                if (autoForm)
                {
                    var result = await _customIdGenerator.GenerateAsync(inventoryId);
                    vm.CustomId = result.CustomId;
                    sequenceNumberForm = result.SequenceNumber;
                }
                var item = new Item
                {
                    Id = Guid.NewGuid(),
                    InventoryId = inventoryId,
                    CustomId = vm.CustomId!,
                    CreatedById = userIdForm,
                    CreatedAt = DateTime.UtcNow,
                    SequenceNumber = sequenceNumberForm
                };
                _context.Items.Add(item);
                foreach (var f in vm.Fields)
                {
                    _context.ItemFieldValues.Add(new ItemFieldValue
                    {
                        ItemId = item.Id,
                        CustomFieldId = f.CustomFieldId,
                        TextValue = f.TextValue,
                        NumberValue = f.NumberValue,
                        LinkValue = f.LinkValue,
                        BoolValue = f.BoolValue
                    });
                }
                try
                {
                    await _context.SaveChangesAsync();
                    await _searchService.IndexItemAsync(item.Id);
                    return RedirectToAction("Details", "Inventories", new { id = inventoryId, tab = "items" });
                }
                catch (DbUpdateException)
                {
                    if (!autoForm)
                    {
                        await FillCreateVmAsync(inventoryId, vm);
                        ModelState.AddModelError(nameof(vm.CustomId), "Custom ID already exists in this inventory.");
                        return View(vm);
                    }
                    _context.ChangeTracker.Clear();
                    if (i == attemptsForm - 1)
                    {
                        await FillCreateVmAsync(inventoryId, vm);
                        ModelState.AddModelError(nameof(vm.CustomId), "Failed to generate a unique Custom ID. Try again.");
                        return View(vm);
                    }
                }
            }
            return RedirectToAction("Details", "Inventories", new { id = vm.InventoryId, tab = "items" });
        }
        [HttpGet("{itemId:guid}/Edit")]
        public async Task<IActionResult> Edit(Guid inventoryId, Guid itemId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canEditItems = await _accessService.CanEditItems(inventoryId, userId);
            if (!canEditItems) return NotFound();
            var item = await _context.Items
                .Where(it => it.Id == itemId && it.InventoryId == inventoryId)
                .SingleOrDefaultAsync();
            if (item == null) return NotFound();
            var vm = new EditItemVm
            {
                InventoryId = inventoryId,
                ItemId = itemId,
                CustomId = item.CustomId,
                CanEditItems = canEditItems,
                Version = item.Version
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
            var canEditItems = await _accessService.CanEditItems(inventoryId, userId);
            if (!canEditItems) return NotFound();
            if (inventoryId != vm.InventoryId) return NotFound();
            if (itemId != vm.ItemId) return NotFound();
            if (!ModelState.IsValid)
            {
                await FillEditVm(inventoryId, itemId, vm);
                return View(vm);
            }
            var item = await _context.Items
                .Where(it => it.Id == itemId && it.InventoryId == inventoryId)
                .SingleOrDefaultAsync();
            if (item == null) return NotFound(); 
            _context.Entry(item).Property(x => x.Version).OriginalValue = vm.Version;
            item.CustomId = (vm.CustomId ?? "").Trim();
            item.UpdatedAt = DateTime.UtcNow;
            var dbValues = await _context.ItemFieldValues
                .Where(v => v.ItemId == itemId)
                .ToListAsync();
            var dbMap = dbValues.ToDictionary(v => v.CustomFieldId);
            foreach (var f in vm.Fields)
            {
                if (!dbMap.TryGetValue(f.CustomFieldId, out var db))
                {
                    db = new ItemFieldValue
                    {
                        ItemId = itemId,
                        CustomFieldId = f.CustomFieldId
                    };
                    _context.ItemFieldValues.Add(db);
                    dbMap[f.CustomFieldId] = db;
                }
                db.TextValue = f.TextValue;
                db.NumberValue = f.NumberValue;
                db.LinkValue = f.LinkValue;
                db.BoolValue = f.BoolValue;
            }
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError("", "The item has been updated by someone else. Please refresh the page and apply your changes again.");
                await FillEditVm(inventoryId, itemId, vm);
                var freshVersion = await _context.Items
                    .AsNoTracking()
                    .Where(x => x.Id == itemId)
                    .Select(x => x.Version)
                    .SingleOrDefaultAsync(); 
                vm.Version = freshVersion;
                return View(vm);
            }
            await _searchService.IndexItemAsync(itemId);
            return RedirectToAction("Details", "Inventories", new { id = vm.InventoryId, tab = "items" });
        }
        [HttpPost("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid inventoryId, List<Guid> itemIds)
        {
            if (itemIds == null || itemIds.Count == 0) 
                return RedirectToAction("Details", "Inventories", new { id = inventoryId, tab = "items" });
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canEditItems = await _accessService.CanEditItems(inventoryId, userId);
            if (!canEditItems) return NotFound();
            var itemsToDelete = await _context.Items
                .Where(it => it.InventoryId == inventoryId && itemIds.Contains(it.Id))
                .ToListAsync();
            _context.Items.RemoveRange(itemsToDelete);
            await _context.SaveChangesAsync();
            var deletedIds = itemsToDelete.Select(it => it.Id).ToList();
            await _searchService.RemoveItemsAsync(deletedIds);
            return RedirectToAction("Details", "Inventories", new { id = inventoryId, tab = "items" });
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
                Description = f.Description,
                Type = f.Type
            }).ToList();
            vm.InventoryTitle = title ?? "";
        }
        private async Task FillEditVm(Guid inventoryId, Guid itemId, EditItemVm vm)
        {
            var values = await _context.ItemFieldValues
                .Where(v => v.ItemId == itemId)
                .ToListAsync();
            var valuesMap = values.ToDictionary(v => v.CustomFieldId);
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
                valuesMap.TryGetValue(f.Id, out var val);
                return new FieldValueInputVm
                {
                    CustomFieldId = f.Id,
                    Name = f.Name,
                    Description = f.Description,
                    Type = f.Type,
                    TextValue = val?.TextValue,
                    NumberValue = val?.NumberValue,
                    LinkValue = val?.LinkValue,
                    BoolValue = val?.BoolValue ?? false
                };
            }).ToList();
            vm.InventoryTitle = title ?? "";
        }
        [HttpPost("Likes/Set")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetLikes(Guid inventoryId, [FromBody] SetLikesRequest req)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();
            if (req?.ItemIds == null || req.ItemIds.Count == 0)
                return Ok(new { ok = true, items = Array.Empty<object>() });
            var validItemIds = await _context.Items
                .Where(i => i.InventoryId == inventoryId && req.ItemIds.Contains(i.Id))
                .Select(i => i.Id)
                .ToListAsync();
            if (validItemIds.Count == 0)
                return Ok(new { ok = true, items = Array.Empty<object>() });
            if (req.Like)
            {
                var existing = await _context.ItemLikes
                    .Where(l => l.UserId == userId && validItemIds.Contains(l.ItemId))
                    .Select(l => l.ItemId)
                    .ToListAsync();
                var likedSet = existing.ToHashSet();
                foreach (var id in validItemIds)
                {
                    if (!likedSet.Contains(id))
                    {
                        _context.ItemLikes.Add(new ItemLike
                        {
                            ItemId = id,
                            UserId = userId
                        });
                    }
                }
            }
            else
            {
                var likes = await _context.ItemLikes
                    .Where(l => l.UserId == userId && validItemIds.Contains(l.ItemId))
                    .ToListAsync();
                _context.ItemLikes.RemoveRange(likes);
            }
            await _context.SaveChangesAsync();
            var counts = await _context.ItemLikes
                .Where(l => validItemIds.Contains(l.ItemId))
                .GroupBy(l => l.ItemId)
                .Select(g => new { ItemId = g.Key, LikesCount = g.Count() })
                .ToListAsync();
            var countMap = counts.ToDictionary(x => x.ItemId, x => x.LikesCount);
            return Ok(new
            {
                ok = true,
                items = validItemIds.Select(id => new
                {
                    id,
                    liked = req.Like,
                    likesCount = countMap.TryGetValue(id, out var c) ? c : 0
                })
            });
        }
    }
}
