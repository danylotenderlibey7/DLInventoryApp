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
        public CustomIdController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, 
            IAccessService accessService, ICustomIdGenerator customIdGenerator)
        {
            _context = context;
            _userManager = userManager;
            _accessService = accessService;
            _customIdGenerator = customIdGenerator;
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
                    Format = e.Format
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
                    format = entity.Format
                },
                preview
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
            string preview;
            try { preview = (await _customIdGenerator.PreviewAsync(inventoryId)).CustomId; }
            catch { preview = "(no template)"; }
            return Ok(new { ok = true, preview });
        }
        [HttpPost("Reorder")]
        public async Task<IActionResult> Reorder(Guid inventoryId, [FromBody] List<int> orderedIds)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();
            var canManage = await _accessService.CanManageInventory(inventoryId, userId);
            if (!canManage) return NotFound();
            var elements = await _context.CustomIdElements
                .Where(e => e.InventoryId == inventoryId)
                .ToListAsync();
            //if (elements.Count != orderedIds.Count) return BadRequest();
            var elementIds = elements.Select(e => e.Id).OrderBy(x => x);
            var incomingIds = orderedIds.OrderBy(x => x);
            if (!elementIds.SequenceEqual(incomingIds)) return BadRequest();
            int temp = 1000;
            for (int i = 0; i < elements.Count(); i++) 
            {
                elements[i].Order = temp + i;
            }
            await _context.SaveChangesAsync();
            for (int i = 0; i < orderedIds.Count(); i++)
            {
                var element = elements.Single(e => e.Id == orderedIds[i]);
                element.Order = i + 1;
            }
            await _context.SaveChangesAsync();
            return NoContent();
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
            entity.Type = dto.Type;
            entity.Text = string.IsNullOrWhiteSpace(dto.Text) ? null : dto.Text.Trim();
            entity.Format = string.IsNullOrWhiteSpace(dto.Format) ? null : dto.Format.Trim();
            await _context.SaveChangesAsync();
            string preview;
            try { preview = (await _customIdGenerator.PreviewAsync(inventoryId)).CustomId; }
            catch { preview = "(no template)"; }
            return Ok(new { ok = true, preview });
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
            for (int i = 0; i < elements.Count; i++)
                elements[i].Order = i + 1;
            await _context.SaveChangesAsync();
        }
    }
}
