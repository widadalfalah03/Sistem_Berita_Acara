namespace SistemBeritaAcara.Core.Entities;

public class MasterBarang
{
    public int Id { get; set; }
    public string KodeBarang { get; set; } = string.Empty;
    public string NamaBarang { get; set; } = string.Empty;
    public bool IsAktif { get; set; } = true;

    public ICollection<PerangkatBA> PerangkatBAs { get; set; } = [];
}
