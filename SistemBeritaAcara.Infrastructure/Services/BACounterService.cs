using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SistemBeritaAcara.Core.Interfaces;
using SistemBeritaAcara.Infrastructure.Data;

namespace SistemBeritaAcara.Infrastructure.Services;

public class BACounterService(AppDbContext db, IConfiguration configuration) : IBACounterService
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

        bool isLainnya = jenis != null && jenis.Equals("Lainnya", StringComparison.OrdinalIgnoreCase);
        var formatKey = isLainnya ? "NomorSurat:FormatLainnya" : "NomorSurat:FormatUmum";
        var format = configuration[formatKey]
            ?? (isLainnya ? "BA-{counter}/PPNEG1000/{year}-S8" : "BA-{counter}/PPNEG1000/{year}-S0");

        var nomor = format
            .Replace("{counter}", value.ToString("D3"))
            .Replace("{year}", tanggal.Year.ToString());

        return (value, nomor);
    }

    public async Task AdjustCounterAfterDeleteAsync()
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        var counter = await db.BACounter
            .FromSqlRaw("SELECT * FROM [BACounter] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = 1")
            .FirstAsync();

        var maxCounter = await db.BeritaAcara.MaxAsync(ba => (int?)ba.CounterValue) ?? 0;
        
        // If the next value is greater than the max counter + 1, it means the highest BA was deleted.
        // We can safely step the counter back so the next BA reuses the deleted number.
        if (counter.NextValue > maxCounter + 1)
        {
            counter.NextValue = maxCounter + 1;
            await db.SaveChangesAsync();
        }
        
        await tx.CommitAsync();
    }
}
