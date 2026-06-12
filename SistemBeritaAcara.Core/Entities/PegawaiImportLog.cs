namespace SistemBeritaAcara.Core.Entities;

public class PegawaiImportLog
{
    public int Id { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.Now;
    public int ImportedBy { get; set; }
    public string? FileName { get; set; }
    public int AddedCount { get; set; } = 0;
    public int UpdatedCount { get; set; } = 0;
    public int DeactivatedCount { get; set; } = 0;

    public ApplicationUser ImportedByUser { get; set; } = null!;
}
