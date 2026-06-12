namespace SistemBeritaAcara.Core.Interfaces;

public interface IDeaktivasiService
{
    Task DeaktivasiPegawaiAsync(int pegawaiId);
}
