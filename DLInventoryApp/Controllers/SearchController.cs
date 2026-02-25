using DLInventoryApp.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using static System.Net.Mime.MediaTypeNames;

namespace DLInventoryApp.Controllers
{
    public class SearchController : Controller
    {
        private readonly ISearchService _search;
        public SearchController(ISearchService search)
        {
            _search = search;
        }
        public async Task<IActionResult> Index(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return BadRequest(new { error = "Text is required." });
            var vm = await _search.SearchAsync(query);
            return View(vm);
        }
    }
}
