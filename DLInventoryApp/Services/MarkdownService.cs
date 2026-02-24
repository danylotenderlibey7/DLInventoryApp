using DLInventoryApp.Services.Interfaces;
using Markdig;

namespace DLInventoryApp.Services
{
    public class MarkdownService : IMarkdownService
    {
        private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .DisableHtml()
            .Build();
        public string ToSafeHtml(string markdown) => Markdig.Markdown.ToHtml(markdown ?? "", _pipeline);
    }
}
