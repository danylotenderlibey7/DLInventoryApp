namespace DLInventoryApp.Services.Interfaces
{
    public interface ITagService 
    {
        Task SyncInventoryTagsAsync(Guid inventoryId, IEnumerable<string> tags);
    }
}
