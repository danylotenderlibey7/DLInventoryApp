using DLInventoryApp.ViewModels.Items.Search;

namespace DLInventoryApp.Services.Interfaces
{
    public interface ISearchService
    {
        Task<SearchResultVm> SearchAsync(string query, int inventoriesLimit = 5, int itemsLimit = 20); 
        Task ReindexAllAsync();
        Task IndexInventoryAsync(Guid inventoryId);
        Task RemoveInventoryAsync(List<Guid> inventoryId);
        Task RemoveInventoryItemsAsync(List<Guid> inventoryId);
        Task ReindexInventoryItemsAsync(Guid inventoryId);
        Task IndexItemAsync(Guid itemId);
        Task RemoveItemsAsync(List<Guid> itemIds);
    }
}
