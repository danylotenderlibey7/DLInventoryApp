namespace DLInventoryApp.Services.Interfaces
{
    public interface ICustomIdGenerator
    {
        string Generate(string title, IReadOnlyCollection<string> existingIds);
    }
}
