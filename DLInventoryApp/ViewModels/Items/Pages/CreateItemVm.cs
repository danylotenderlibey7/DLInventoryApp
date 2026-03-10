using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace DLInventoryApp.ViewModels.Items.Pages
{
    public class CreateItemVm
    {
        public Guid InventoryId { get; set; }
        public string InventoryTitle { get; set; } = string.Empty;
        public string? CustomId { get; set; }
        public string? PreviewCustomId { get; set; }
        public List<FieldValueInputVm> Fields { get; set; } = new(); 
        public bool CanEditItem { get; set; }
    }
}
