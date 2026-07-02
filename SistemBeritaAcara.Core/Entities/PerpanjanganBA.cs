namespace SistemBeritaAcara.Core.Entities;

public class PerpanjanganBA
{
    public int Id { get; set; }
    public int BeritaAcaraId { get; set; }
    public DateOnly TanggalKembaliBaru { get; set; }
    public DateOnly TanggalKembalisebelumnya { get; set; }
    public DateTime DiperpanjangPada { get; set; } = DateTime.Now;
    public string? Catatan { get; set; }

    public BeritaAcara BeritaAcara { get; set; } = null!;
}
