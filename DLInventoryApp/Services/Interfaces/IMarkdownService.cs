namespace DLInventoryApp.Services.Interfaces
{
    public interface IMarkdownService
    {
        string ToSafeHtml(string markdown);
    }
}
