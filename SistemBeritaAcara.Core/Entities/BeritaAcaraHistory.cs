namespace SistemBeritaAcara.Core.Entities;






public class BeritaAcaraHistory
{
    public int Id { get; set; }
    public int BaId { get; set; }
    public int RejectedByUserId { get; set; }
    public string AlasanReject { get; set; } = string.Empty;
    public DateTime RejectedAt { get; set; } = DateTime.Now;

    
    
    public string BaJenis { get; set; } = string.Empty;
    public string? BaNomorSurat { get; set; }
    public int BaCreatedBy { get; set; }
    public int? BaMengetahuiId { get; set; }

    
    public BeritaAcara BeritaAcara { get; set; } = null!;
    public ApplicationUser RejectedByUser { get; set; } = null!;
}
