using Microsoft.EntityFrameworkCore;
using SistemBeritaAcara.Core.Entities;
using SistemBeritaAcara.Core.Interfaces;
using SistemBeritaAcara.Infrastructure.Data;

namespace SistemBeritaAcara.Infrastructure.Jobs;

public class DueDateCheckerJob(
    AppDbContext db,
    INotificationService notificationService,
    IEmailService emailService)
{
    public async Task CheckDueDatesAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        // ── 1. Jatuh tempo HARI INI (H-0) ─────────────────────────────────────
        var dueToday = await db.BeritaAcara
            .Where(ba => ba.Status == "Approved"
                && ba.Jenis == "Peminjaman"
                && ba.TanggalKembali == today
                && !ba.IsReturned)
            .Include(ba => ba.Creator)
            .Include(ba => ba.Pj)
            .ToListAsync();

        foreach (var ba in dueToday)
        {
            string tglStr = today.ToString("dd/MM/yyyy");
            string nomorSurat = ba.NomorSurat ?? $"BA-{ba.Id}";
            string pjNama = ba.Pj?.Nama ?? "–";

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
                    isForPj: false);
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
                    isForPj: true);
            }
        }

        // ── 2. Sudah LEWAT jatuh tempo (overdue) ──────────────────────────────
        // Kirim pengingat sekali sehari untuk yang sudah overdue
        var overdue = await db.BeritaAcara
            .Where(ba => ba.Status == "Approved"
                && ba.Jenis == "Peminjaman"
                && ba.TanggalKembali < today
                && !ba.IsReturned)
            .Include(ba => ba.Creator)
            .Include(ba => ba.Pj)
            .ToListAsync();

        foreach (var ba in overdue)
        {
            string tglStr = ba.TanggalKembali!.Value.ToString("dd/MM/yyyy");
            string nomorSurat = ba.NomorSurat ?? $"BA-{ba.Id}";
            string pjNama = ba.Pj?.Nama ?? "–";

            // Notifikasi Inbox ke Admin Gudang
            string msgInbox = $"🚨 Peminjaman {nomorSurat} (PJ: {pjNama}) telah melewati batas pengembalian ({tglStr}). Segera tindak lanjuti!";
            await notificationService.SendAsync(ba.CreatedBy, "DUE_OVERDUE", msgInbox, ba.Id);

            // Email ke Admin Gudang
            if (!string.IsNullOrEmpty(ba.Creator?.Email))
            {
                await emailService.SendDueDateReminderAsync(
                    ba.Creator.Email,
                    ba.Creator.Nama ?? "Admin",
                    nomorSurat,
                    pjNama,
                    tglStr,
                    daysUntilDue: -1,
                    isForPj: false);
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
                    daysUntilDue: -1,
                    isForPj: true);
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
            .ToListAsync();

        foreach (var ba in dueTomorrow)
        {
            string tglStr = tomorrow.ToString("dd/MM/yyyy");
            string nomorSurat = ba.NomorSurat ?? $"BA-{ba.Id}";
            string pjNama = ba.Pj?.Nama ?? "–";

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
                    isForPj: false);
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
                    isForPj: true);
            }
        }
    }
}
