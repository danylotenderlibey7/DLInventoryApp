namespace DLInventoryApp.ViewModels.Admin
{
    public class UserAdminVm
    {
        public string Id { get; set; } = null!;
        public string Email { get; set; } = null!;
        public bool EmailConfirmed { get; set; }
        public bool IsBlocked { get; set; }
        public bool IsAdmin { get; set; }
    }
}
