namespace SistemBeritaAcara.Core.Entities;

/// <summary>
/// Menyimpan riwayat setiap kejadian penolakan (reject) Berita Acara.
/// Independen dari tabel Notification sehingga data historis tidak hilang
/// meskipun notifikasi di Inbox dihapus.
/// </summary>
public class BeritaAcaraHistory
{
    public int Id { get; set; }
    public int BaId { get; set; }
    public int RejectedByUserId { get; set; }
    public string AlasanReject { get; set; } = string.Empty;
    public DateTime RejectedAt { get; set; } = DateTime.Now;

    // Snapshot data BA saat penolakan, agar tetap terbaca
    // bahkan jika data BA sudah berubah karena direvisi
    public string BaJenis { get; set; } = string.Empty;
    public string? BaNomorSurat { get; set; }
    public int BaCreatedBy { get; set; }
    public int? BaMengetahuiId { get; set; }

    // Navigation properties
    public BeritaAcara BeritaAcara { get; set; } = null!;
    public ApplicationUser RejectedByUser { get; set; } = null!;
}
