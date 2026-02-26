using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.ViewModels.Search;
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
        [HttpGet]
        public async Task<IActionResult> Suggest(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
                return PartialView("_SearchSuggest", new SearchResultVm { Query = query });
            var vm = await _search.SearchAsync(query, inventoriesLimit: 5, itemsLimit: 5);
            return PartialView("_SearchSuggest", vm);
        }
        [HttpGet]
        public async Task<IActionResult> Reindex()
        {
            await _search.ReindexAllAsync();
            return Ok("Reindex completed");
        }
    }
}
