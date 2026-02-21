using DLInventoryApp.Data;
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
