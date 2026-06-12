using Microsoft.EntityFrameworkCore;
using SistemBeritaAcara.Core.Interfaces;
using SistemBeritaAcara.Infrastructure.Data;

namespace SistemBeritaAcara.Infrastructure.Jobs;

public class DueDateCheckerJob(AppDbContext db, INotificationService notificationService)
{
    public async Task CheckDueDatesAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var tomorrow = today.AddDays(1);

        var dueTomorrow = await db.BeritaAcara
            .Where(ba => ba.Status == "Approved"
                && ba.TanggalKembali == tomorrow
                && ba.Jenis == "Peminjaman")
            .Include(ba => ba.Creator)
            .ToListAsync();

        foreach (var ba in dueTomorrow)
        {
            string msg = $"Peminjaman BA {ba.NomorSurat} jatuh tempo besok ({tomorrow:dd/MM/yyyy}).";
            await notificationService.SendAsync(ba.CreatedBy, "DUE_REMINDER", msg, ba.Id);
        }

        var dueToday = await db.BeritaAcara
            .Where(ba => ba.Status == "Approved"
                && ba.TanggalKembali == today
                && ba.Jenis == "Peminjaman")
            .ToListAsync();

        foreach (var ba in dueToday)
        {
            string msg = $"Peminjaman BA {ba.NomorSurat} jatuh tempo hari ini ({today:dd/MM/yyyy}).";
            await notificationService.SendAsync(ba.CreatedBy, "DUE_TODAY", msg, ba.Id);
        }
    }
}
