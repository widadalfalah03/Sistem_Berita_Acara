using Microsoft.EntityFrameworkCore;
using SistemBeritaAcara.Core.Interfaces;
using SistemBeritaAcara.Infrastructure.Data;

namespace SistemBeritaAcara.Infrastructure.Services;

public class BACounterService(AppDbContext db) : IBACounterService
{
    private static readonly string[] RomawiMap = ["", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII"];

    public async Task<(int counterValue, string nomorSurat)> GetNextNomorSuratAsync(DateOnly tanggal, string jenis)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        var counter = await db.BACounter
            .FromSqlRaw("SELECT * FROM [BACounter] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = 1")
            .FirstAsync();

        int value = counter.NextValue;
        counter.NextValue++;
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        string nomor;
        if (jenis != null && jenis.Equals("Lainnya", StringComparison.OrdinalIgnoreCase))
        {
            nomor = $"BA {value:D3}/PPNEG1000/{tanggal.Year}-S8";
        }
        else
        {
            nomor = $"BA {value:D3}/PPNEG1000/{tanggal.Year}-S0";
        }
        return (value, nomor);
    }
}
