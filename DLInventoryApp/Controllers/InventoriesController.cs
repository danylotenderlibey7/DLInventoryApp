using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.ViewModels.Inventories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DLInventoryApp.Services.Tabs;
using DLInventoryApp.ViewModels.Inventories.Pages;
using DLInventoryApp.ViewModels.Inventories.Inline;
using DLInventoryApp.ViewModels.Inventories.Tabs.Settings.Dtos;
using DLInventoryApp.ViewModels.Common.Pagination;
using DLInventoryApp.ViewModels.Categories;
using Humanizer;

namespace DLInventoryApp.Controllers
{
    [Authorize]
    public class InventoriesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAccessService _accessService;
        private readonly ITagService _tagService;
        private readonly ISearchService _searchService;
        private readonly CustomIdTabBuilder _customIdTabBuilder;
        private readonly FieldsTabBuilder _fieldsTabBuilder;
        private readonly SettingsTabBuilder _settingsTabBuilder;
        private readonly AccessTabBuilder _accessTabBuilder;
        private readonly ChatTabBuilder _chatTabBuilder;
        private readonly ItemsTabBuilder _itemsTabBuilder;
        public InventoriesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            ITagService tagService, IAccessService accessService, ISearchService searchService, CustomIdTabBuilder customIdTabBuilder, FieldsTabBuilder fieldsTabBuilder,
            SettingsTabBuilder settingsTabBuilder, AccessTabBuilder accessTabBuilder, ChatTabBuilder chatTabBuilder, ItemsTabBuilder itemsTabBuilder)
        {
            _context = context;
            _userManager = userManager;
            _tagService = tagService;
            _accessService = accessService;
            _searchService = searchService;
            _customIdTabBuilder = customIdTabBuilder;
            _fieldsTabBuilder = fieldsTabBuilder;
            _settingsTabBuilder = settingsTabBuilder;
            _accessTabBuilder = accessTabBuilder;
            _chatTabBuilder = chatTabBuilder;
            _itemsTabBuilder = itemsTabBuilder;
        }
        [AllowAnonymous]
        public async Task<IActionResult> Index(string? tag, string view = "latest", int page = 1, int pageSize = 6)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 6 or > 50 ? 10 : pageSize;
            var query = _context.Inventories.AsQueryable();
            if (!string.IsNullOrWhiteSpace(tag))
            {
                tag = tag.Trim().ToLower();
                query = query.Where(inv =>
                    inv.InventoryTags.Any(it => it.Tag.Name == tag)
                );
            }
            view = (view ?? "latest").ToLower();
            var allowedViews = new[] { "latest", "popular", "all" };
            if (!allowedViews.Contains(view)) view = "latest";
            switch (view)
            {
                case "latest":
                    query = query.OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt);
                    break;
                case "popular":
                    query = query.OrderByDescending(i => i.Items.Count);
                    break;
                case "all":
                    query = query.OrderBy(i => i.Title);
                    break;
            }
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages > 0 && page > totalPages) page = totalPages;
            int skip = (page - 1) * pageSize;
            var list = await query
                .Select(inv => new MyInventoryRowVm
                {
                    Id = inv.Id,
                    Title = inv.Title,
                    OwnerId = inv.OwnerId,
                    OwnerUserName = inv.Owner.UserName,
                    CreatedAt = inv.CreatedAt,
                    UpdatedAt = inv.UpdatedAt,
                    ItemsCount = inv.Items.Count(),
                    CategoryName = inv.Category != null ? inv.Category.Name : null,
                    //Tags = inv.InventoryTags
                    //.Select(it => it.Tag.Name)
                    //.ToList()
                })
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();
            var vm = new PagedVm<MyInventoryRowVm>
            {
                Items = list,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
            var tags = await _context.Tags
                .Select(t => new
                {
                    Name = t.Name,
                    Count = t.InventoryTags.Count
                })
                .OrderByDescending(x => x.Count)
                .Take(20)
                .Select(x => x.Name)
                .ToListAsync();
            ViewBag.TagCloud = tags;
            ViewBag.Tag = tag;
            ViewBag.View = view;
            return View(vm);
        }
        public async Task<IActionResult> My(int ownedPage = 1, int sharedPage = 1, int pageSize = 4)
        {
            ownedPage = ownedPage < 1 ? 1 : ownedPage;
            sharedPage = sharedPage < 1 ? 1 : sharedPage;
            pageSize = pageSize is < 1 or > 50 ? 4 : pageSize;
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var ownedQuery = _context.Inventories
                .Where(inv => inv.OwnerId == userId)
                .OrderByDescending(inv => inv.UpdatedAt ?? inv.CreatedAt);
            var ownedTotalCount = await ownedQuery.CountAsync();
            var ownedTotalPages = (int)Math.Ceiling(ownedTotalCount / (double)pageSize);
            if (ownedTotalPages > 0 && ownedPage > ownedTotalPages) ownedPage = ownedTotalPages;
            var ownedSkip = (ownedPage - 1) * pageSize;
            var ownedList = await ownedQuery
                .Select(inv => new MyInventoryRowVm
                {
                    Id = inv.Id,
                    Title = inv.Title,
                    IsPublic = inv.IsPublic,
                    CreatedAt = inv.CreatedAt,
                    UpdatedAt = inv.UpdatedAt,
                    ItemsCount = inv.Items.Count(),
                    CategoryName = inv.Category != null ? inv.Category.Name : null,
                    //Tags = inv.InventoryTags
                    //    .Select(it => it.Tag.Name)
                    //    .ToList()
                })
                .Skip(ownedSkip)
                .Take(pageSize)
                .ToListAsync();
            var sharedQuery = _context.InventoryWriteAccesses
                .Where(ac => ac.UserId == userId)
                .Select(ac => ac.Inventory)
                .Where(inv => inv.OwnerId != userId)
                .OrderByDescending(inv => inv.UpdatedAt ?? inv.CreatedAt);
            var sharedTotalCount = await sharedQuery.CountAsync();
            var sharedTotalPages = (int)Math.Ceiling(sharedTotalCount / (double)pageSize);
            if (sharedTotalPages > 0 && sharedPage > sharedTotalPages) sharedPage = sharedTotalPages;
            var sharedSkip = (sharedPage - 1) * pageSize;
            var sharedList = await sharedQuery
                .Select(inv => new MyInventoryRowVm
                {
                    Id = inv.Id,
                    Title = inv.Title,
                    IsPublic = inv.IsPublic,
                    OwnerId = inv.OwnerId,
                    OwnerUserName = inv.Owner.UserName,
                    CreatedAt = inv.CreatedAt,
                    UpdatedAt = inv.UpdatedAt,
                    ItemsCount = inv.Items.Count(),
                    CategoryName = inv.Category != null ? inv.Category.Name : null,
                    //Tags = inv.InventoryTags
                    //    .Select(it => it.Tag.Name)
                    //    .ToList()
                })
                .Skip(sharedSkip)
                .Take(pageSize)
                .ToListAsync();
            var myVm = new PagedVm<MyInventoryRowVm>
            {
                Items = ownedList,
                Page = ownedPage,
                PageSize = pageSize,
                TotalCount = ownedTotalCount
            };
            var sharedVm = new PagedVm<MyInventoryRowVm>
            {
                Items = sharedList,
                Page = sharedPage,
                PageSize = pageSize,
                TotalCount = sharedTotalCount
            };
            var vm = new MyInventoriesPageVm
            {
                My = myVm,
                Shared = sharedVm
            };
            return View(vm);
        }
        public async Task<IActionResult> Create()
        {
            var vm = new CreateInventoryVm
            {
                Categories = await _context.Categories
                    .OrderBy(c => c.Name)
                    .Select(c => new CategoryOptionVm
                    {
                        Id = c.Id,
                        Name = c.Name
                    }).ToListAsync()
            };
            return View(vm);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateInventoryVm vm)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            if (!ModelState.IsValid)
            {
                vm.Categories = await _context.Categories
                    .OrderBy(c => c.Name)
                    .Select(c => new CategoryOptionVm
                    {
                        Id = c.Id,
                        Name = c.Name
                    }).ToListAsync();
                return View(vm);
            }
            var entity = new Inventory
            {
                Title = vm.Title,
                Description = vm.Description,
                IsPublic = vm.IsPublic,
                CategoryId = vm.CategoryId,
                OwnerId = userId,
                CreatedAt = DateTime.UtcNow
            };
            _context.Inventories.Add(entity);
            _context.CustomIdElements.AddRange(
                new InventoryCustomIdElement
                {
                    InventoryId = entity.Id,
                    Order = 1,
                    Type = CustomIdElementType.FixedText,
                    Text = "INV-"
                },
                new InventoryCustomIdElement
                {
                    InventoryId = entity.Id,
                    Order = 2,
                    Type = CustomIdElementType.Sequence,
                    Format = "D4"
                }
            );
            var sequence = new InventorySequence
            {
                InventoryId = entity.Id,
                NextValue = 1
            };
            await _context.InventorySequences.AddAsync(sequence);
            await _tagService.SyncInventoryTagsAsync(entity.Id, vm.Tags);
            await _context.SaveChangesAsync();
            await _searchService.IndexInventoryAsync(entity.Id);
            return RedirectToAction("Details", "Inventories", new { id = entity.Id, tab = "items" });
        }
        [HttpGet("Inventories/{inventoryId:guid}/Edit")]
        public async Task<IActionResult> Edit(Guid inventoryId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canManage = await _accessService.CanManageInventory(inventoryId, userId);
            if (!canManage) return NotFound();
            var vm = await _context.Inventories
                .Where(inv => inv.Id == inventoryId)
                .Select(inv => new EditInventoryVm
                {
                    InventoryId = inv.Id,
                    Title = inv.Title,
                    Description = inv.Description,
                    IsPublic = inv.IsPublic,
                    CategoryId = inv.CategoryId,
                    Tags = inv.InventoryTags
                        .Select(it => it.Tag.Name)
                        .ToList(),
                    Version = inv.Version,
                    Categories = _context.Categories
                        .OrderBy(c => c.Name)
                        .Select(c => new CategoryOptionVm
                        {
                            Id = c.Id,
                            Name = c.Name
                        }).ToList()
                }).SingleOrDefaultAsync();
            if (vm == null) return NotFound();
            return View(vm);
        }
        [HttpPost("Inventories/{inventoryId:guid}/Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid inventoryId, EditInventoryVm vm)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canManage = await _accessService.CanManageInventory(inventoryId, userId);
            if (!canManage) return NotFound();
            if (inventoryId != vm.InventoryId) return NotFound();
            if (!ModelState.IsValid)
            {
                await FillEditInventoryVmAsync(vm);
                return View(vm);
            }
            var entity = await _context.Inventories.SingleOrDefaultAsync(inv => inv.Id == inventoryId);
            if (entity == null) return NotFound();
            _context.Entry(entity).Property(x => x.Version).OriginalValue = vm.Version;
            entity.Title = vm.Title.Trim();
            entity.Description = vm.Description?.Trim() ?? "";
            entity.IsPublic = vm.IsPublic;
            entity.CategoryId = vm.CategoryId;
            entity.UpdatedAt = DateTime.UtcNow;
            await _tagService.SyncInventoryTagsAsync(entity.Id, vm.Tags);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError("", "The inventory has been updated by someone else. Please refresh the page and apply your changes again.");
                await FillEditInventoryVmAsync(vm);
                var dbValues = await _context.Inventories
                    .AsNoTracking()
                    .Where(x => x.Id == inventoryId)
                    .Select(x => new { x.Version })
                    .SingleOrDefaultAsync(); 
                vm.Version = dbValues.Version;
                return View(vm);
            }
            await _searchService.IndexInventoryAsync(entity.Id);
            return RedirectToAction(nameof(My));
        }
        private async Task FillEditInventoryVmAsync(EditInventoryVm vm)
        {
            vm.Categories = await _context.Categories
                .OrderBy(c => c.Name)
                .Select(c => new CategoryOptionVm
                {
                    Id = c.Id,
                    Name = c.Name
                })
                .ToListAsync();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(List<Guid> inventoryIds)
        {
            if (inventoryIds == null || inventoryIds.Count == 0)
                return RedirectToAction(nameof(My));
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var inventories = await _context.Inventories
                .Where(inv => inventoryIds.Contains(inv.Id) && inv.OwnerId == userId)
                .ToListAsync();
            if (inventories.Count == 0) return RedirectToAction(nameof(My));
            _context.Inventories.RemoveRange(inventories);
            await _context.SaveChangesAsync();
            await _context.Tags
                .Where(t => !t.InventoryTags.Any())
                .ExecuteDeleteAsync();
            var deletedIds = inventories.Select(inv => inv.Id).ToList();
            await _searchService.RemoveInventoryAsync(deletedIds);
            await _searchService.RemoveInventoryItemsAsync(deletedIds);
            return RedirectToAction(nameof(My));
        }
        private static readonly HashSet<string> AllowedTabs = new(StringComparer.OrdinalIgnoreCase)
        {
            "items", "chat", "settings", "customid", "fields", "access"
        };
        [AllowAnonymous]
        public async Task<IActionResult> Details(Guid id, string tab = "items")
        {
            tab = string.IsNullOrWhiteSpace(tab) ? "items" : tab.Trim();
            if (!AllowedTabs.Contains(tab)) tab = "items";
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");
            var invBase = await _context.Inventories
                .Where(invb => invb.Id == id)
                .Select(invb => new
                {
                    invb.Id,
                    invb.Title,
                    invb.OwnerId
                }).SingleOrDefaultAsync();
            if (invBase == null) return NotFound();
            bool canEditItems = false;
            bool canManageInventory = false;
            if (userId != null)
            {
                canEditItems = await _accessService.CanEditItems(invBase.Id, userId);
                canManageInventory = await _accessService.CanManageInventory(invBase.Id, userId);
            }
            var inv = new InventoryDetailsShellVm
            {
                InventoryId = invBase.Id,
                Title = invBase.Title,
                ActiveTab = tab,
                CanEditItems = canEditItems,
                CanManageInventory = canManageInventory
            };
            switch (tab)
            {
                case "items":
                    inv.Items = await _itemsTabBuilder.BuildAsync(id, invBase.Title, canEditItems, userId);
                    break;
                case "chat":
                    inv.Discussion = await _chatTabBuilder.BuildAsync(id, userId, isAdmin, invBase.OwnerId);
                    break;
                case "customid":
                    inv.CustomId = await _customIdTabBuilder.BuildAsync(id, invBase.Title, canManageInventory);
                    break;
                case "settings":
                    var settings = await _settingsTabBuilder.BuildAsync(id, canManageInventory);
                    if (settings == null) return NotFound();
                    inv.Settings = settings;
                    break;
                case "fields":
                    inv.Fields = await _fieldsTabBuilder.BuildAsync(id, invBase.Title, canManageInventory);
                    break;
                case "access":
                    if (userId == null) return Challenge();
                    if (!canManageInventory) return NotFound();
                    inv.Accesses = await _accessTabBuilder.BuildAsync(id, invBase.Title);
                    break;
            }
            return View(inv);
        }
        [HttpPost("Inventories/{inventoryId:guid}/Settings/InlineUpdate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InlineUpdate(Guid inventoryId, [FromBody] InventoryInlineUpdateDto dto)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();
            var inv = await _context.Inventories
                .Where(x => x.Id == inventoryId)
                .SingleOrDefaultAsync();
            if (inv == null) return NotFound();
            var canManage = await _accessService.CanManageInventory(inventoryId, userId);
            if (!canManage) return Forbid();
            _context.Entry(inv).Property(x => x.Version).OriginalValue = dto.Version;
            var title = (dto.Title ?? "").Trim();
            if (string.IsNullOrWhiteSpace(title))
                return BadRequest(new { error = "Title is required." });
            if (title.Length > 250)
                return BadRequest(new { error = "Title is too long (max 250)." });
            inv.Title = title;
            inv.Description = (dto.Description ?? "").Trim();
            inv.CategoryId = dto.CategoryId;
            inv.IsPublic = dto.IsPublic;
            inv.UpdatedAt = DateTime.UtcNow;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { ok = false, error = "Inventory was updated by someone else. Refresh the page." });
            }
            await _searchService.IndexInventoryAsync(inv.Id);
            return Ok(new
            {
                ok = true,
                version = inv.Version,
                updatedAt = inv.UpdatedAt?.ToString("yyyy-MM-dd HH:mm")
            });
        }
        [HttpPost("Inventories/{inventoryId:guid}/Settings/InlineUpdateTags")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InlineUpdateTags(Guid inventoryId, [FromBody] InventoryInlineTagsDto dto)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();
            var inv = await _context.Inventories
                .Where(x => x.Id == inventoryId)
                .SingleOrDefaultAsync();
            if (inv == null) return NotFound();
            var canManage = await _accessService.CanManageInventory(inventoryId, userId);
            if (!canManage) return Forbid();
            _context.Entry(inv).Property(x => x.Version).OriginalValue = dto.Version;
            var normalized = (dto.Tags ?? new List<string>())
                .Select(t => (t ?? "").Trim().ToLower())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .Take(30)
                .ToList();
            try
            {
                inv.UpdatedAt = DateTime.UtcNow;
                await _tagService.SyncInventoryTagsAsync(inventoryId, normalized);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { ok = false, error = "Inventory was updated by someone else. Refresh the page." });
            }
            await _searchService.IndexInventoryAsync(inv.Id);
            return Ok(new
            {
                ok = true,
                tags = normalized,
                version = inv.Version,
                updatedAt = inv.UpdatedAt?.ToString("yyyy-MM-dd HH:mm")
            });
        }
    }
}
