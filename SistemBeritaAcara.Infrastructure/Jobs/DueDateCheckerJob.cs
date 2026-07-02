using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SistemBeritaAcara.Core.Entities;
using SistemBeritaAcara.Core.Interfaces;
using SistemBeritaAcara.Infrastructure.Data;

namespace SistemBeritaAcara.Infrastructure.Jobs;

public class DueDateCheckerJob(
    AppDbContext db,
    INotificationService notificationService,
    IEmailService emailService,
    IConfiguration configuration)
{
    private string BaseUrl => (configuration["App:PublicUrl"] ?? configuration["App:BaseUrl"] ?? "http://localhost:5000").TrimEnd('/');

    // Helper: ambil daftar nama barang dari BA
    private static List<string> GetBarangList(BeritaAcara ba)
    {
        return ba.Perangkat
            .Select(p =>
            {
                string namaBarang = p.Barang?.NamaBarang ?? "Perangkat";
                string detail = $"{p.Jumlah} {p.Satuan}";
                if (!string.IsNullOrEmpty(p.NoSerial)) detail += $" — S/N: {p.NoSerial}";
                if (!string.IsNullOrEmpty(p.Keterangan)) detail += $" ({p.Keterangan})";
                return $"{namaBarang} — {detail}";
            })
            .ToList();
    }

    public async Task CheckDueDatesAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var todayStart = DateTime.Today;

        // ── 1. Jatuh tempo HARI INI (H-0) ─────────────────────────────────────
        var dueToday = await db.BeritaAcara
            .Where(ba => ba.Status == "Approved"
                && ba.Jenis == "Peminjaman"
                && ba.TanggalKembali == today
                && !ba.IsReturned)
            .Include(ba => ba.Creator)
            .Include(ba => ba.Pj)
            .Include(ba => ba.Perangkat).ThenInclude(p => p.Barang)
            .ToListAsync();

        foreach (var ba in dueToday)
        {
            // Dedup: skip jika notifikasi DUE_TODAY untuk BA ini sudah dikirim hari ini
            if (await db.Notification.AnyAsync(n => n.BaId == ba.Id && n.Tipe == "DUE_TODAY" && n.CreatedAt >= todayStart))
                continue;

            string tglStr = today.ToString("dd/MM/yyyy");
            string nomorSurat = ba.NomorSurat ?? $"BA-{ba.Id}";
            string pjNama = ba.Pj?.Nama ?? "–";
            var barangList = GetBarangList(ba);

            // Notifikasi Inbox ke Admin Gudang yang buat BA
            string msgInbox = $"⏰ Peminjaman {nomorSurat} (PJ: {pjNama}) jatuh tempo hari ini ({tglStr}). Harap segera koordinasikan pengembalian perangkat.";
            await notificationService.SendAsync(ba.CreatedBy, "DUE_TODAY", msgInbox, ba.Id);

            // Email ke Admin Gudang
            if (!string.IsNullOrEmpty(ba.Creator?.Email))
            {
                await emailService.SendDueDateReminderAsync(
                    ba.Creator.Email,
                    ba.Creator.Nama ?? "Admin",
                    nomorSurat,
                    pjNama,
                    tglStr,
                    daysUntilDue: 0,
                    isForPj: false,
                    baId: ba.Id,
                    baseUrl: BaseUrl,
                    barangList: barangList);
            }

            // Email ke PJ
            if (!string.IsNullOrEmpty(ba.Pj?.Email))
            {
                await emailService.SendDueDateReminderAsync(
                    ba.Pj.Email,
                    pjNama,
                    nomorSurat,
                    pjNama,
                    tglStr,
                    daysUntilDue: 0,
                    isForPj: true,
                    baId: ba.Id,
                    baseUrl: BaseUrl,
                    barangList: barangList);
            }
        }


        // ── 3. Jatuh tempo BESOK (H-1) ─────────────────────────────────────────
        var tomorrow = today.AddDays(1);
        var dueTomorrow = await db.BeritaAcara
            .Where(ba => ba.Status == "Approved"
                && ba.Jenis == "Peminjaman"
                && ba.TanggalKembali == tomorrow
                && !ba.IsReturned)
            .Include(ba => ba.Creator)
            .Include(ba => ba.Pj)
            .Include(ba => ba.Perangkat).ThenInclude(p => p.Barang)
            .ToListAsync();

        foreach (var ba in dueTomorrow)
        {
            // Dedup: skip jika notifikasi DUE_TOMORROW untuk BA ini sudah dikirim hari ini
            if (await db.Notification.AnyAsync(n => n.BaId == ba.Id && n.Tipe == "DUE_TOMORROW" && n.CreatedAt >= todayStart))
                continue;

            string tglStr = tomorrow.ToString("dd/MM/yyyy");
            string nomorSurat = ba.NomorSurat ?? $"BA-{ba.Id}";
            string pjNama = ba.Pj?.Nama ?? "–";
            var barangList = GetBarangList(ba);

            // Notifikasi Inbox ke Admin Gudang
            string msgInbox = $"ℹ️ Peminjaman {nomorSurat} (PJ: {pjNama}) akan jatuh tempo besok ({tglStr}). Harap persiapkan pengembalian.";
            await notificationService.SendAsync(ba.CreatedBy, "DUE_TOMORROW", msgInbox, ba.Id);

            // Email ke Admin Gudang
            if (!string.IsNullOrEmpty(ba.Creator?.Email))
            {
                await emailService.SendDueDateReminderAsync(
                    ba.Creator.Email,
                    ba.Creator.Nama ?? "Admin",
                    nomorSurat,
                    pjNama,
                    tglStr,
                    daysUntilDue: 1,
                    isForPj: false,
                    baId: ba.Id,
                    baseUrl: BaseUrl,
                    barangList: barangList);
            }

            // Email ke PJ
            if (!string.IsNullOrEmpty(ba.Pj?.Email))
            {
                await emailService.SendDueDateReminderAsync(
                    ba.Pj.Email,
                    pjNama,
                    nomorSurat,
                    pjNama,
                    tglStr,
                    daysUntilDue: 1,
                    isForPj: true,
                    baId: ba.Id,
                    baseUrl: BaseUrl,
                    barangList: barangList);
            }
        }
    }
}
