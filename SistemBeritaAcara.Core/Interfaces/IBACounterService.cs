namespace SistemBeritaAcara.Core.Interfaces;

public interface IBACounterService
{
    Task<(int counterValue, string nomorSurat)> GetNextNomorSuratAsync(DateOnly tanggal, string jenis);
    Task AdjustCounterAfterDeleteAsync();
}
