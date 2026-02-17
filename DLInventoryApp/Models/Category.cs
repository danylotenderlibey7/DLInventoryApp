using System.ComponentModel.DataAnnotations;

namespace DLInventoryApp.Models
{
    public class Category
    {
        public int Id { get; set; }
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = null!;
    }
}
