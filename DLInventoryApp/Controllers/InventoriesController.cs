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
using DLInventoryApp.ViewModels.Inventories.Tabs.Access;
using DLInventoryApp.ViewModels.Inventories.Inline;
using DLInventoryApp.ViewModels.Inventories.Tabs.Settings.Dtos;
using DLInventoryApp.ViewModels.Common.Pagination;
using DLInventoryApp.ViewModels.Categories;

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
        private readonly IMarkdownService _markdown;
        private readonly ICustomIdGenerator _customIdGenerator;
        private readonly CustomIdTabBuilder _customIdTabBuilder;
        private readonly FieldsTabBuilder _fieldsTabBuilder;
        private readonly SettingsTabBuilder _settingsTabBuilder;
        private readonly AccessTabBuilder _accessTabBuilder;
        private readonly ChatTabBuilder _chatTabBuilder;
        private readonly ItemsTabBuilder _itemsTabBuilder;
        public InventoriesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            ITagService tagService, IAccessService accessService, ISearchService searchService, IMarkdownService markdown,
            ICustomIdGenerator customIdGenerator, CustomIdTabBuilder customIdTabBuilder, FieldsTabBuilder fieldsTabBuilder,
            SettingsTabBuilder settingsTabBuilder, AccessTabBuilder accessTabBuilder, ChatTabBuilder chatTabBuilder,
            ItemsTabBuilder itemsTabBuilder)
        {
            _context = context;
            _userManager = userManager;
            _tagService = tagService;
            _accessService = accessService;
            _searchService = searchService;
            _markdown = markdown;
            _customIdGenerator = customIdGenerator;
            _customIdTabBuilder = customIdTabBuilder;
            _fieldsTabBuilder = fieldsTabBuilder;
            _settingsTabBuilder = settingsTabBuilder;
            _accessTabBuilder = accessTabBuilder;
            _chatTabBuilder = chatTabBuilder;
            _itemsTabBuilder = itemsTabBuilder;
        }
        [AllowAnonymous]
        public async Task<IActionResult> Index(string? tag, string view = "latest", int page = 1, int pageSize = 5)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 5 or > 50 ? 10 : pageSize;
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
                    OwnerEmail = inv.Owner.Email,
                    CreatedAt = inv.CreatedAt,
                    UpdatedAt = inv.UpdatedAt,
                    ItemsCount = inv.Items.Count(),
                    CategoryName = inv.Category != null ? inv.Category.Name : null,
                    Tags = inv.InventoryTags
                    .Select(it => it.Tag.Name)
                    .ToList()
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
                    Tags = inv.InventoryTags
                        .Select(it => it.Tag.Name)
                        .ToList()
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
                    OwnerEmail = inv.Owner.Email,
                    CreatedAt = inv.CreatedAt,
                    UpdatedAt = inv.UpdatedAt,
                    ItemsCount = inv.Items.Count(),
                    CategoryName = inv.Category != null ? inv.Category.Name : null,
                    Tags = inv.InventoryTags
                        .Select(it => it.Tag.Name)
                        .ToList()
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
            if (!ModelState.IsValid) return View(vm);
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
            await _context.SaveChangesAsync();
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
            await _context.SaveChangesAsync();
            var sequence = new InventorySequence
            {
                InventoryId = entity.Id,
                NextValue = 1
            };
            await _context.InventorySequences.AddAsync(sequence);
            await _context.SaveChangesAsync();
            await _tagService.SyncInventoryTagsAsync(entity.Id, vm.Tags);
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
            entity.Title = vm.Title;
            entity.Description = vm.Description;
            entity.IsPublic = vm.IsPublic;
            entity.CategoryId = vm.CategoryId;
            entity.UpdatedAt = DateTime.UtcNow;
            _context.Entry(entity).Property(x => x.Version).OriginalValue = vm.Version;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError("", "The inventory has been updated by someone else. Please refresh the page and apply your changes again.");
                await FillEditInventoryVmAsync(vm);
                return View(vm);
            }
            await _tagService.SyncInventoryTagsAsync(entity.Id, vm.Tags);
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
            foreach (var inv in inventories)
                await _searchService.RemoveInventoryAsync(inv.Id);
            return RedirectToAction(nameof(My));
        }
        private static readonly HashSet<string> AllowedTabs = new(StringComparer.OrdinalIgnoreCase)
        {
            "items", "chat", "settings", "customid", "fields", "access", "stats", "export"
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
                case "export":
                    break;
                case "stats":
                    break;
            }
            return View(inv);
        }
        [HttpPost("Inventories/{inventoryId:guid}/Settings/InlineUpdateField")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SettingsInlineUpdate(Guid inventoryId, [FromBody] InventorySettingsInlineUpdateDto dto)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();
            var invBase = await _context.Inventories
                .Where(x => x.Id == inventoryId)
                .Select(x => new { x.Id, x.OwnerId })
                .SingleOrDefaultAsync();
            if (invBase == null) return NotFound();
            var isAdmin = User.Identity?.IsAuthenticated == true && User.IsInRole("Admin");
            var canManage = await _accessService.CanManageInventory(inventoryId, userId);
            if (!canManage) return Forbid();
            var field = (dto.Field ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(field))
                return BadRequest(new { ok = false, error = "Field is required." });
            var entity = await _context.Inventories.SingleOrDefaultAsync(x => x.Id == inventoryId);
            if (entity == null) return NotFound();
            if (dto.Version != null)
                _context.Entry(entity).Property(x => x.Version).OriginalValue = dto.Version;

            try
            {
                switch (field)
                {
                    case "title":
                        {
                            var v = (dto.Value?.ToString() ?? "").Trim();
                            if (string.IsNullOrWhiteSpace(v)) return BadRequest(new { ok = false, error = "Title is required." });
                            if (v.Length > 250) return BadRequest(new { ok = false, error = "Title is too long." });
                            entity.Title = v;
                            break;
                        }
                    case "description":
                        {
                            var v = (dto.Value?.ToString() ?? "");
                            if (v.Length > 1000) return BadRequest(new { ok = false, error = "Description is too long." });
                            entity.Description = v;
                            break;
                        }
                    case "ispublic":
                        {
                            bool v;
                            if (dto.Value is bool b) v = b;
                            else
                            {
                                var s = (dto.Value?.ToString() ?? "").Trim().ToLowerInvariant();
                                v = (s == "true" || s == "1" || s == "yes");
                            }
                            entity.IsPublic = v;
                            break;
                        }
                    case "tags":
                        {
                            var raw = (dto.Value?.ToString() ?? "");
                            var tags = raw
                                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(x => x.Trim().ToLowerInvariant())
                                .Where(x => !string.IsNullOrWhiteSpace(x))
                                .Distinct()
                                .Take(30)
                                .ToList();
                            entity.UpdatedAt = DateTime.UtcNow;
                            await _context.SaveChangesAsync();

                            await _tagService.SyncInventoryTagsAsync(entity.Id, tags);
                            await _searchService.IndexInventoryAsync(entity.Id);
                            var normalized = string.Join(", ", tags);
                            return Ok(new
                            {
                                ok = true,
                                field = "tags",
                                display = normalized,
                                tags,
                                updatedAt = (entity.UpdatedAt ?? entity.CreatedAt).ToString("yyyy-MM-dd HH:mm"),
                                version = entity.Version
                            });
                        }
                    default:
                        return BadRequest(new { ok = false, error = "Unknown field." });
                }
                entity.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await _searchService.IndexInventoryAsync(entity.Id);
                var display = field switch
                {
                    "title" => entity.Title,
                    "description" => entity.Description,
                    "ispublic" => entity.IsPublic ? "Yes" : "No",
                    _ => ""
                };
                return Ok(new
                {
                    ok = true,
                    field,
                    display,
                    updatedAt = (entity.UpdatedAt ?? entity.CreatedAt).ToString("yyyy-MM-dd HH:mm"),
                    version = entity.Version
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { ok = false, error = "Concurrency conflict. Refresh the page." });
            }
        }
        [HttpPost("Inventories/{inventoryId:guid}/Settings/InlineUpdate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InlineUpdate(Guid inventoryId, [FromBody] InventoryInlineUpdateDto dto)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();
            var isAdmin = User.IsInRole("Admin");
            var inv = await _context.Inventories
                .Where(x => x.Id == inventoryId)
                .SingleOrDefaultAsync();
            if (inv == null) return NotFound();
            var canManage = await _accessService.CanManageInventory(inventoryId, userId);
            if (!canManage) return Forbid();
            if (!string.IsNullOrWhiteSpace(dto.Version))
                _context.Entry(inv).Property(x => x.Version).OriginalValue = Convert.FromBase64String(dto.Version);
            var title = (dto.Title ?? "").Trim();
            if (string.IsNullOrWhiteSpace(title))
                return BadRequest(new { error = "Title is required." });
            if (title.Length > 250)
                return BadRequest(new { error = "Title is too long (max 250)." });
            inv.Title = title;
            inv.Description = (dto.Description ?? "").Trim();
            inv.IsPublic = dto.IsPublic;
            inv.UpdatedAt = DateTime.UtcNow;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { error = "Inventory was updated by someone else. Refresh the page." });
            }
            await _searchService.IndexInventoryAsync(inv.Id);
            return Ok(new
            {
                ok = true,
                version = Convert.ToBase64String(inv.Version ?? Array.Empty<byte>()),
                updatedAt = inv.UpdatedAt?.ToString("yyyy-MM-dd HH:mm")
            });
        }
        [HttpPost("Inventories/{inventoryId:guid}/Settings/InlineUpdateTags")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InlineUpdateTags(Guid inventoryId, [FromBody] InventoryInlineTagsDto dto)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();
            var isAdmin = User.IsInRole("Admin");
            var inv = await _context.Inventories
                .Where(x => x.Id == inventoryId)
                .SingleOrDefaultAsync();
            if (inv == null) return NotFound();
            var canManage = await _accessService.CanManageInventory(inventoryId, userId);
            if (!canManage) return Forbid();
            if (!string.IsNullOrWhiteSpace(dto.Version))
                _context.Entry(inv).Property(x => x.Version).OriginalValue = Convert.FromBase64String(dto.Version);
            var normalized = (dto.Tags ?? new List<string>())
                .Select(t => (t ?? "").Trim().ToLower())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .Take(30)
                .ToList();
            try
            {
                await _tagService.SyncInventoryTagsAsync(inventoryId, normalized);

                inv.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { error = "Inventory was updated by someone else. Refresh the page." });
            }
            await _searchService.IndexInventoryAsync(inv.Id);
            return Ok(new
            {
                ok = true,
                tags = normalized,
                version = Convert.ToBase64String(inv.Version ?? Array.Empty<byte>()),
                updatedAt = inv.UpdatedAt?.ToString("yyyy-MM-dd HH:mm")
            });
        }
    }
}
