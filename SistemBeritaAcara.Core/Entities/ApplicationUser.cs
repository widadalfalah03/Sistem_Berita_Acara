using Microsoft.AspNetCore.Identity;

namespace SistemBeritaAcara.Core.Entities;

public class ApplicationUser : IdentityUser<int>
{
    public string Nama { get; set; } = string.Empty;
    public string? Jabatan { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? TtdPath { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public int? PegawaiId { get; set; }
    public string? ProfilePicPath { get; set; }

    // Diisi saat Admin IT inisiasi pergantian; dikosongkan setelah Admin IT baru selesai setup
    public string? PendingNama { get; set; }
    public string? PendingEmail { get; set; }

    public Pegawai? Pegawai { get; set; }
    public ICollection<BeritaAcara> BeritaAcaraCreated { get; set; } = [];
    public ICollection<BeritaAcara> BeritaAcaraMenyerahkan { get; set; } = [];
    public ICollection<BeritaAcara> BeritaAcaraMengetahui { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
    public ICollection<PegawaiImportLog> PegawaiImportLogs { get; set; } = [];
    public ICollection<BarangImportLog> BarangImportLogs { get; set; } = [];
}
