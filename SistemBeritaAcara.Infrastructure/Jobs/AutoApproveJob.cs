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
    IBACounterService baCounterService,
    IExcelService excelService,
    ILogger<AutoApproveJob> logger)
{
    private string BaseUrl => (configuration["App:PublicUrl"] ?? configuration["App:BaseUrl"] ?? "http://localhost:5000").TrimEnd('/');

    public async Task ProcessSingleAutoApproveAsync(int baId)
    {
        // Cari BA spesifik
        var ba = await db.BeritaAcara
            .Include(b => b.Pj)
            .Include(b => b.Menyerahkan)
            .Include(b => b.Mengetahui)
            .Include(b => b.Creator)
            .Include(b => b.BuktiFotos)
            .Include(b => b.Perangkat).ThenInclude(p => p.Barang)
            .FirstOrDefaultAsync(b => b.Id == baId);

        if (ba == null) return;
        
        // Cek status apakah masih WaitingPJSign
        if (ba.Status != "WaitingPJSign")
        {
            logger.LogInformation($"[AutoApproveJob] BA {baId} tidak jadi di-auto-approve karena status sudah berubah menjadi {ba.Status}.");
            return;
        }

        logger.LogInformation($"[AutoApproveJob] Memulai auto-approve untuk BA ID {ba.Id} secara real-time.");

        try
        {

                // 1. Set info stempel
                ba.TtdPjPath = "images/auto_approve_stamp.png";
                ba.PjSignedAt = DateTime.Now;

                // 2. Generate Nomor Surat
                if (string.IsNullOrEmpty(ba.NomorSurat))
                {
                    var next = await baCounterService.GetNextNomorSuratAsync(ba.Tanggal, ba.Jenis);
                    ba.CounterValue = next.counterValue;
                    ba.NomorSurat = next.nomorSurat;
                }

                // 3. Patch Nomor Surat atau Generate
                var docxPhysicalPath = string.IsNullOrEmpty(ba.DocxPath)
                    ? null
                    : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                          ba.DocxPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                if (docxPhysicalPath != null && File.Exists(docxPhysicalPath))
                {
                    await documentService.PatchNomorSuratAsync(ba.Id, ba.NomorSurat!);
                }
                else
                {
                    ba.DocxPath = await documentService.GenerateDocxAsync(ba);
                }

                // 4. Embed stempel "Automatically Approved"
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

                // 5. Transaksi database & Ekspor Excel
                await using var tx = await db.Database.BeginTransactionAsync();
                ba.Status = "Approved";
                ba.ApprovedAt = DateTime.Now;
                ba.ExcelExported = false;
                
                db.BeritaAcara.Update(ba);
                await db.SaveChangesAsync();

                await excelService.AppendBeritaAcaraToArsipAsync(ba);
                ba.ExcelExported = true;
                await db.SaveChangesAsync();
                await tx.CommitAsync();

                // 6. Kirim notifikasi ke Admin Gudang (Creator)
                string msgInbox = $"Berita Acara {ba.Jenis} {ba.NomorSurat} telah selesai secara otomatis (PJ melewati batas waktu).";
                await notificationService.SendAsync(ba.CreatedBy, "BA_APPROVED", msgInbox, ba.Id);

                // 7. Kirim email ke Admin Gudang dan PJ
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

                if (ba.Creator != null && !string.IsNullOrEmpty(ba.Creator.Email))
                {
                    await emailService.SendApprovalResultAsync(ba.Creator.Email, ba.Creator.Nama ?? ba.Creator.Email, ba.Jenis, true, ba.Id, BaseUrl, isForPj: false, nomorSurat: ba.NomorSurat, barangList: barangList);
                }
                
                if (ba.Pj != null && !string.IsNullOrEmpty(ba.Pj.Email))
                {
                    await emailService.SendApprovalResultAsync(ba.Pj.Email, ba.Pj.Nama, ba.Jenis, true, ba.Id, BaseUrl, isForPj: true, nomorSurat: ba.NomorSurat, barangList: barangList);
                }

                logger.LogInformation($"[AutoApproveJob] BA {ba.Id} ({ba.NomorSurat}) berhasil di-auto-approve dan selesai.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"[AutoApproveJob] Gagal memproses auto-approve untuk BA ID: {baId}.");
            }
    }
}
