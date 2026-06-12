namespace SistemBeritaAcara.Core.Entities;

public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? BaId { get; set; }
    public string Tipe { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ApplicationUser User { get; set; } = null!;
    public BeritaAcara? BeritaAcara { get; set; }
}
