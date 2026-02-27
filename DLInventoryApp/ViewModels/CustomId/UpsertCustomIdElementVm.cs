using DLInventoryApp.Models;
using System.ComponentModel.DataAnnotations;

namespace DLInventoryApp.ViewModels.CustomId
{
    public class UpsertCustomIdElementVm
    {
        public Guid InventoryId { get; set; }
        public int? Id { get; set; } 
        [Range(1, 100000)]
        public int Order { get; set; }
        public CustomIdElementType Type { get; set; }
        [MaxLength(200)]
        public string? Text { get; set; }
        [MaxLength(50)]
        public string? Format { get; set; }
    }
}
