using DLInventoryApp.ViewModels.Common.Pagination;

namespace DLInventoryApp.ViewModels.Inventories.Pages
{
    public class MyInventoriesPageVm
    {
        public PagedVm<MyInventoryRowVm> My { get; set; } = new();
        public PagedVm<MyInventoryRowVm> Shared { get; set; } = new();
    }
}