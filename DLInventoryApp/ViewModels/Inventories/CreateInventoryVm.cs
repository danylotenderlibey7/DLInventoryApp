using System.ComponentModel.DataAnnotations;

namespace DLInventoryApp.ViewModels.Inventories
{
    public class CreateInventoryVm
    {
        [Required]
        [MaxLength(250)]
        public string Title { get; set; } = null!;
        [MaxLength(1000)]
        public string? Description { get; set; }
        public int? CategoryId { get; set; }
        public bool IsPublic { get; set; }
    }
}
