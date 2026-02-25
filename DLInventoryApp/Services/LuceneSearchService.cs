using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.ViewModels.Search;
using Humanizer;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using MailKit.Search;
using Microsoft.EntityFrameworkCore;

namespace DLInventoryApp.Services
{
    public class LuceneSearchService : ISearchService
    {
        private readonly ApplicationDbContext _context; 
        private readonly string _indexPath;
        public LuceneSearchService(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            var path = config["Lucene:IndexPath"];
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("Lucene IndexPath is not configured in appsettings.json");
            _indexPath = Path.GetFullPath(path);
        }
        public async Task<SearchResultVm> SearchAsync(string query, int inventoriesLimit = 5, int itemsLimit = 20)
        {
            if (string.IsNullOrWhiteSpace(query)) return new SearchResultVm { Query = query };
            var directory = OpenIndexDirectory();
            using var reader = DirectoryReader.Open(directory);
            var analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48);
            var searcher = new IndexSearcher(reader);
            var parser = new MultiFieldQueryParser(LuceneVersion.LUCENE_48, new[] { "Title", "Description", "CustomId", "Content" }, analyzer);
            var luceneQuery = parser.Parse(query);
            var fields = new[] { "Title", "Description", "CustomId", "Content" };
            var expanded = new BooleanQuery { { luceneQuery, Occur.SHOULD } };
            foreach (var term in query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (term.Length < 3) continue;
                foreach (var f in fields)
                    expanded.Add(new PrefixQuery(new Term(f, term.ToLowerInvariant())), Occur.SHOULD);
            }
            var invQuery = new BooleanQuery { { expanded, Occur.MUST }, { new TermQuery(new Term("DocType", "Inventory")), Occur.MUST } };
            var itemQuery = new BooleanQuery { { expanded, Occur.MUST }, { new TermQuery(new Term("DocType", "Item")), Occur.MUST } };
            var invTop = searcher.Search(invQuery, inventoriesLimit);
            var itemTop = searcher.Search(itemQuery, itemsLimit);
            var vm = new SearchResultVm { Query = query };
            foreach (var sd in invTop.ScoreDocs)
            {
                var doc = searcher.Doc(sd.Doc);
                var idStr = doc.Get("Id");
                var title = doc.Get("Title") ?? "";
                var description = doc.Get("Description") ?? "";
                var len = Math.Min(60, description.Length);
                vm.Inventories.Add(new InventorySearchRowVm { Id = Guid.Parse(idStr!), Title = title, Snippet = description.Substring(0, len) });
            }
            foreach (var sd in itemTop.ScoreDocs)
            {
                var doc = searcher.Doc(sd.Doc);
                var internalIdStr = doc.Get("InternalId");
                var inventoryIdStr = doc.Get("InventoryId");
                var customId = doc.Get("CustomId") ?? "";
                vm.Items.Add(new ItemSearchRowVm { Id = Guid.Parse(internalIdStr!), InventoryId = Guid.Parse(inventoryIdStr!), CustomId = customId, InventoryTitle = "", Snippet = "" });
            }
            if (vm.Items.Count > 0)
            {
                var invIds = vm.Items.Select(i => i.InventoryId).Distinct().ToList();
                var inventoryTitlesById = await _context.Inventories.AsNoTracking().Where(inv => invIds.Contains(inv.Id)).Select(inv => new { inv.Id, inv.Title }).ToDictionaryAsync(x => x.Id, x => x.Title);
                foreach (var item in vm.Items) if (inventoryTitlesById.TryGetValue(item.InventoryId, out var title)) item.InventoryTitle = title ?? "";
            }
            return vm;
        }
        public async Task ReindexAllAsync()
        {
            using var writer = OpenWriter();
            writer.DeleteAll(); 
            var inventories = await _context.Inventories
                .AsNoTracking()
                .ToListAsync();
            var items = await _context.Items
                .AsNoTracking()
                .Include(i => i.FieldValues)
                .ToListAsync();
            foreach(var inv in inventories)
            {
                var doc = BuildInventoryDocument(inv);
                writer.AddDocument(doc);
            }
            foreach(var item in items)
            {
                var doc = BuildItemDocument(item);
                writer.AddDocument(doc);
            }
            writer.Commit();
        }
        public async Task IndexInventoryAsync(Guid inventoryId)
        {
            var inventory = await _context.Inventories
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == inventoryId);
            if (inventory == null) return;
            using var writer = OpenWriter();
            writer.DeleteDocuments(new Term("Id", inventoryId.ToString()));
            writer.AddDocument(BuildInventoryDocument(inventory));
            writer.Commit();
        }
        public Task RemoveInventoryAsync(Guid inventoryId)
        {
            using var writer = OpenWriter();
            writer.DeleteDocuments(new Term("Id", inventoryId.ToString()));
            writer.Commit();
            return Task.CompletedTask;
        }
        public async Task IndexItemAsync(Guid itemId)
        {
            var item = await _context.Items
                .AsNoTracking()
                .Include(i => i.FieldValues)
                .FirstOrDefaultAsync(i => i.Id == itemId);
            if (item == null) return;
            using var writer = OpenWriter();
            writer.DeleteDocuments(new Term("InternalId", itemId.ToString()));
            writer.AddDocument(BuildItemDocument(item));
            writer.Commit();
        }
        public Task RemoveItemAsync(Guid itemId)
        {
            using var writer = OpenWriter();
            writer.DeleteDocuments(new Term("InternalId", itemId.ToString()));
            writer.Commit();
            return Task.CompletedTask;
        }
        private FSDirectory OpenIndexDirectory()
        {
            var directoryInfo = System.IO.Directory.CreateDirectory(_indexPath);
            return FSDirectory.Open(directoryInfo);
        }
        private IndexWriter OpenWriter()
        {
            var directory = OpenIndexDirectory();
            var analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48);
            var config = new IndexWriterConfig(LuceneVersion.LUCENE_48, analyzer);
            return new IndexWriter(directory, config);
        }
        private Document BuildInventoryDocument(Inventory inventory)
        {
            var doc = new Document();
            doc.Add(new StringField("DocType", "Inventory", Field.Store.YES));
            doc.Add(new StringField("Id", inventory.Id.ToString(), Field.Store.YES));
            doc.Add(new TextField("Title", inventory.Title ?? "", Field.Store.YES));
            doc.Add(new TextField("Description", inventory.Description ?? "", Field.Store.YES));
            return doc;
        }
        private Document BuildItemDocument(Item item)
        {
            var doc = new Document();
            doc.Add(new StringField("DocType", "Item", Field.Store.YES));
            doc.Add(new StringField("InternalId", item.Id.ToString(), Field.Store.YES));
            doc.Add(new StringField("InventoryId", item.InventoryId.ToString(), Field.Store.YES));
            doc.Add(new TextField("CustomId", item.CustomId ?? "", Field.Store.YES));
            var combinedText = item.FieldValues?
                .Where(f => !string.IsNullOrWhiteSpace(f.TextValue))
                .Select(f => f.TextValue)
                .DefaultIfEmpty("")
                .Aggregate((a, b) => a + " " + b);
            doc.Add(new TextField("Content", combinedText ?? "", Field.Store.NO));
            return doc;
        }
    }
}
