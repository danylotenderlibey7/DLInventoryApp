using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace DLInventoryApp.Models
{
    public class ApplicationUser : IdentityUser
    {
        public bool IsBlocked { get; set; } = false;
        public List<ItemLike> LikedItems { get; set; } = new();
    }
}
