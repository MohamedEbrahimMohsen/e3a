namespace E3A.Application.Options;

public sealed class CatalogOptions
{
    public const string SectionName = "Catalog";

    public int DefaultPageSize { get; set; }
    public int MaxPageSize { get; set; }
    public int SearchTextMaxLength { get; set; }
    public int MaxTagFilters { get; set; }
    public int TagFilterMaxLength { get; set; }
}
