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
                .Where(inv => inv.Id == inventoryId)
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
            var contentType = Request.ContentType ?? "";
            var isJson = contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase);
            if (isJson)
            {
                var userId = _userManager.GetUserId(User);
                if (userId == null) return Unauthorized();
                var canEditItems = await _accessService.CanEditItems(inventoryId, userId);
                if (!canEditItems) return Forbid();
                InlineCreateItemRequest? req;
                try
                {
                    req = await JsonSerializer.DeserializeAsync<InlineCreateItemRequest>(
                        Request.Body,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                }
                catch
                {
                    return BadRequest(new { errors = new Dictionary<string, string> { ["_"] = "Invalid JSON" } });
                }
                req ??= new InlineCreateItemRequest();
                var fieldsMeta = await _context.CustomFields
                    .Where(f => f.InventoryId == inventoryId)
                    .OrderBy(f => f.Order)
                    .Select(f => new { f.Id, f.Type })
                    .ToListAsync();
                int? sequenceNumber = null;
                bool auto = string.IsNullOrWhiteSpace(req.CustomId);
                int attempts = auto ? 3 : 1;
                for (int i = 0; i < attempts; i++)
                {
                    sequenceNumber = null;
                    var customId = (req.CustomId ?? "").Trim();
                    if (auto)
                    {
                        var gen = await _customIdGenerator.GenerateAsync(inventoryId);
                        customId = gen.CustomId;
                        sequenceNumber = gen.SequenceNumber;
                    }
                    var item = new Item
                    {
                        Id = Guid.NewGuid(),
                        InventoryId = inventoryId,
                        CustomId = customId,
                        CreatedById = userId,
                        CreatedAt = DateTime.UtcNow,
                        SequenceNumber = sequenceNumber
                    };
                    _context.Items.Add(item);
                    foreach (var fm in fieldsMeta)
                    {
                        var incoming = req.Fields.FirstOrDefault(x => x.CustomFieldId == fm.Id);
                        _context.ItemFieldValues.Add(new ItemFieldValue
                        {
                            ItemId = item.Id,
                            CustomFieldId = fm.Id,
                            TextValue = incoming?.TextValue,
                            NumberValue = incoming?.NumberValue,
                            LinkValue = incoming?.LinkValue,
                            BoolValue = incoming?.BoolValue
                        });
                    }
                    try
                    {
                        await _context.SaveChangesAsync();
                        await _searchService.IndexItemAsync(item.Id);
                        var resp = new InlineCreateItemResponse
                        {
                            ItemId = item.Id,
                            CustomId = item.CustomId,
                            CreatedAt = item.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                            UpdatedAt = item.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                            LikesCount = 0,
                            IsLikedByMe = false,
                            Cells = fieldsMeta.Select(fm =>
                            {
                                var incoming = req.Fields.FirstOrDefault(x => x.CustomFieldId == fm.Id);
                                if (incoming == null) return (string?)null;
                                return fm.Type switch
                                {
                                    CustomFieldType.SingleLineText => incoming.TextValue,
                                    CustomFieldType.MultiLineText => incoming.TextValue,
                                    CustomFieldType.DocumentLink => incoming.LinkValue,
                                    CustomFieldType.Number => incoming.NumberValue?.ToString(),
                                    CustomFieldType.Boolean => (incoming.BoolValue ?? false) ? "Yes" : "No",
                                    _ => null
                                };
                            }).ToList()
                        };
                        return Ok(resp);
                    }
                    catch (DbUpdateException)
                    {
                        if (!auto)
                            return BadRequest(new { errors = new Dictionary<string, string> { ["customId"] = "Custom ID already exists in this inventory." } });
                        _context.ChangeTracker.Clear();
                        if (i == attempts - 1)
                            return BadRequest(new { errors = new Dictionary<string, string> { ["customId"] = "Failed to generate a unique Custom ID." } });
                    }
                }
                return BadRequest(new { errors = new Dictionary<string, string> { ["customId"] = "Failed" } });
            }
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
            await _context.Items
                .Where(i => i.Id == itemId)
                .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.ViewsTotal, i => i.ViewsTotal + 1));
            var vm = new EditItemVm
            {
                InventoryId = inventoryId,
                ItemId = itemId,
                CustomId = item.CustomId,
                CanEditItems = canEditItems
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
            foreach (var it in itemsToDelete)
                await _searchService.RemoveItemAsync(it.Id);
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
