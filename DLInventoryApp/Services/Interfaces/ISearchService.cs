using DLInventoryApp.ViewModels.Search;

namespace DLInventoryApp.Services.Interfaces
{
    public interface ISearchService
    {
        Task<SearchResultVm> SearchAsync(string query, int inventoriesLimit = 5, int itemsLimit = 20); 
        Task ReindexAllAsync();
        Task IndexInventoryAsync(Guid inventoryId);
        Task RemoveInventoryAsync(Guid inventoryId);
        Task IndexItemAsync(Guid itemId);
        Task RemoveItemAsync(Guid itemId);
    }
}
