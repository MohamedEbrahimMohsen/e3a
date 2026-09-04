namespace E3A.Application.Options;

public sealed class ReportsOptions
{
    public const string SectionName = "Reports";

    public int DetailsMaxLength { get; set; }
    public int MaxReportsPerItem { get; set; }
}
