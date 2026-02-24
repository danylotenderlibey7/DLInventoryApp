using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace DLInventoryApp.Models
{
    public class ApplicationUser : IdentityUser
    {
        public bool IsBlocked { get; set; } = false;
        [MaxLength(10)]
        public string PreferredLanguage { get; set; } = "en";
        [MaxLength(20)]
        public string PreferredTheme { get; set; } = "light";
        public List<ItemLike> LikedItems { get; set; } = new();
    }
}
