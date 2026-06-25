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

    public async Task SendMagicLinkAsync(string toEmail, string toPjName, int baId, string token, string baseUrl)
    {
        string link = $"{baseUrl.TrimEnd('/')}/pj/sign/{token}";
        string body = $"""
            <h2 style="color: #0f172a;">Pemberitahuan Sistem: Persetujuan Dokumen</h2>
            <p>Yth. <strong>{toPjName}</strong>,</p>
            <p>Terdapat satu dokumen Berita Acara baru yang membutuhkan otorisasi dan tanda tangan digital Anda untuk dapat diproses lebih lanjut.</p>
            
            <div style="text-align: center; margin: 40px 0;">
                <a href="{link}" style="background-color: #0284c7; color: #ffffff; padding: 15px 30px; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 16px; border: 1px solid #0369a1;">Akses Dokumen Sekarang</a>
            </div>
            
            <p style="font-size: 13px; color: #64748b;"><em>Catatan: Akses ini bersifat privat dan token akan otomatis kedaluwarsa dalam waktu 7 hari demi keamanan data.</em></p>
            <br>
            <p>Salam hangat,<br><strong>Administrator IT Pertamina Patra Niaga</strong></p>
            """;

        await SendEmailAsync(toEmail, "Penting: Otorisasi Dokumen Berita Acara Terbaru", body);
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

    public async Task SendApprovalRequestAsync(string toEmail, string approverName, string baseUrl, int baId)
    {
        // Link mengarah ke halaman login dengan returnUrl ke detail BA spesifik
        // Setelah login sebagai Approver, langsung diarahkan ke BA yang perlu di-review
        var returnUrl = Uri.EscapeDataString($"/berita-acara/{baId}");
        var loginLink = $"{baseUrl}/login?returnUrl={returnUrl}";

        string body = $"""
            <h2 style="color: #0f172a;">Dokumen Berita Acara Menunggu Persetujuan Anda</h2>
            <p>Yth. <strong>{approverName}</strong>,</p>
            <p>Penanggung Jawab telah menandatangani Berita Acara dan dokumen tersebut kini menunggu persetujuan Anda.</p>

            <div style="text-align: center; margin: 40px 0;">
                <a href="{loginLink}" style="background-color: #0284c7; color: #ffffff; padding: 15px 30px; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 16px; border: 1px solid #0369a1;">Login & Review Dokumen</a>
            </div>

            <p style="font-size: 13px; color: #64748b;"><em>Anda akan diarahkan ke halaman login. Setelah berhasil login sebagai Approver, sistem akan otomatis membuka dokumen yang perlu ditinjau.</em></p>
            <br>
            <p>Salam hangat,<br><strong>Sistem Informasi Berita Acara</strong></p>
            """;

        await SendEmailAsync(toEmail, "Tindakan Diperlukan: Review & Persetujuan Berita Acara", body);
    }

    public async Task SendApprovalResultAsync(string toEmail, string recipientName, string nomorSurat, bool approved, string? alasan = null)
    {
        string statusColor = approved ? "#16a34a" : "#dc2626";
        string statusText = approved ? "DISETUJUI" : "DITOLAK";
        string alasanSection = (!approved && !string.IsNullOrEmpty(alasan))
            ? $"""
              <div style="background:#fef2f2;border-left:4px solid #dc2626;padding:12px 16px;margin:20px 0;border-radius:4px;">
                  <strong>Alasan Penolakan:</strong><br>
                  <span style="color:#374151;">{alasan}</span>
              </div>
              """
            : string.Empty;

        string body = $"""
            <h2 style="color: {statusColor};">Berita Acara {statusText}</h2>
            <p>Yth. <strong>{recipientName}</strong>,</p>
            <p>Berita Acara dengan nomor surat <strong>{nomorSurat}</strong> telah <strong style="color:{statusColor};">{statusText}</strong> oleh Approver.</p>
            {alasanSection}
            <br>
            <p>Salam hangat,<br><strong>Sistem Informasi Berita Acara</strong></p>
            """;

        string subject = approved
            ? $"Berita Acara {nomorSurat} Telah Disetujui"
            : $"Berita Acara {nomorSurat} Ditolak";

        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendDueDateReminderAsync(string toEmail, string recipientName, string nomorSurat, string pjNama, string tanggalKembali, int daysUntilDue, bool isForPj)
    {
        bool isOverdue = daysUntilDue < 0;
        bool isH1 = daysUntilDue == 1;

        string statusColor = isOverdue ? "#dc2626" : (isH1 ? "#0284c7" : "#d97706");
        string statusBg = isOverdue ? "#fef2f2" : (isH1 ? "#f0f9ff" : "#fffbeb");
        string statusBorder = isOverdue ? "#dc2626" : (isH1 ? "#0284c7" : "#d97706");
        string statusText = isOverdue ? "TELAH JATUH TEMPO" : (isH1 ? "JATUH TEMPO BESOK (H-1)" : "JATUH TEMPO HARI INI");
        
        string actionText;
        string callToAction;

        if (isForPj)
        {
            if (isOverdue)
            {
                actionText = $"Perangkat yang Anda pinjam <strong>telah melewati</strong> batas pengembalian ({tanggalKembali}). Segera kembalikan perangkat tersebut ke bagian IT / Gudang.";
            }
            else if (isH1)
            {
                actionText = $"Perangkat yang Anda pinjam <strong>akan jatuh tempo besok</strong> ({tanggalKembali}). Harap persiapkan perangkat untuk dikembalikan ke bagian IT / Gudang.";
            }
            else
            {
                actionText = $"Perangkat yang Anda pinjam <strong>jatuh tempo hari ini</strong> ({tanggalKembali}). Harap kembalikan perangkat tersebut ke bagian IT / Gudang sebelum akhir hari kerja.";
            }
            callToAction = "Abaikan email ini jika Anda sudah mengembalikan perangkat.";
        }
        else
        {
            if (isOverdue)
            {
                actionText = $"Peminjaman ini <strong>telah melewati</strong> batas pengembalian ({tanggalKembali}). Harap segera hubungi Penanggung Jawab untuk menindaklanjuti pengembalian perangkat.";
            }
            else if (isH1)
            {
                actionText = $"Peminjaman ini <strong>akan jatuh tempo besok</strong> ({tanggalKembali}). Harap informasikan ke Penanggung Jawab agar mempersiapkan pengembalian.";
            }
            else
            {
                actionText = $"Peminjaman ini <strong>jatuh tempo hari ini</strong> ({tanggalKembali}). Harap pantau pengembalian perangkat dari Penanggung Jawab hari ini.";
            }
            callToAction = "Login ke sistem untuk menandai perangkat sebagai sudah dikembalikan setelah menerima fisik perangkat.";
        }

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
            
            <p>{actionText}</p>
            <p>{callToAction}</p>
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
