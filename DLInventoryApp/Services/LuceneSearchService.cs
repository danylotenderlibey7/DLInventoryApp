using DLInventoryApp.Data;
using DLInventoryApp.Models;
using DLInventoryApp.Services.Interfaces;
using DLInventoryApp.ViewModels.Search;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
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
            var strongBoosts = new Dictionary<string, float>
            {
                { "CustomId", 10f },
                { "Title", 6f }
            };
            var strongParser = new MultiFieldQueryParser(LuceneVersion.LUCENE_48, new[] { "Title", "CustomId" }, analyzer, strongBoosts)
            {
                DefaultOperator = Operator.AND
            };
            var weakBoosts = new Dictionary<string, float>
            {
                { "Description", 2f },
                { "Content", 1f }
            };
            var weakParser = new MultiFieldQueryParser(LuceneVersion.LUCENE_48, new[] { "Description", "Content" }, analyzer, weakBoosts)
            {
                DefaultOperator = Operator.OR
            };
            Query strongQuery;
            Query weakQuery;
            try
            {
                strongQuery = strongParser.Parse(query);
            }
            catch (ParseException)
            {
                strongQuery = strongParser.Parse(QueryParserBase.Escape(query));
            }
            try
            {
                weakQuery = weakParser.Parse(query);
            }
            catch (ParseException)
            {
                weakQuery = weakParser.Parse(QueryParserBase.Escape(query));
            }
            if (weakQuery is BooleanQuery bq)
            {
                var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var count = terms.Length;
                int minShouldMatch;
                if (count <= 2) minShouldMatch = 1;
                else if (count <= 4) minShouldMatch = 2;
                else minShouldMatch = (int)Math.Floor(count * 0.6);
                bq.MinimumNumberShouldMatch = minShouldMatch;
            }
            strongQuery.Boost = 2f;
            weakQuery.Boost = 1f;
            var expanded = new BooleanQuery
            {
                { strongQuery, Occur.SHOULD },
                { weakQuery, Occur.SHOULD }
            };
            var fields = new[] { "Title", "Description", "CustomId", "Content" };
            foreach (var term in query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (term.Length < 3) continue;
                var t = term.ToLowerInvariant();
                foreach (var f in fields)
                {
                    var pq = new PrefixQuery(new Term(f, t));
                    pq.Boost = 0.3f;
                    expanded.Add(pq, Occur.SHOULD);
                }
            }
            var invQuery = new BooleanQuery
            {
                { expanded, Occur.MUST },
                { new TermQuery(new Term("DocType", "Inventory")), Occur.MUST }
            };
            var itemQuery = new BooleanQuery
            {
                { expanded, Occur.MUST },
                { new TermQuery(new Term("DocType", "Item")), Occur.MUST }
            };
            var invTop = searcher.Search(invQuery, inventoriesLimit);
            var itemTop = searcher.Search(itemQuery, itemsLimit);
            var totalHits = invTop.TotalHits + itemTop.TotalHits;
            if (totalHits == 0)
            {
                var fuzzyFields = new[] { "Description", "Content" };
                var fuzzy = BuildFuzzyQuery(query, fuzzyFields, boost: 0.15f);
                expanded.Add(fuzzy, Occur.SHOULD);
                invQuery = new BooleanQuery
                {
                    { expanded, Occur.MUST },
                    { new TermQuery(new Term("DocType", "Inventory")), Occur.MUST }
                };
                itemQuery = new BooleanQuery
                {
                    { expanded, Occur.MUST },
                    { new TermQuery(new Term("DocType", "Item")), Occur.MUST }
                };
                invTop = searcher.Search(invQuery, inventoriesLimit);
                itemTop = searcher.Search(itemQuery, itemsLimit);
            }
            var vm = new SearchResultVm { Query = query };
            foreach (var sd in invTop.ScoreDocs)
            {
                var doc = searcher.Doc(sd.Doc);
                var idStr = doc.Get("Id");
                var title = doc.Get("Title") ?? "";
                var description = doc.Get("Description") ?? "";
                var len = Math.Min(60, description.Length);
                vm.Inventories.Add(new InventorySearchRowVm
                {
                    Id = Guid.Parse(idStr!),
                    Title = title,
                    Snippet = description.Substring(0, len)
                });
            }
            foreach (var sd in itemTop.ScoreDocs)
            {
                var doc = searcher.Doc(sd.Doc);
                var internalIdStr = doc.Get("InternalId");
                var inventoryIdStr = doc.Get("InventoryId");
                var customId = doc.Get("CustomId") ?? "";
                var preview = doc.Get("ContentPreview") ?? "";
                var snippet = "";
                if (!string.IsNullOrWhiteSpace(preview))
                {
                    var lowerPreview = preview.ToLowerInvariant();
                    var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var matches = new List<string>();
                    foreach (var t in terms)
                    {
                        var termLower = t.ToLowerInvariant();
                        var index = lowerPreview.IndexOf(termLower);
                        if (index >= 0)
                        {
                            var start = Math.Max(0, index - 30);
                            var length = Math.Min(80, preview.Length - start);
                            matches.Add(preview.Substring(start, length));
                        }
                    }
                    snippet = string.Join(" ... ", matches.Distinct());
                }
                vm.Items.Add(new ItemSearchRowVm
                {
                    Id = Guid.Parse(internalIdStr!),
                    InventoryId = Guid.Parse(inventoryIdStr!),
                    CustomId = customId,
                    InventoryTitle = "",
                    Snippet = snippet
                });
            }
            if (vm.Items.Count > 0)
            {
                var invIds = vm.Items.Select(i => i.InventoryId).Distinct().ToList();
                var inventoryTitlesById = await _context.Inventories.AsNoTracking()
                    .Where(inv => invIds.Contains(inv.Id))
                    .Select(inv => new { inv.Id, inv.Title })
                    .ToDictionaryAsync(x => x.Id, x => x.Title);
                foreach (var item in vm.Items)
                    if (inventoryTitlesById.TryGetValue(item.InventoryId, out var title))
                        item.InventoryTitle = title ?? "";
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
                .ThenInclude(fv => fv.CustomField)
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
                .ThenInclude(fv => fv.CustomField)
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
            var parts = new List<string>();
            if (item.FieldValues != null)
            {
                foreach (var fv in item.FieldValues)
                {
                    var fieldName = fv.CustomField?.Name?.Trim();
                    if (!string.IsNullOrWhiteSpace(fv.TextValue))
                    {
                        var v = fv.TextValue.Trim();
                        parts.Add(v);
                        if (!string.IsNullOrWhiteSpace(fieldName))
                            parts.Add($"{fieldName} {v}");
                    }
                    if (fv.NumberValue.HasValue)
                    {
                        var v = fv.NumberValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        parts.Add(v);
                        if (!string.IsNullOrWhiteSpace(fieldName))
                            parts.Add($"{fieldName} {v}");
                    }
                    if (fv.BoolValue.HasValue)
                    {
                        var v = fv.BoolValue.Value ? "true" : "false";
                        parts.Add(v);
                        if (!string.IsNullOrWhiteSpace(fieldName))
                            parts.Add($"{fieldName} {v}");
                    }
                }
            }
            var combinedText = string.Join(" ", parts);
            var preview = combinedText;
            if (!string.IsNullOrWhiteSpace(preview) && preview.Length > 400) 
                preview = preview.Substring(0, 400);
            doc.Add(new TextField("ContentPreview", preview ?? "", Field.Store.YES));
            doc.Add(new TextField("Content", combinedText, Field.Store.NO));
            return doc;
        }
        private static Query BuildFuzzyQuery(string query, string[] fields, float boost = 0.25f)
        {
            var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var bq = new BooleanQuery();
            foreach (var raw in terms)
            {
                var t = raw.ToLowerInvariant();
                if (t.Length < 3) continue;
                var perTerm = new BooleanQuery();
                foreach (var f in fields)
                {
                    var fq = new FuzzyQuery(new Term(f, t), maxEdits: 1, prefixLength: 1);
                    fq.Boost = boost;
                    perTerm.Add(fq, Occur.SHOULD);
                }
                bq.Add(perTerm, Occur.MUST);
            }
            return bq;
        }
    }
}
