using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SistemBeritaAcara.Core.Entities;
using SistemBeritaAcara.Core.Interfaces;
using SistemBeritaAcara.Infrastructure.Data;

namespace SistemBeritaAcara.Infrastructure.Jobs;

public class AutoApproveJob(
    AppDbContext db,
    IDocumentService documentService,
    INotificationService notificationService,
    IEmailService emailService,
    IConfiguration configuration,
    ILogger<AutoApproveJob> logger)
{
    private string BaseUrl => (configuration["App:PublicUrl"] ?? configuration["App:BaseUrl"] ?? "http://localhost:5000").TrimEnd('/');

    public async Task ProcessAutoApproveAsync()
    {
        // Gunakan DateTime.Now (waktu lokal) agar konsisten dengan cara SubmittedAt disimpan
        // TESTING: batas waktu 2 menit (production: 24 jam)
        var cutoffTime = DateTime.Now.AddMinutes(-2);

        // Cari BA yang statusnya "WaitingPJSign" dan sudah lebih dari 2 menit sejak di-submit
        var pendingBas = await db.BeritaAcara
            .Include(b => b.Pj)
            .Include(b => b.Menyerahkan)
            .Include(b => b.Mengetahui)
            .Include(b => b.Creator)
            .Include(b => b.BuktiFotos)
            .Include(b => b.Perangkat).ThenInclude(p => p.Barang)
            .Where(b => b.Status == "WaitingPJSign" && b.SubmittedAt <= cutoffTime)
            .ToListAsync();

        if (!pendingBas.Any())
        {
            logger.LogInformation("[AutoApproveJob] Tidak ada BA yang melewati batas waktu 2 menit.");
            return;
        }

        logger.LogInformation($"[AutoApproveJob] Ditemukan {pendingBas.Count} BA yang melewati 2 menit. Memulai auto-approve.");

        foreach (var ba in pendingBas)
        {
            try
            {
                // 1. Update status & tandai sebagai auto-approved oleh sistem
                ba.TtdPjPath = "images/auto_approve_stamp.png";
                ba.PjSignedAt = DateTime.Now;
                ba.Status = "WaitingApproval";
                ba.SubmittedAt = DateTime.Now; // Waktu pengiriman ke Approver

                // 2. Generate ulang dokumen dari template (DocxPath bisa null jika belum pernah dibuat)
                //    dan simpan path-nya ke ba.DocxPath
                ba.DocxPath = null; // Paksa regenerasi dari template
                db.BeritaAcara.Update(ba);
                await db.SaveChangesAsync();

                // 3. Generate dokumen baru dari template
                ba.DocxPath = await documentService.GenerateDocxAsync(ba);
                db.BeritaAcara.Update(ba);
                await db.SaveChangesAsync();

                logger.LogInformation($"[AutoApproveJob] Dokumen BA {ba.Id} berhasil di-generate ulang.");

                // 4. Embed stempel "Automatically Approved" ke placeholder {{SIG_PJ}}
                var stampPhysicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "auto_approve_stamp.png");
                if (File.Exists(stampPhysicalPath))
                {
                    await documentService.EmbedTtdPjAsync(ba.Id, stampPhysicalPath);
                    logger.LogInformation($"[AutoApproveJob] Stempel auto-approve berhasil di-embed ke BA {ba.Id}.");
                }
                else
                {
                    logger.LogWarning($"[AutoApproveJob] File stempel tidak ditemukan di {stampPhysicalPath}. Dokumen BA {ba.Id} tidak akan memiliki stempel.");
                }

                // 4.5 Embed tanda tangan Admin Gudang (Yang Menyerahkan)
                if (ba.Creator != null && !string.IsNullOrEmpty(ba.Creator.TtdPath))
                {
                    var creatorTtdPhysicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", ba.Creator.TtdPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                    if (File.Exists(creatorTtdPhysicalPath))
                    {
                        await documentService.EmbedTtdMenyerahkanAsync(ba.Id, creatorTtdPhysicalPath);
                        logger.LogInformation($"[AutoApproveJob] TTD Admin Gudang berhasil di-embed ke BA {ba.Id}.");
                    }
                }

                // 5. Kirim notifikasi ke Approver (inbox)
                string msgInbox = $"Dokumen {ba.NomorSurat ?? $"BA-{ba.Id}"} telah disetujui otomatis (PJ melewati batas 2 menit / TESTING) dan membutuhkan otorisasi Anda.";
                await notificationService.SendAsync(ba.MengetahuiId ?? 0, "APPROVAL_REQUIRED", msgInbox, ba.Id);

                // 6. Kirim email ke Approver
                if (!string.IsNullOrEmpty(ba.Mengetahui?.Email))
                {
                    var barangList = ba.Perangkat
                        .Select(p =>
                        {
                            string namaBarang = p.Barang?.NamaBarang ?? "Perangkat";
                            string detail = $"{p.Jumlah} {p.Satuan}";
                            if (!string.IsNullOrEmpty(p.NoSerial)) detail += $" — S/N: {p.NoSerial}";
                            if (!string.IsNullOrEmpty(p.Keterangan)) detail += $" ({p.Keterangan})";
                            return $"{namaBarang} — {detail}";
                        })
                        .ToList();

                    await emailService.SendApprovalRequestAsync(
                        ba.Mengetahui.Email,
                        ba.Mengetahui.Nama ?? "Approver",
                        BaseUrl,
                        ba.Id,
                        ba.Jenis ?? "Berita Acara",
                        barangList
                    );
                }

                logger.LogInformation($"[AutoApproveJob] BA {ba.Id} ({ba.NomorSurat}) berhasil di-auto-approve dan notifikasi dikirim ke Approver.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"[AutoApproveJob] Gagal memproses auto-approve untuk BA ID: {ba.Id}.");
            }
        }
    }
}
