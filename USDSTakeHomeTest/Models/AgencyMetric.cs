namespace USDSTakeHomeTest.Models;

public class AgencyMetric
{
    public int Id { get; set; }

    public int AgencyId { get; set; }
    public Agency Agency { get; set; } = null!;

    public int SnapshotId { get; set; }
    public Snapshot Snapshot { get; set; } = null!;

    public int WordCount { get; set; }

    public double ObligationIntensity { get; set; }

    public string Sha256Checksum { get; set; } = null!;
}
