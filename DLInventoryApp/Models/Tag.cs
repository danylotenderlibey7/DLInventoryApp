using System.ComponentModel.DataAnnotations;
namespace DLInventoryApp.Models
{
    public class Tag
    {
        public int Id { get; set; }
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = null!;
        public List<InventoryTag> InventoryTags { get; set; } = new();
    }
}
