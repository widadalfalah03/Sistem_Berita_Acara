namespace SistemBeritaAcara.Core.Entities;

public class BeritaAcara
{
    public int Id { get; set; }
    public string? NomorSurat { get; set; }
    public int? CounterValue { get; set; }
    public DateOnly Tanggal { get; set; }
    public string Jenis { get; set; } = string.Empty;
    public string? JenisCustom { get; set; }
    public int PjId { get; set; }
    public string? PjNoTelp { get; set; }
    public int MenyerahkanId { get; set; }
    public int MengetahuiId { get; set; }
    public string? TiketSscNo { get; set; }
    public DateOnly? TanggalKembali { get; set; }
    public string Status { get; set; } = "Draft";
    public string? AlasanReject { get; set; }
    public string? TtdPjPath { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? PjSignedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? DocxPath { get; set; }
    public string? DocxFinalPath { get; set; }
    public bool ExcelExported { get; set; } = false;
    public bool IsReturned { get; set; } = false;
    public DateTime? ReturnedAt { get; set; }
    public bool WasRejected { get; set; } = false;
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Pegawai Pj { get; set; } = null!;
    public Pegawai Menyerahkan { get; set; } = null!;
    public ApplicationUser Mengetahui { get; set; } = null!;
    public ApplicationUser Creator { get; set; } = null!;
    public ICollection<PerangkatBA> Perangkat { get; set; } = [];
    public ICollection<BuktiFoto> BuktiFotos { get; set; } = [];
    public ICollection<ApprovalToken> ApprovalTokens { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
}
