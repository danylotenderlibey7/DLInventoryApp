using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace DLInventoryApp.ViewModels.Items
{
    public class CreateItemVm
    {
        public Guid InventoryId { get; set; }
        public string InventoryTitle { get; set; } = string.Empty;
        public string CustomId { get; set; } = string.Empty;
    }
}
