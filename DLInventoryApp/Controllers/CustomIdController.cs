using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.ViewModels.CustomId;
using DLInventoryApp.ViewModels.CustomId.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Controllers
{
    [Authorize]
    [Route("Inventories/{inventoryId:guid}/CustomId")]
    public class CustomIdController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAccessService _accessService; 
        private readonly ICustomIdGenerator _customIdGenerator;
        private readonly ISearchService _searchService;
        public CustomIdController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, 
            IAccessService accessService, ICustomIdGenerator customIdGenerator, ISearchService searchService)
        {
            _context = context;
            _userManager = userManager;
            _accessService = accessService;
            _customIdGenerator = customIdGenerator;
            _searchService = searchService;
        }
        public async Task<IActionResult> Index(Guid inventoryId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canManage = await _accessService.CanManageInventory(inventoryId, userId);
            var invTitle = await _context.Inventories
                .Where(x => x.Id == inventoryId)
                .Select(x => x.Title)
                .SingleOrDefaultAsync();
            if (invTitle == null) return NotFound();
            var elements = await _context.CustomIdElements
                .Where(e => e.InventoryId == inventoryId)
                .OrderBy(e => e.Order)
                .Select(e => new CustomIdElementRowVm
                {
                    Id = e.Id,
                    Order = e.Order,
                    Type = e.Type,
                    Text = e.Text,
                    Format = e.Format,
                    Version = e.Version
                }).ToListAsync();
            string preview = "";
            try
            {
                var result = await _customIdGenerator.PreviewAsync(inventoryId);
                preview = result.CustomId;
            }
            catch
            {
                preview = "(no template)";
            }
            var vm = new CustomIdIndexVm
            {
                InventoryId = inventoryId,
                InventoryTitle = invTitle,
                CanManage = canManage,
                Preview = preview,
                Elements = elements
            };
            return View(vm);
        }
        [HttpPost("Add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(Guid inventoryId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canManage = await _accessService.CanManageInventory(inventoryId, userId);
            if (!canManage) return NotFound();
            var maxOrder = await _context.CustomIdElements
                .Where(e => e.InventoryId == inventoryId)
                .MaxAsync(e => (int?)e.Order) ?? 0;
            var entity = new InventoryCustomIdElement
            {
                InventoryId = inventoryId,
                Order = maxOrder + 1,
                Type = CustomIdElementType.FixedText,
                Text = null,
                Format = null
            };
            _context.CustomIdElements.Add(entity);
            await _context.SaveChangesAsync();
            await NormalizeOrdersAsync(inventoryId); 
            var versions = await _context.CustomIdElements
                .Where(e => e.InventoryId == inventoryId)
                .Select(e => new
                {
                    id = e.Id,
                    version = e.Version
                }).ToListAsync();
            await _searchService.ReindexInventoryItemsAsync(inventoryId);
            string preview;
            try { preview = (await _customIdGenerator.PreviewAsync(inventoryId)).CustomId; }
            catch { preview = "(no template)"; }
            return Ok(new
            {
                element = new
                {
                    id = entity.Id,
                    order = entity.Order,
                    type = (int)entity.Type,
                    text = entity.Text,
                    format = entity.Format,
                    version = entity.Version
                },
                preview,
                versions
            });
        }
        [HttpPost("{id:int}/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid inventoryId, int id)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canManage = await _accessService.CanManageInventory(inventoryId, userId);
            if (!canManage) return NotFound();
            var entity = await _context.CustomIdElements
                .Where(e => e.InventoryId == inventoryId && e.Id == id)
                .SingleOrDefaultAsync();
            if (entity == null) return NotFound();
            _context.CustomIdElements.Remove(entity);
            await _context.SaveChangesAsync();
            await NormalizeOrdersAsync(inventoryId);
            var versions = await _context.CustomIdElements
                .Where(e => e.InventoryId == inventoryId)
                .Select(e => new
                {
                    id = e.Id,
                    version = e.Version
                }).ToListAsync();
            await _searchService.ReindexInventoryItemsAsync(inventoryId);
            string preview;
            try 
            { 
                preview = (await _customIdGenerator.PreviewAsync(inventoryId)).CustomId; 
            }
            catch 
            { 
                preview = "(no template)"; 
            }
            return Ok(new { ok = true, preview, versions });
        }
        [HttpPost("Reorder")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reorder(Guid inventoryId, [FromBody] List<int> orderedIds)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();
            var invBase = await _context.Inventories
                .Where(x => x.Id == inventoryId)
                .Select(x => new { x.Id })
                .SingleOrDefaultAsync();
            if (invBase == null) return NotFound();
            var canManage = await _accessService.CanManageInventory(inventoryId, userId);
            if (!canManage) return NotFound();
            if (orderedIds == null || orderedIds.Count == 0)
                return BadRequest(new { ok = false, error = "Empty reorder payload." });
            var elements = await _context.CustomIdElements
                .Where(e => e.InventoryId == inventoryId)
                .ToListAsync();
            if (elements.Count != orderedIds.Count)
                return BadRequest(new { ok = false, error = "Invalid reorder payload." });
            var current = elements.Select(e => e.Id).OrderBy(x => x).ToList();
            var incoming = orderedIds.OrderBy(x => x).ToList();
            if (!current.SequenceEqual(incoming))
                return BadRequest(new { ok = false, error = "Invalid reorder payload." });
            try
            {
                var tempBase = 1000;
                for (int i = 0; i < orderedIds.Count; i++)
                {
                    var element = elements.Single(x => x.Id == orderedIds[i]);
                    element.Order = tempBase + i + 1;
                }
                await _context.SaveChangesAsync();
                for (int i = 0; i < orderedIds.Count; i++)
                {
                    var element = elements.Single(x => x.Id == orderedIds[i]);
                    element.Order = i + 1;
                }
                await _context.SaveChangesAsync();
                string preview;
                try
                {
                    preview = (await _customIdGenerator.PreviewAsync(inventoryId)).CustomId;
                }
                catch
                {
                    preview = "(no template)";
                }
                return Ok(new
                {
                    ok = true,
                    preview,
                    versions = elements.Select(e => new
                    {
                        id = e.Id,
                        version = e.Version
                    }).ToList()
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { ok = false, error = "Custom ID elements were modified by another user. Reload the page." });
            }
        }
        [HttpPost("{id:int}/InlineUpdate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InlineUpdate(Guid inventoryId, int id, [FromBody] CustomIdInlineUpdateDto dto)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canManage = await _accessService.CanManageInventory(inventoryId, userId);
            if (!canManage) return NotFound();
            var entity = await _context.CustomIdElements
                .Where(e => e.InventoryId == inventoryId && e.Id == id)
                .SingleOrDefaultAsync();
            if (entity == null) return NotFound();
            _context.Entry(entity).Property(x => x.Version).OriginalValue = dto.Version;
            entity.Type = dto.Type;
            entity.Text = string.IsNullOrWhiteSpace(dto.Text) ? null : dto.Text.Trim();
            entity.Format = string.IsNullOrWhiteSpace(dto.Format) ? null : dto.Format.Trim();
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { ok = false, error = "Custom ID element was updated by someone else. Refresh the page." });
            }
            await _searchService.ReindexInventoryItemsAsync(inventoryId);
            string preview;
            try
            {
                preview = (await _customIdGenerator.PreviewAsync(inventoryId)).CustomId;
            }
            catch
            {
                preview = "(no template)";
            }
            return Ok(new { ok = true, preview, version = entity.Version });
        }
        [HttpGet("Preview")]
        public async Task<IActionResult> Preview(Guid inventoryId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canManage = await _accessService.CanManageInventory(inventoryId, userId);
            if (!canManage) return NotFound();
            string preview;
            try { preview = (await _customIdGenerator.PreviewAsync(inventoryId)).CustomId; }
            catch { preview = "(no template)"; }
            return Ok(new { preview });
        }
        private async Task NormalizeOrdersAsync(Guid inventoryId)
        {
            var elements = await _context.CustomIdElements
                .Where(e => e.InventoryId == inventoryId)
                .OrderBy(e => e.Order)
                .ToListAsync();
            var changed = false;
            for (int i = 0; i < elements.Count; i++)
            {
                var newOrder = i + 1;
                if (elements[i].Order != newOrder)
                {
                    elements[i].Order = newOrder;
                    changed = true;
                }
            }
            if (changed) await _context.SaveChangesAsync();
        }
    }
}
