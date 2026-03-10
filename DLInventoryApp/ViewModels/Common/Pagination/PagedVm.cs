namespace DLInventoryApp.ViewModels.Common.Pagination
{
    public class PagedVm<T>
    {
        public IReadOnlyList<T> Items { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 5;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPrevPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
        public int From => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
        public int To => Math.Min(Page * PageSize, TotalCount);
    }
}
