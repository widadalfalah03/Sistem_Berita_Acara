using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using SistemBeritaAcara.Core.Interfaces;

namespace SistemBeritaAcara.Infrastructure.Services;

public class EmailService(IConfiguration config) : IEmailService
{
    private readonly string _host = config["Email:SmtpHost"] ?? "smtp.mailtrap.io";
    private readonly int _port = int.Parse(config["Email:SmtpPort"] ?? "587");
    private readonly string _user = config["Email:Username"] ?? "";
    private readonly string _pass = config["Email:Password"] ?? "";
    private readonly string _from = config["Email:From"] ?? "noreply@pertamina.com";
    // PublicUrl digunakan untuk link di email agar bisa diakses browser (bukan Docker-internal)
    private readonly string _baseUrl = config["App:PublicUrl"] ?? config["App:BaseUrl"] ?? "http://localhost:5000";

    // Helper: render daftar barang sebagai tabel HTML
    private static string RenderBarangTable(List<string> barangList)
    {
        if (barangList == null || barangList.Count == 0)
            return "<p style=\"color:#64748b;\"><em>Data barang tidak tersedia.</em></p>";

        var rows = string.Join("", barangList.Select((item, i) =>
            $"<tr style=\"background:{((i % 2 == 0) ? "#f8fafc" : "#ffffff")}\">" +
            $"<td style=\"padding:8px 12px;border:1px solid #e2e8f0;color:#64748b;text-align:center;width:40px;\">{i + 1}</td>" +
            $"<td style=\"padding:8px 12px;border:1px solid #e2e8f0;color:#1e293b;\">{item}</td>" +
            $"</tr>"));

        return $"""
            <div style="margin:20px 0;">
                <div style="font-weight:700;font-size:14px;color:#0f172a;margin-bottom:8px;">Daftar Barang / Perangkat:</div>
                <table style="width:100%;border-collapse:collapse;font-size:14px;">
                    <thead>
                        <tr style="background:#0284c7;">
                            <th style="padding:8px 12px;border:1px solid #0369a1;color:white;text-align:center;width:40px;">No</th>
                            <th style="padding:8px 12px;border:1px solid #0369a1;color:white;text-align:left;">Nama Barang / Perangkat</th>
                        </tr>
                    </thead>
                    <tbody>{rows}</tbody>
                </table>
            </div>
            """;
    }

    public async Task SendMagicLinkAsync(string toEmail, string toPjName, int baId, string token, string baseUrl, string jenisBA, List<string> barangList)
    {
        string link = $"{baseUrl.TrimEnd('/')}/pj/sign/{token}";
        string barangTable = RenderBarangTable(barangList);

        string body = $"""
            <h2 style="color: #0f172a;">Pemberitahuan Sistem: Persetujuan Dokumen</h2>
            <p>Yth. <strong>{toPjName}</strong>,</p>
            <p>Terdapat satu dokumen <strong>Berita Acara {jenisBA}</strong> yang membutuhkan otorisasi dan tanda tangan digital Anda untuk dapat diproses lebih lanjut.</p>

            {barangTable}
            
            <div style="text-align: center; margin: 40px 0;">
                <a href="{link}" style="background-color: #0284c7; color: #ffffff; padding: 15px 30px; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 16px; border: 1px solid #0369a1;">Akses Dokumen &amp; Tanda Tangan</a>
            </div>
            
            <p style="font-size: 13px; color: #64748b;"><em>Catatan: Akses ini bersifat privat dan token akan otomatis kedaluwarsa dalam waktu 7 hari demi keamanan data.</em></p>
            <br>
            <p>Salam hangat,<br><strong>Administrator IT Pertamina Patra Niaga</strong></p>
            """;

        await SendEmailAsync(toEmail, $"Penting: Otorisasi Dokumen Berita Acara {jenisBA}", body);
    }

    public async Task SendNotificationAsync(string toEmail, string subject, string htmlBody)
        => await SendEmailAsync(toEmail, subject, htmlBody);

    public async Task SendUserInvitationAsync(string toEmail, string userName, string token, string baseUrl)
    {
        var encodedToken = Uri.EscapeDataString(token);
        var encodedEmail = Uri.EscapeDataString(toEmail);
        string link = $"{baseUrl.TrimEnd('/')}/invitation?email={encodedEmail}&token={encodedToken}";

        string body = $"""
            <h2 style="color: #0f172a;">Selamat Datang di Sistem Informasi Berita Acara</h2>
            <p>Yth. <strong>{userName}</strong>,</p>
            <p>Akun Anda telah didaftarkan oleh Administrator. Untuk menyelesaikan proses pendaftaran dan mulai menggunakan sistem, Anda perlu membuat password baru dan mengatur tanda tangan digital Anda.</p>
            
            <div style="text-align: center; margin: 40px 0;">
                <a href="{link}" style="background-color: #0284c7; color: #ffffff; padding: 15px 30px; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 16px; border: 1px solid #0369a1;">Selesaikan Setup Akun</a>
            </div>
            
            <p style="font-size: 13px; color: #64748b;"><em>Catatan: Link ini hanya berlaku selama 24 jam demi keamanan data. Jika link kedaluwarsa, silakan hubungi Admin IT.</em></p>
            <br>
            <p>Salam hangat,<br><strong>Administrator IT Pertamina Patra Niaga</strong></p>
            """;

        await SendEmailAsync(toEmail, "Undangan Pengguna: Setup Akun Sistem Berita Acara", body);
    }

    public async Task SendPasswordResetLinkAsync(string toEmail, string userName, string token, string baseUrl)
    {
        var encodedToken = Uri.EscapeDataString(token);
        var encodedEmail = Uri.EscapeDataString(toEmail);
        string link = $"{baseUrl.TrimEnd('/')}/reset-password?email={encodedEmail}&token={encodedToken}";

        string body = $"""
            <h2 style="color: #0f172a;">Permintaan Reset Password</h2>
            <p>Yth. <strong>{userName}</strong>,</p>
            <p>Sistem menerima permintaan untuk mengatur ulang password akun Anda. Klik tombol di bawah ini untuk membuat password baru:</p>
            
            <div style="text-align: center; margin: 40px 0;">
                <a href="{link}" style="background-color: #eab308; color: #ffffff; padding: 15px 30px; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 16px; border: 1px solid #ca8a04;">Reset Password</a>
            </div>
            
            <p style="font-size: 13px; color: #64748b;"><em>Catatan: Link ini hanya berlaku selama 24 jam. Jika Anda tidak pernah meminta reset password, abaikan email ini dan akun Anda akan tetap aman.</em></p>
            <br>
            <p>Salam hangat,<br><strong>Administrator IT Pertamina Patra Niaga</strong></p>
            """;

        await SendEmailAsync(toEmail, "Permintaan Reset Password - Sistem Berita Acara", body);
    }

    public async Task SendTtdUsedNotificationAsync(string toEmail, string pegawaiNama, string nomorSurat)
    {
        string body = $"""
            <h2 style="color: #16a34a;">Notifikasi Penggunaan Tanda Tangan</h2>
            <p>Yth. <strong>{pegawaiNama}</strong>,</p>
            <p>Tanda tangan digital Anda telah berhasil di-embed pada dokumen Berita Acara dengan nomor surat: <strong>{nomorSurat}</strong>.</p>
            <p>Jika Anda merasa tidak melakukan proses pengajuan atau persetujuan dokumen ini, segera hubungi Admin IT.</p>
            <br>
            <p>Hormat kami,<br><strong>Tim IT PT Pertamina Patra Niaga</strong></p>
            """;

        await SendEmailAsync(toEmail, $"Notifikasi Keamanan: Tanda Tangan Digunakan ({nomorSurat})", body);
    }

    // ── Email #5: Permintaan Approval ke Reviewer ─────────────────────────────
    public async Task SendApprovalRequestAsync(string toEmail, string reviewerName, string baseUrl, int baId, string jenisBA, List<string> barangList)
    {
        var returnUrl = Uri.EscapeDataString($"/berita-acara/{baId}");
        var loginLink = $"{baseUrl}/login?returnUrl={returnUrl}";
        string barangTable = RenderBarangTable(barangList);

        string body = $"""
            <h2 style="color: #0f172a;">Dokumen Berita Acara Menunggu Persetujuan Anda</h2>
            <p>Yth. <strong>{reviewerName}</strong>,</p>
            <p>Penanggung Jawab telah menandatangani Berita Acara <strong>{jenisBA}</strong> dan dokumen tersebut kini menunggu persetujuan Anda.</p>

            {barangTable}

            <div style="text-align: center; margin: 32px 0;">
                <a href="{loginLink}" style="background-color: #0284c7; color: #ffffff; padding: 15px 30px; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 16px; border: 1px solid #0369a1;">Login &amp; Review Dokumen</a>
            </div>

            <p style="font-size: 13px; color: #64748b;"><em>Anda akan diarahkan ke halaman login. Setelah berhasil login sebagai Reviewer, sistem akan otomatis membuka dokumen yang perlu ditinjau.</em></p>
            <br>
            <p>Salam hangat,<br><strong>Sistem Informasi Berita Acara</strong></p>
            """;

        await SendEmailAsync(toEmail, $"Tindakan Diperlukan: Review & Persetujuan Berita Acara {jenisBA}", body);
    }

    // ── Email #6: Hasil Approval (Disetujui / Ditolak) ───────────────────────
    public async Task SendApprovalResultAsync(string toEmail, string recipientName, string jenisBA, bool approved, int baId, string baseUrl, bool isForPj, string? nomorSurat = null, string? alasan = null, List<string>? barangList = null)
    {
        string statusColor = approved ? "#16a34a" : "#dc2626";
        string statusText = approved ? "DISETUJUI" : "DITOLAK";
        string baLabel = string.IsNullOrEmpty(nomorSurat) ? $"Berita Acara {jenisBA}" : $"Berita Acara {jenisBA} (No. {nomorSurat})";

        string barangTable = (barangList != null && barangList.Count > 0)
            ? RenderBarangTable(barangList)
            : string.Empty;

        string alasanSection = (!approved && !string.IsNullOrEmpty(alasan))
            ? $"""
              <div style="background:#fef2f2;border-left:4px solid #dc2626;padding:12px 16px;margin:20px 0;border-radius:4px;">
                  <strong>Alasan Penolakan:</strong><br>
                  <span style="color:#374151;">{alasan}</span>
              </div>
              """
            : string.Empty;

        string actionButtons;
        if (approved)
        {
            // Tombol download PDF (langsung ke file PDF final)
            string pdfLink = $"{baseUrl}/api/ba/{baId}/download";
            string detailLink = $"{baseUrl}/login?returnUrl={Uri.EscapeDataString($"/berita-acara/{baId}")}";
            
            if (isForPj)
            {
                actionButtons = $"""
                    <div style="text-align:center;margin:32px 0;">
                        <a href="{pdfLink}" style="background-color:#16a34a;color:#ffffff;padding:13px 26px;text-decoration:none;border-radius:8px;font-weight:bold;font-size:15px;border:1px solid #15803d;display:inline-block;">⬇ Download PDF</a>
                    </div>
                    """;
            }
            else
            {
                actionButtons = $"""
                    <div style="text-align:center;margin:32px 0;">
                        <a href="{pdfLink}" style="background-color:#16a34a;color:#ffffff;padding:13px 26px;text-decoration:none;border-radius:8px;font-weight:bold;font-size:15px;border:1px solid #15803d;display:inline-block;margin:8px;">⬇ Download PDF</a>
                        <a href="{detailLink}" style="background-color:#0284c7;color:#ffffff;padding:13px 26px;text-decoration:none;border-radius:8px;font-weight:bold;font-size:15px;border:1px solid #0369a1;display:inline-block;margin:8px;">Lihat Detail BA</a>
                    </div>
                    """;
            }
        }
        else
        {
            if (isForPj)
            {
                // PJ yang ditolak: tidak perlu tombol khusus, hanya informasi
                actionButtons = string.Empty;
            }
            else
            {
                // Admin Gudang yang ditolak: tombol ke menu Rejected
                string rejectedLink = $"{baseUrl}/login?returnUrl={Uri.EscapeDataString("/rejected")}";
                actionButtons = $"""
                    <div style="text-align:center;margin:32px 0;">
                        <a href="{rejectedLink}" style="background-color:#dc2626;color:#ffffff;padding:13px 26px;text-decoration:none;border-radius:8px;font-weight:bold;font-size:15px;border:1px solid #b91c1c;display:inline-block;">Lihat BA yang Ditolak</a>
                    </div>
                    """;
            }
        }

        string recipientNote = isForPj
            ? "<p>Anda menerima email ini sebagai Penanggung Jawab yang terdaftar pada dokumen tersebut.</p>"
            : "<p>Anda menerima email ini sebagai pembuat Berita Acara tersebut.</p>";

        string body = $"""
            <h2 style="color: {statusColor};">{baLabel} {statusText}</h2>
            <p>Yth. <strong>{recipientName}</strong>,</p>
            <p>{baLabel} telah <strong style="color:{statusColor};">{statusText}</strong> oleh Reviewer.</p>
            {alasanSection}
            {barangTable}
            {actionButtons}
            {recipientNote}
            <br>
            <p>Salam hangat,<br><strong>Sistem Informasi Berita Acara</strong></p>
            """;

        string subject = approved
            ? $"Berita Acara {jenisBA} Telah Disetujui"
            : $"Berita Acara {jenisBA} Ditolak";

        await SendEmailAsync(toEmail, subject, body);
    }

    // ── Email #7: Pengingat Jatuh Tempo ──────────────────────────────────────
    public async Task SendDueDateReminderAsync(string toEmail, string recipientName, string nomorSurat, string pjNama, string tanggalKembali, int daysUntilDue, bool isForPj, int baId, string baseUrl, List<string>? barangList = null)
    {
        bool isOverdue = daysUntilDue < 0;
        bool isH1 = daysUntilDue == 1;

        string statusColor = isOverdue ? "#dc2626" : (isH1 ? "#0284c7" : "#d97706");
        string statusBg = isOverdue ? "#fef2f2" : (isH1 ? "#f0f9ff" : "#fffbeb");
        string statusBorder = isOverdue ? "#dc2626" : (isH1 ? "#0284c7" : "#d97706");
        string statusText = isOverdue ? "TELAH JATUH TEMPO" : (isH1 ? "JATUH TEMPO BESOK (H-1)" : "JATUH TEMPO HARI INI");
        
        string actionText;
        string callToAction;
        string actionButtons;

        if (isForPj)
        {
            if (isOverdue)
                actionText = $"Perangkat yang Anda pinjam <strong>telah melewati</strong> batas pengembalian ({tanggalKembali}). Segera kembalikan perangkat tersebut ke bagian IT / Gudang.";
            else if (isH1)
                actionText = $"Perangkat yang Anda pinjam <strong>akan jatuh tempo besok</strong> ({tanggalKembali}). Harap persiapkan perangkat untuk dikembalikan ke bagian IT / Gudang.";
            else
                actionText = $"Perangkat yang Anda pinjam <strong>jatuh tempo hari ini</strong> ({tanggalKembali}). Harap kembalikan perangkat tersebut ke bagian IT / Gudang sebelum akhir hari kerja.";
            
            callToAction = "Abaikan email ini jika Anda sudah mengembalikan perangkat.";
            actionButtons = string.Empty; // PJ tidak perlu tombol sistem
        }
        else
        {
            if (isOverdue)
                actionText = $"Peminjaman ini <strong>telah melewati</strong> batas pengembalian ({tanggalKembali}). Harap segera hubungi Penanggung Jawab untuk menindaklanjuti pengembalian perangkat.";
            else if (isH1)
                actionText = $"Peminjaman ini <strong>akan jatuh tempo besok</strong> ({tanggalKembali}). Harap informasikan ke Penanggung Jawab agar mempersiapkan pengembalian.";
            else
                actionText = $"Peminjaman ini <strong>jatuh tempo hari ini</strong> ({tanggalKembali}). Harap pantau pengembalian perangkat dari Penanggung Jawab hari ini.";
            
            callToAction = "Login ke sistem untuk menandai perangkat sebagai sudah dikembalikan setelah menerima fisik perangkat.";

            // Tombol untuk Admin Gudang: dashboard + detail BA
            string dashboardLink = $"{baseUrl}/login?returnUrl={Uri.EscapeDataString("/dashboard")}";
            string detailLink = $"{baseUrl}/login?returnUrl={Uri.EscapeDataString($"/berita-acara/{baId}")}";
            actionButtons = $"""
                <div style="text-align:center;margin:28px 0;display:flex;gap:16px;justify-content:center;flex-wrap:wrap;">
                    <a href="{detailLink}" style="background-color:{statusColor};color:#ffffff;padding:13px 26px;text-decoration:none;border-radius:8px;font-weight:bold;font-size:15px;">Lihat Detail Peminjaman</a>
                    <a href="{dashboardLink}" style="background-color:#64748b;color:#ffffff;padding:13px 26px;text-decoration:none;border-radius:8px;font-weight:bold;font-size:15px;">Buka Dashboard</a>
                </div>
                """;
        }

        string barangTable = (barangList != null && barangList.Count > 0)
            ? RenderBarangTable(barangList)
            : string.Empty;

        string body = $"""
            <h2 style="color: {statusColor};">Peminjaman Perangkat {statusText}</h2>
            <p>Yth. <strong>{recipientName}</strong>,</p>
            <p>Berikut adalah informasi peminjaman perangkat yang memerlukan perhatian Anda:</p>
            
            <div style="background:{statusBg};border-left:4px solid {statusBorder};padding:16px 20px;margin:20px 0;border-radius:4px;">
                <table style="width:100%;border-collapse:collapse;">
                    <tr><td style="padding:4px 0;color:#64748b;width:160px;">No. Surat BA</td><td style="font-weight:bold;">{nomorSurat}</td></tr>
                    <tr><td style="padding:4px 0;color:#64748b;">Penanggung Jawab</td><td style="font-weight:bold;">{pjNama}</td></tr>
                    <tr><td style="padding:4px 0;color:#64748b;">Batas Pengembalian</td><td style="font-weight:bold;color:{statusColor};">{tanggalKembali}</td></tr>
                </table>
            </div>

            {barangTable}
            
            <p>{actionText}</p>
            <p>{callToAction}</p>
            {actionButtons}
            <br>
            <p>Salam,<br><strong>Sistem Informasi Berita Acara</strong><br>PT Pertamina Patra Niaga</p>
            """;

        string subject = isOverdue
            ? $"[OVERDUE] Peminjaman {nomorSurat} Telah Melewati Batas Pengembalian"
            : (isH1 ? $"[H-1] Pengingat: Peminjaman {nomorSurat} Jatuh Tempo Besok" : $"Pengingat: Peminjaman {nomorSurat} Jatuh Tempo Hari Ini");

        await SendEmailAsync(toEmail, subject, body);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        if (string.IsNullOrEmpty(_user) || string.IsNullOrEmpty(_pass))
        {
            Console.WriteLine($"[MOCK EMAIL] To: {toEmail}, Subject: {subject}");
            Console.WriteLine($"[MOCK EMAIL BODY]: {htmlBody}");
            return;
        }

        var message = new MimeMessage();
        // Set nama pengirim agar terlihat profesional dan tidak terdeteksi spam
        message.From.Add(new MailboxAddress("Sistem Informasi Berita Acara", _from));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        // Buat Fallback Plain Text
        var plainText = System.Text.RegularExpressions.Regex.Replace(htmlBody, "<.*?>", String.Empty);
        plainText = plainText.Replace("&nbsp;", " ").Trim();

        // Template HTML standar lengkap dengan head dan body
        var fullHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #334155; background-color: #f8fafc; padding: 15px; margin: 0; }}
        .container {{ max-width: 600px; margin: 0 auto; background: #ffffff; padding: 20px 15px; border: 1px solid #e2e8f0; border-radius: 8px; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1); }}
        .footer {{ margin-top: 40px; font-size: 12px; color: #94a3b8; text-align: center; border-top: 1px solid #f1f5f9; padding-top: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        {htmlBody}
        <div class='footer'>
            <p>Email ini dihasilkan secara otomatis oleh <strong>Sistem Informasi Berita Acara</strong> PT Pertamina Patra Niaga.</p>
            <p>Mohon tidak membalas email ini (<em>No-Reply</em>).</p>
        </div>
    </div>
</body>
</html>";

        var builder = new BodyBuilder
        {
            TextBody = plainText,
            HtmlBody = fullHtml
        };

        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_host, _port, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(_user, _pass);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
