namespace DLInventoryApp.Dtos.Support
{
    public class SupportTicketPayload
    {
        public string TicketId { get; set; } = null!;
        public string ReportedBy { get; set; } = null!;
        public string? ReportedByEmail { get; set; } 
        public string? InventoryTitle { get; set; }
        public string Link { get; set; } = null!;
        public string Priority { get; set; } = null!;
        public string Summary { get; set; } = null!;
        public List<string> AdminEmails { get; set; } = new();
        public DateTime CreatedAtUtc { get; set; }
    }
}