namespace SistemBeritaAcara.Core.Entities;

public class BuktiFoto
{
    public int Id { get; set; }
    public int BaId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.Now;

    public BeritaAcara BeritaAcara { get; set; } = null!;
}
