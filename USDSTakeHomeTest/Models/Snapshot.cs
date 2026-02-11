namespace USDSTakeHomeTest.Models;

public class Snapshot
{
    public int Id { get; set; }

    /// <summary>
    /// E.g. "CurrentECFR" or "CFRAnnual"
    /// </summary>
    public string Type { get; set; } = null!;

    public DateOnly SnapshotDate { get; set; }

    public string? Source { get; set; }

    public DateTime IngestedAt { get; set; }
}
