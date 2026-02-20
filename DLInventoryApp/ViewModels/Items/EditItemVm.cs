using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace DLInventoryApp.ViewModels.Items
{
    public class EditItemVm
    {
        public Guid InventoryId { get; set; }
        public Guid ItemId { get; set; }
        public string InventoryTitle { get; set; } = string.Empty;
        public string CustomId { get; set; } = string.Empty;
        public List<FieldValueInputVm> Fields { get; set; } = new();
        public bool CanWrite { get; set; }
    }
}
