using SistemBeritaAcara.Core.Entities;

namespace SistemBeritaAcara.Core.Interfaces;

public interface IExcelService
{
    Task<(int added, int updated, int deactivated, List<string> errors)> ImportPegawaiAsync(Stream excelStream, int importedBy, string fileName);
    Task<(int added, int updated, int deactivated, List<string> errors)> ImportBarangAsync(Stream excelStream, int importedBy, string fileName);
    Task AppendBeritaAcaraToArsipAsync(BeritaAcara ba);
    Task<byte[]> ExportArsipAsync(IEnumerable<BeritaAcara> data, string baseUrl);
    Task<byte[]> ExportPegawaiAsync(IEnumerable<Pegawai> data);
    Task<byte[]> ExportBarangAsync(IEnumerable<MasterBarang> data);
}
