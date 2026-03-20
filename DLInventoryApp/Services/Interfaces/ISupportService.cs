using DLInventoryApp.ViewModels.Support;

namespace DLInventoryApp.Services.Interfaces
{
    public interface ISupportService
    {
        Task<string> CreateTicketAsync(SupportTicketVm vm, string userId);
    }
}