namespace SistemBeritaAcara.Core.Entities;

public class Pegawai
{
    public int Id { get; set; }
    public string Nama { get; set; } = string.Empty;
    public string NoPekerja { get; set; } = string.Empty;
    public string? Jabatan { get; set; }
    public string? CostCenter { get; set; }
    public string? FungsiDirektorat { get; set; }
    public string? Alamat { get; set; }
    public string? Email { get; set; }
    public string? NoTelp { get; set; }
    public string? TtdPath { get; set; }
    public bool IsAktif { get; set; } = true;
    public DateTime? LastSync { get; set; }

    public ICollection<BeritaAcara> BeritaAcaraPj { get; set; } = [];
    public ICollection<BeritaAcara> BeritaAcaraMenyerahkan { get; set; } = [];
    public ICollection<ApplicationUser> Users { get; set; } = [];
}
