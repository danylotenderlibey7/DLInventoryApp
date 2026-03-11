using DLInventoryApp.Data;
using DLInventoryApp.ViewModels.Common.Pagination;
using DLInventoryApp.ViewModels.Tags;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Controllers
{
    [Route("Tags")]
    public class TagsController : Controller
    {
        private readonly ApplicationDbContext _context;
        public TagsController(ApplicationDbContext context)
        {
            _context = context;
        }
        [HttpGet("")]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 8)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 5 or > 50 ? 8 : pageSize;
            var query = _context.Tags
                .Select(t => new TagListItemVm
                {
                    Name = t.Name,
                    InventoriesCount = t.InventoryTags.Count
                })
                .OrderByDescending(t => t.InventoriesCount)
                .ThenBy(t => t.Name);
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages > 0 && page > totalPages) page = totalPages;
            var skip = (page - 1) * pageSize;
            var items = await query
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();
            var vm = new PagedVm<TagListItemVm>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
            return View(vm);
        }
        [HttpGet("Suggest")]
        public async Task<IActionResult> Suggest(string prefix)
        {
            if (string.IsNullOrEmpty(prefix) || prefix.Length < 2) return Json(new List<string>());
            prefix = prefix.ToLower();
            var tags = await _context.Tags
                .Where(t => t.Name.StartsWith(prefix))
                .OrderBy(t => t.Name)
                .Select(t => t.Name)
                .Take(10).ToListAsync();
            return Json(tags);
        }
    }
}
