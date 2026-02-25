using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.ViewModels.Inventories;
using DLInventoryApp.ViewModels.Items;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

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
        public InventoriesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, 
            ITagService tagService, IAccessService accessService, ISearchService searchService)
        {
            _context = context;
            _userManager = userManager;
            _tagService = tagService;
            _accessService = accessService;
            _searchService = searchService;
        }
        [AllowAnonymous]
        public async Task<IActionResult> Index(string? tag)
        {
            var query = _context.Inventories.AsQueryable(); 
            if (!string.IsNullOrWhiteSpace(tag))
            {
                tag = tag.Trim().ToLower();
                query = query.Where(inv =>
                    inv.InventoryTags.Any(it => it.Tag.Name == tag)
                );
            }
            var list = await query
                .Select(inv => new MyInventoryRowVm
                {
                    Id = inv.Id,
                    Title = inv.Title,
                    CreatedAt = inv.CreatedAt,
                    UpdatedAt = inv.UpdatedAt,
                    ItemsCount = inv.Items.Count(),
                    CategoryName = inv.Category != null ? inv.Category.Name : null,
                    Tags = inv.InventoryTags
                    .Select(it => it.Tag.Name) 
                    .ToList()
                })
                .OrderByDescending(vm => vm.UpdatedAt ?? vm.CreatedAt)
                .ToListAsync();

            return View(list);
        }
        public async Task<IActionResult> My()
        {
            var userId = _userManager.GetUserId(User);
            var list = await _context.Inventories
                .Where(inv => inv.OwnerId == userId)
                .Select(inv => new MyInventoryRowVm
                {
                    Id = inv.Id,
                    Title = inv.Title,
                    CreatedAt = inv.CreatedAt,
                    UpdatedAt = inv.UpdatedAt,
                    ItemsCount = inv.Items.Count(),
                    CategoryName = inv.Category != null ? inv.Category.Name : null,
                    Tags = inv.InventoryTags
                    .Select(it => it.Tag.Name)
                    .ToList()
                })
                .OrderByDescending(vm => vm.UpdatedAt ?? vm.CreatedAt)
                .ToListAsync();
            return View(list);
        }
        public IActionResult Create()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateInventoryVm vm)
        {
            if (!ModelState.IsValid) return View(vm);
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
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
            await _tagService.SyncInventoryTagsAsync(entity.Id, vm.Tags);
            await _searchService.IndexInventoryAsync(entity.Id);
            return RedirectToAction(nameof(My));
        }
        [HttpGet("Inventories/{inventoryId:guid}/Edit")]
        public async Task<IActionResult> Edit(Guid inventoryId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canWrite = await _accessService.CanWriteInventory(inventoryId, userId);
            if (!canWrite) return NotFound(); 
            var vm = await _context.Inventories
                .Where(inv => inv.Id == inventoryId)
                .Select(inv => new EditInventoryVm
                {
                    InventoryId = inv.Id,
                    Title=inv.Title,
                    Description = inv.Description,
                    IsPublic = inv.IsPublic,
                    //CategoryId = inv.CategoryId,
                    Tags = inv.InventoryTags.Select(it=>it.Tag.Name).ToList()
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
            var canWrite = await _accessService.CanWriteInventory(inventoryId, userId);
            if (!canWrite) return NotFound();
            if (inventoryId != vm.InventoryId) return NotFound();
            if (!ModelState.IsValid) return View(vm);
            var entity = await _context.Inventories
                .SingleOrDefaultAsync(inv => inv.Id == inventoryId);
            if (entity == null) return NotFound();
            entity.Title = vm.Title;
            entity.Description = vm.Description;
            entity.IsPublic = vm.IsPublic;
            //entity.CategoryId = vm.CategoryId;
            entity.UpdatedAt = DateTime.UtcNow;
            _context.Entry(entity)
                .Property(x => x.Version).OriginalValue = vm.Version;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                ModelState.AddModelError("", "The inventory has been updated by someone else. Please refresh the page and apply your changes again.");
                return View(vm);
            }
            await _tagService.SyncInventoryTagsAsync(entity.Id, vm.Tags);
            return RedirectToAction(nameof(Details), new { id = entity.Id });
        }
        [AllowAnonymous]
        public async Task<IActionResult> Details(Guid id)
        {
            var userId = _userManager.GetUserId(User);
            var vm = await _context.Inventories
                .Where(inv => inv.Id == id)
                .Select(inv => new InventoryDetailsVm
                {
                    Id = inv.Id,
                    Title = inv.Title,
                    Description = inv.Description,
                    IsPublic = inv.IsPublic,
                    CategoryName = inv.Category != null ? inv.Category.Name : null,
                    ItemsCount = inv.Items.Count(),
                    CreatedAt = inv.CreatedAt,
                    UpdatedAt = inv.UpdatedAt,
                    Tags = inv.InventoryTags.Select(it => it.Tag.Name).ToList()
                }).SingleOrDefaultAsync();
            if (vm == null) return NotFound();
            return View(vm);
        }
        [HttpGet("Inventories/{inventoryId}/Access")]
        public async Task<IActionResult> Access(Guid inventoryId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var inventory = await _context.Inventories
                .Where(inv => inv.Id == inventoryId && inv.OwnerId == userId)
                .Select(inv => new
                {
                    inv.Id,
                    inv.Title
                }).SingleOrDefaultAsync();
            if (inventory == null) return NotFound();
            var users = await _context.InventoryWriteAccesses
                .Where(x => x.InventoryId == inventoryId)
                .Select(x => new AccessUserVm
                {
                    UserId = x.UserId,
                    Email = x.User.Email!
                }).ToListAsync();
            var vm = new InventoryAccessVm
            {
                InventoryId = inventoryId,
                InventoryTitle = inventory.Title,
                Users = users
            };
            return View(vm);
        }
        [HttpPost("Inventories/{inventoryId:guid}/Access/Add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAccess(Guid inventoryId, InventoryAccessVm vm)
        {
            var ownerId = _userManager.GetUserId(User);
            if (ownerId == null) return Challenge();
            var inventory = await _context.Inventories
                .Where(inv => inv.Id == inventoryId && inv.OwnerId == ownerId)
                .Select(inv => new { inv.Id, inv.Title })
                .SingleOrDefaultAsync();
            if (inventory == null) return NotFound();
            async Task<InventoryAccessVm> RebuildVmAsync()
            {
                var users = await _context.InventoryWriteAccesses
                    .Where(x => x.InventoryId == inventoryId)
                    .Select(x => new AccessUserVm
                    {
                        UserId = x.UserId,
                        Email = x.User.Email!
                    }).ToListAsync();
                return new InventoryAccessVm
                {
                    InventoryId = inventoryId,
                    InventoryTitle = inventory.Title,
                    Users = users,
                    NewUserEmail = vm.NewUserEmail
                };
            }
            var email = (vm.NewUserEmail ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError(nameof(vm.NewUserEmail), "Email is required.");
                return View("Access", await RebuildVmAsync());
            }
            var user = await _context.Users
                .Where(u => u.Email != null && u.Email.ToLower() == email.ToLower())
                .Select(u => new { u.Id, u.Email })
                .SingleOrDefaultAsync();
            if (user == null)
            {
                ModelState.AddModelError(nameof(vm.NewUserEmail), "User with this email was not found.");
                return View("Access", await RebuildVmAsync());
            }
            if (user.Id == ownerId)
            {
                ModelState.AddModelError(nameof(vm.NewUserEmail), "Owner already has full access.");
                return View("Access", await RebuildVmAsync());
            }
            var alreadyExists = await _context.InventoryWriteAccesses
                .AnyAsync(x => x.InventoryId == inventoryId && x.UserId == user.Id);
            if (alreadyExists)
            {
                ModelState.AddModelError(nameof(vm.NewUserEmail), "This user already has write access.");
                return View("Access", await RebuildVmAsync());
            }
            _context.InventoryWriteAccesses.Add(new InventoryWriteAccess
            {
                InventoryId = inventoryId,
                UserId = user.Id
            });
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Access), new { inventoryId });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAccess(Guid inventoryId, string userId)
        {
            var ownerId = _userManager.GetUserId(User);
            if (ownerId == null) return Challenge();
            var inventory = await _context.Inventories
                .Where(inv => inv.Id == inventoryId && inv.OwnerId == ownerId)
                .Select(inv => new { inv.Id, inv.Title })
                .SingleOrDefaultAsync();
            if (inventory == null) return NotFound();
            var access = await _context.InventoryWriteAccesses
                .SingleOrDefaultAsync(x => x.InventoryId == inventoryId && x.UserId == userId);
            if (access != null)
            {
                _context.InventoryWriteAccesses.Remove(access);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Access), new { inventoryId });
        }
    }
}
