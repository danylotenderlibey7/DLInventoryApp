using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.Services.Models;
using DLInventoryApp.Services.Tabs;
using DLInventoryApp.ViewModels.CustomFields;
using DLInventoryApp.ViewModels.Items.Inline;
using DLInventoryApp.ViewModels.Items.Pages;
using DLInventoryApp.ViewModels.Items.Tabs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
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
            if (inv == null)
                return NotFound();
            var ids = await _context.Items
                .Where(it => it.InventoryId == inventoryId)
                .Select(it => it.CustomId)
                .ToListAsync();
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
                CustomId = preview?.CustomId,
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
                Type = f.Type,
                IsRequired = f.IsRequired
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
                    .Select(f => new { f.Id, f.Type, f.IsRequired })
                    .ToListAsync();
                var errors = new Dictionary<string, string>();
                foreach (var fm in fieldsMeta)
                {
                    if (!fm.IsRequired) continue;
                    var incoming = req.Fields.FirstOrDefault(x => x.CustomFieldId == fm.Id);
                    bool missing = fm.Type switch
                    {
                        CustomFieldType.SingleLineText or CustomFieldType.MultiLineText
                            => string.IsNullOrWhiteSpace(incoming?.TextValue),
                        CustomFieldType.DocumentLink
                            => string.IsNullOrWhiteSpace(incoming?.LinkValue),
                        CustomFieldType.Number
                            => incoming?.NumberValue == null,
                        CustomFieldType.Boolean
                            => false,

                        _ => false
                    };
                    if (missing)
                        errors[fm.Id.ToString()] = "Required";
                }
                if (errors.Count > 0)
                    return BadRequest(new { errors });
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
            if (itemIds == null || itemIds.Count == 0) return RedirectToAction("Index", new { inventoryId });
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
        [HttpPost("{itemId:guid}/Cell")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCell(Guid inventoryId, Guid itemId, [FromBody] UpdateCellDto dto)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canEditItems = await _accessService.CanEditItems(inventoryId, userId);
            if (!canEditItems) return NotFound();
            var field = await _context.CustomFields
                .Where(f => f.InventoryId == inventoryId && f.Id == dto.CustomFieldId)
                .SingleOrDefaultAsync();
            if (field == null)
                return BadRequest(new { ok = false, error = "Field not found." });
            var item = await _context.Items
                .Where(it => it.InventoryId == inventoryId && it.Id == itemId)
                .SingleOrDefaultAsync();
            if (item == null) return NotFound();
            _context.Entry(item)
                .Property(x => x.Version).OriginalValue = dto.Version;
            var val = await _context.ItemFieldValues
                .Where(v => v.ItemId == itemId && v.CustomFieldId == field.Id)
                .SingleOrDefaultAsync();
            if (val == null)
            {
                val = new ItemFieldValue
                {
                    ItemId = itemId,
                    CustomFieldId = field.Id
                };
                _context.ItemFieldValues.Add(val);
            }
            string? text = null;
            decimal? number = null;
            string? link = null;
            bool? boolean = null;
            try
            {
                switch (field.Type)
                {
                    case CustomFieldType.SingleLineText:
                    case CustomFieldType.MultiLineText:
                        text = (dto.Value?.ToString() ?? "").Trim();
                        if (field.IsRequired && string.IsNullOrWhiteSpace(text))
                            return BadRequest(new { ok = false, error = "This field is required." });
                        break;
                    case CustomFieldType.DocumentLink:
                        link = (dto.Value?.ToString() ?? "").Trim();
                        if (field.IsRequired && string.IsNullOrWhiteSpace(link))
                            return BadRequest(new { ok = false, error = "This field is required." });
                        break;
                    case CustomFieldType.Number:
                        var s = (dto.Value?.ToString() ?? "").Trim();
                        if (field.IsRequired && string.IsNullOrWhiteSpace(s))
                            return BadRequest(new { ok = false, error = "This field is required." });
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            if (!decimal.TryParse(s, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                            {
                                if (!decimal.TryParse(s, out parsed))
                                    return BadRequest(new { ok = false, error = "Invalid number." });
                            }
                            number = parsed;
                        }
                        break;
                    case CustomFieldType.Boolean:
                        if (dto.Value is bool b) boolean = b;
                        else
                        {
                            var bs = (dto.Value?.ToString() ?? "").Trim().ToLower();
                            boolean = (bs == "true" || bs == "1" || bs == "yes");
                        }
                        break;
                }
            }
            catch
            {
                return BadRequest(new { ok = false, error = "Invalid value." });
            }
            if (field.IsUnique)
            {
                var q = _context.ItemFieldValues
                    .Where(v => v.CustomFieldId == field.Id && v.Item.InventoryId == inventoryId && v.ItemId != itemId);

                bool exists = field.Type switch
                {
                    CustomFieldType.SingleLineText or CustomFieldType.MultiLineText => await q.AnyAsync(v => v.TextValue == text),
                    CustomFieldType.DocumentLink => await q.AnyAsync(v => v.LinkValue == link),
                    CustomFieldType.Number => await q.AnyAsync(v => v.NumberValue == number),
                    CustomFieldType.Boolean => await q.AnyAsync(v => v.BoolValue == boolean),
                    _ => false
                };
                if (exists)
                    return BadRequest(new { ok = false, error = "Value must be unique." });
            }
            val.TextValue = null;
            val.NumberValue = null;
            val.LinkValue = null;
            val.BoolValue = null;
            switch (field.Type)
            {
                case CustomFieldType.SingleLineText:
                case CustomFieldType.MultiLineText:
                    val.TextValue = text;
                    break;
                case CustomFieldType.DocumentLink:
                    val.LinkValue = link;
                    break;
                case CustomFieldType.Number:
                    val.NumberValue = number;
                    break;
                case CustomFieldType.Boolean:
                    val.BoolValue = boolean ?? false;
                    break;
            }
            item.UpdatedAt = DateTime.UtcNow;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new
                {
                    ok = false,
                    error = "Item was modified by another user. Reload the page."
                });
            }
            await _searchService.IndexItemAsync(itemId);
            string display = field.Type switch
            {
                CustomFieldType.Boolean => (val.BoolValue == true ? "Yes" : "No"),
                CustomFieldType.Number => (val.NumberValue?.ToString() ?? ""),
                CustomFieldType.DocumentLink => (val.LinkValue ?? ""),
                _ => (val.TextValue ?? "")
            };
            return Ok(new { ok = true, display, version = Convert.ToBase64String(item.Version) });
        }
    }
}
