namespace DLInventoryApp.Services.Interfaces
{
    public interface ISalesforceService
    {
        Task ExportContactAsync(string firstName, string lastName, string email, string phone, string companyName, string jobTitle);
    }
}