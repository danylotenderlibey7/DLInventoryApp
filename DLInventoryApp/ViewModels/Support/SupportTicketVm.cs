using System.ComponentModel.DataAnnotations;

namespace DLInventoryApp.ViewModels.Support
{
    public class SupportTicketVm
    {
        public string Summary { get; set; } = null!;
        public string Priority { get; set; } = "Average";
        public string? CurrentUrl { get; set; }
        public Guid? InventoryId { get; set; }
    }
}