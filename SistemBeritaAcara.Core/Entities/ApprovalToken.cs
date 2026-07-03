using System.ComponentModel.DataAnnotations.Schema;

namespace SistemBeritaAcara.Core.Entities;

public class ApprovalToken
{
    public int Id { get; set; }
    public int BaId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string TokenType { get; set; } = "PJ_SIGNATURE";
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [ForeignKey("BaId")]
    public BeritaAcara BeritaAcara { get; set; } = null!;
}
