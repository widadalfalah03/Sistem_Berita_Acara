namespace SistemBeritaAcara.Core.Entities;

public class PerangkatBA
{
    public int Id { get; set; }
    public int BaId { get; set; }
    public int BarangId { get; set; }
    public int Jumlah { get; set; }
    public string Satuan { get; set; } = "Pcs";
    public string? NoSerial { get; set; }
    public string? Keterangan { get; set; }

    public BeritaAcara BeritaAcara { get; set; } = null!;
    public MasterBarang Barang { get; set; } = null!;
}
