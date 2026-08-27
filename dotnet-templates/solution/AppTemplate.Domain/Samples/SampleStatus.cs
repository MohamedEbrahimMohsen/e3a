namespace AppTemplate.Domain.Samples;

public enum SampleStatus
{
    Draft = 0,
    Active = 1,
    Archived = 2,
}

public static class SampleStatusExtensions
{
    public static bool IsTerminal(this SampleStatus status)
    {
        return status == SampleStatus.Archived;
    }
}
