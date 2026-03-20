namespace DLInventoryApp.Services.Interfaces
{
    public interface IOneDriveService
    {
        Task UploadJsonAsync(string fileName, string jsonContent);
    }
}