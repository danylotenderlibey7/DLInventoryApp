using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace DLInventoryApp.Models
{
    public class ApplicationUser : IdentityUser
    {
        public bool IsBlocked { get; set; } = false;
        [MaxLength(2)]
        public string PreferredLanguage { get; set; } = "en";
        public string PreferredTheme { get; set; } = "light";
    }
}
