namespace DLInventoryApp.Services.Options
{
    public class OneDriveOptions
    {
        public const string Section = "OneDrive";
        public string ClientId { get; init; } = null!;
        public string ClientSecret { get; init; } = null!;
        public string RefreshToken { get; set; } = null!;
        public string FolderPath { get; init; } = "SupportTickets";
    }
}