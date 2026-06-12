using SistemBeritaAcara.Core.Entities;

namespace SistemBeritaAcara.Core.Interfaces;

public interface IExcelService
{
    Task<(int added, int updated, int deactivated, List<string> errors)> ImportPegawaiAsync(Stream excelStream, int importedBy, string fileName);
    Task<(int added, int updated, int deactivated, List<string> errors)> ImportBarangAsync(Stream excelStream, int importedBy, string fileName);
    Task AppendBeritaAcaraToArsipAsync(BeritaAcara ba);
}
