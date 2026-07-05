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
    private readonly string _baseUrl = config["App:PublicUrl"] ?? config["App:BaseUrl"] ?? "http://localhost:5000";

    private static string RenderBarangTable(List<string> barangList)
    {
        if (barangList == null || barangList.Count == 0)
            return "<p><em>Data barang tidak tersedia.</em></p>";

        var rows = string.Join("", barangList.Select((item, i) =>
            $"<tr style=\"background:{((i % 2 == 0) ? "#f5f5f5" : "#ffffff")}\">" +
            $"<td style=\"padding:8px 12px;border:1px solid #cccccc;text-align:center;width:40px;color:#000000;\">{i + 1}</td>" +
            $"<td style=\"padding:8px 12px;border:1px solid #cccccc;color:#000000;\">{item}</td>" +
            $"</tr>"));

        return $"""
            <div style="margin:20px 0;">
                <p style="font-weight:700;font-size:14px;color:#000000;margin-bottom:8px;">Daftar Barang / Perangkat:</p>
                <table style="width:100%;border-collapse:collapse;font-size:14px;">
                    <thead>
                        <tr style="background:#0284c7;">
                            <th style="padding:8px 12px;border:1px solid #0369a1;color:#ffffff;text-align:center;width:40px;">No</th>
                            <th style="padding:8px 12px;border:1px solid #0369a1;color:#ffffff;text-align:left;">Nama Barang / Perangkat</th>
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
            <h2 style="color:#000000;">Pemberitahuan: Tanda Tangan Dokumen Diperlukan</h2>
            <p>Yth. <strong>{toPjName}</strong>,</p>
            <p>Dokumen <strong>Berita Acara {jenisBA}</strong> telah disetujui oleh Reviewer dan kini memerlukan tanda tangan Anda sebagai Penanggung Jawab.</p>

            {barangTable}

            <p>Silakan akses tautan di bawah ini untuk meninjau dan menandatangani dokumen:</p>
            <div style="text-align:center;margin:32px 0;">
                <a href="{link}" style="background-color:#0284c7;color:#ffffff;padding:14px 28px;text-decoration:none;font-weight:bold;font-size:15px;border-radius:6px;display:inline-block;">Akses Dokumen &amp; Tanda Tangan</a>
            </div>

            <p style="color:#cc0000;font-size:13px;"><strong>Perhatian:</strong> Dokumen ini akan diproses secara otomatis oleh sistem apabila tidak ditandatangani dalam waktu 1x24 jam.</p>
            <br>
            <p>Regards,<br><strong>Admin IT</strong></p>
            """;

        await SendEmailAsync(toEmail, $"Tanda Tangan Diperlukan: Berita Acara {jenisBA}", body);
    }

    public async Task SendNotificationAsync(string toEmail, string subject, string htmlBody)
        => await SendEmailAsync(toEmail, subject, htmlBody);

    public async Task SendUserInvitationAsync(string toEmail, string userName, string token, string baseUrl)
    {
        var encodedToken = Uri.EscapeDataString(token);
        var encodedEmail = Uri.EscapeDataString(toEmail);
        string link = $"{baseUrl.TrimEnd('/')}/invitation?email={encodedEmail}&token={encodedToken}";

        string body = $"""
            <h2 style="color:#000000;">Undangan: Setup Akun Sistem Informasi Manajemen Berita Acara</h2>
            <p>Yth. <strong>{userName}</strong>,</p>
            <p>Akun Anda telah didaftarkan oleh Admin IT. Untuk mulai menggunakan sistem, silakan selesaikan pengaturan akun Anda melalui tautan berikut:</p>

            <div style="text-align:center;margin:32px 0;">
                <a href="{link}" style="background-color:#0284c7;color:#ffffff;padding:14px 28px;text-decoration:none;font-weight:bold;font-size:15px;border-radius:6px;display:inline-block;">Selesaikan Setup Akun</a>
            </div>

            <p style="color:#cc0000;font-size:13px;"><strong>Perhatian:</strong><br>Tautan ini hanya berlaku selama 24 jam. Apabila sudah kedaluwarsa, silakan hubungi Admin IT.</p>
            <br>
            <p>Regards,<br><strong>Admin IT</strong></p>
            """;

        await SendEmailAsync(toEmail, "Undangan: Setup Akun Sistem Berita Acara", body);
    }

    public async Task SendPasswordResetLinkAsync(string toEmail, string userName, string token, string baseUrl)
    {
        var encodedToken = Uri.EscapeDataString(token);
        var encodedEmail = Uri.EscapeDataString(toEmail);
        string link = $"{baseUrl.TrimEnd('/')}/reset-password?email={encodedEmail}&token={encodedToken}";

        string body = $"""
            <h2 style="color:#000000;">Permintaan Reset Password</h2>
            <p>Yth. <strong>{userName}</strong>,</p>
            <p>Kami menerima permintaan untuk mengatur ulang password akun Anda. Klik tombol di bawah ini untuk membuat password baru:</p>

            <div style="text-align:center;margin:32px 0;">
                <a href="{link}" style="background-color:#0284c7;color:#ffffff;padding:14px 28px;text-decoration:none;font-weight:bold;font-size:15px;border-radius:6px;display:inline-block;">Reset Password</a>
            </div>

            <p style="color:#cc0000;font-size:13px;"><strong>Perhatian:</strong><br>Tautan ini hanya berlaku selama 24 jam. Apabila Anda tidak pernah meminta reset password, abaikan email ini.</p>
            <br>
            <p>Regards,<br><strong>Admin IT</strong></p>
            """;

        await SendEmailAsync(toEmail, "Permintaan Reset Password - Sistem Berita Acara", body);
    }

    public async Task SendTtdUsedNotificationAsync(string toEmail, string pegawaiNama, string nomorSurat)
    {
        string body = $"""
            <h2 style="color:#000000;">Notifikasi: Tanda Tangan Digital Digunakan</h2>
            <p>Yth. <strong>{pegawaiNama}</strong>,</p>
            <p>Tanda tangan digital Anda telah digunakan pada dokumen Berita Acara dengan nomor surat: <strong>{nomorSurat}</strong>.</p>
            <p style="color:#cc0000;"><strong>Apabila Anda merasa tidak melakukan hal ini, segera hubungi Admin IT.</strong></p>
            <br>
            <p>Regards,<br><strong>Admin IT</strong></p>
            """;

        await SendEmailAsync(toEmail, $"Notifikasi Keamanan: Tanda Tangan Digunakan ({nomorSurat})", body);
    }

    public async Task SendApprovalRequestAsync(string toEmail, string reviewerName, string baseUrl, int baId, string jenisBA, List<string> barangList)
    {
        var loginLink = $"{baseUrl}/berita-acara/{baId}?from=review";
        string barangTable = RenderBarangTable(barangList);

        string body = $"""
            <h2 style="color:#000000;">Dokumen Berita Acara Menunggu Persetujuan Anda</h2>
            <p>Yth. <strong>{reviewerName}</strong>,</p>
            <p>Terdapat dokumen <strong>Berita Acara {jenisBA}</strong> yang telah diajukan oleh Admin Gudang Barang dan memerlukan persetujuan Anda.</p>

            {barangTable}

            <div style="text-align:center;margin:32px 0;">
                <a href="{loginLink}" style="background-color:#0284c7;color:#ffffff;padding:14px 28px;text-decoration:none;font-weight:bold;font-size:15px;border-radius:6px;display:inline-block;">Review Dokumen</a>
            </div>
            <br>
            <p>Regards,<br><strong>Admin IT</strong></p>
            """;

        await SendEmailAsync(toEmail, $"Tindakan Diperlukan: Persetujuan Berita Acara {jenisBA}", body);
    }

    public async Task SendApprovalResultAsync(string toEmail, string recipientName, string jenisBA, bool approved, int baId, string baseUrl, bool isForPj, string? nomorSurat = null, string? alasan = null, List<string>? barangList = null)
    {
        string statusText = approved ? "Sah &amp; Berlaku" : "Ditolak";
        string baLabel = string.IsNullOrEmpty(nomorSurat)
            ? $"Berita Acara {jenisBA}"
            : $"Berita Acara {jenisBA} No. {nomorSurat}";

        string barangTable = (barangList != null && barangList.Count > 0)
            ? RenderBarangTable(barangList)
            : string.Empty;

        string alasanSection = (!approved && !string.IsNullOrEmpty(alasan))
            ? $"<p><strong>Alasan Penolakan:</strong> {alasan}</p>"
            : string.Empty;

        string actionButtons = string.Empty;
        if (approved)
        {
            string pdfLink = $"{baseUrl}/api/ba/{baId}/download";
            actionButtons = $"""
                <div style="text-align:center;margin:32px 0;">
                    <a href="{pdfLink}" style="background-color:#0284c7;color:#ffffff;padding:14px 28px;text-decoration:none;font-weight:bold;font-size:15px;border-radius:6px;display:inline-block;">Download PDF</a>
                </div>
                """;
        }
        else
        {
            string rejectedLink = $"{baseUrl}/berita-acara/{baId}?from=rejected";
            actionButtons = $"""
                <div style="text-align:center;margin:32px 0;">
                    <a href="{rejectedLink}" style="background-color:#dc2626;color:#ffffff;padding:14px 28px;text-decoration:none;font-weight:bold;font-size:15px;border-radius:6px;display:inline-block;">Lihat Dokumen Ditolak</a>
                </div>
                """;
        }

        string regardsName = approved ? "Admin IT" : "Reviewer";

        string body = $"""
            <h2 style="color:#000000;">{baLabel} — {statusText}</h2>
            <p>Yth. <strong>{recipientName}</strong>,</p>
            <p>{baLabel} telah <strong>{statusText}</strong>.</p>
            {alasanSection}
            {barangTable}
            {actionButtons}
            <br>
            <p>Regards,<br><strong>{regardsName}</strong></p>
            """;

        string subject = approved
            ? $"Berita Acara {jenisBA} Telah Disetujui"
            : $"Berita Acara {jenisBA} Ditolak";

        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendDueDateReminderAsync(string toEmail, string recipientName, string nomorSurat, string pjNama, string tanggalKembali, int daysUntilDue, bool isForPj, int baId, string baseUrl, List<string>? barangList = null)
    {
        bool isOverdue = daysUntilDue < 0;
        bool isH1 = daysUntilDue == 1;

        string statusText = isOverdue
            ? "Telah Melewati Batas Pengembalian"
            : (isH1 ? "Jatuh Tempo Besok (H-1)" : "Jatuh Tempo Hari Ini");

        string actionText;
        string callToAction;

        if (isForPj)
        {
            if (isOverdue)
                actionText = $"Perangkat yang Anda pinjam telah melewati batas pengembalian pada <strong>{tanggalKembali}</strong>. Harap segera kembalikan perangkat tersebut ke bagian IT / Gudang.";
            else if (isH1)
                actionText = $"Perangkat yang Anda pinjam akan jatuh tempo pada <strong>{tanggalKembali}</strong> (besok). Harap persiapkan perangkat untuk dikembalikan ke bagian IT / Gudang.";
            else
                actionText = $"Perangkat yang Anda pinjam jatuh tempo pada <strong>{tanggalKembali}</strong> (hari ini). Harap kembalikan perangkat ke bagian IT / Gudang sebelum akhir hari kerja.";

            callToAction = "Abaikan email ini apabila perangkat telah dikembalikan.";
        }
        else
        {
            if (isOverdue)
                actionText = $"Peminjaman ini telah melewati batas pengembalian pada <strong>{tanggalKembali}</strong>. Harap segera hubungi Penanggung Jawab untuk menindaklanjuti.";
            else if (isH1)
                actionText = $"Peminjaman ini akan jatuh tempo pada <strong>{tanggalKembali}</strong> (besok). Harap informasikan kepada Penanggung Jawab agar mempersiapkan pengembalian.";
            else
                actionText = $"Peminjaman ini jatuh tempo pada <strong>{tanggalKembali}</strong> (hari ini). Harap pantau pengembalian perangkat dari Penanggung Jawab.";

            callToAction = "Login ke sistem untuk memperbarui status pengembalian setelah menerima perangkat.";
        }

        string barangTable = (barangList != null && barangList.Count > 0)
            ? RenderBarangTable(barangList)
            : string.Empty;

        string headingColor = "#000000";
        string actionTextColor = isOverdue ? "#cc0000" : "#000000";

        string body = $"""
            <h2 style="color:{headingColor};">Pengingat Peminjaman: {statusText}</h2>
            <p>Yth. <strong>{recipientName}</strong>,</p>
            <p>Berikut informasi peminjaman perangkat yang memerlukan perhatian Anda:</p>

            <table style="width:100%;border-collapse:collapse;font-size:14px;margin:16px 0;border:1px solid #cccccc;">
                <tr><td style="padding:8px 12px;color:#000000;width:180px;border:1px solid #cccccc;">No. Surat BA</td><td style="padding:8px 12px;font-weight:bold;border:1px solid #cccccc;">{nomorSurat}</td></tr>
                <tr style="background:#f5f5f5;"><td style="padding:8px 12px;color:#000000;border:1px solid #cccccc;">Penanggung Jawab</td><td style="padding:8px 12px;font-weight:bold;border:1px solid #cccccc;">{pjNama}</td></tr>
                <tr><td style="padding:8px 12px;color:#000000;border:1px solid #cccccc;">Batas Pengembalian</td><td style="padding:8px 12px;font-weight:bold;color:{actionTextColor};border:1px solid #cccccc;">{tanggalKembali}</td></tr>
            </table>

            {barangTable}

            <p style="color:{actionTextColor};"><strong>{actionText}</strong></p>
            <p>{callToAction}</p>
            <br>
            <p>Regards,<br><strong>Admin IT</strong></p>
            """;

        string subject = isOverdue
            ? $"[OVERDUE] Peminjaman {nomorSurat} Melewati Batas Pengembalian"
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
        message.From.Add(new MailboxAddress("Sistem Informasi Manajemen Berita Acara", _from));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        var plainText = System.Text.RegularExpressions.Regex.Replace(htmlBody, "<.*?>", String.Empty);
        plainText = plainText.Replace("&nbsp;", " ").Trim();

        var fullHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.7; color: #000000; background-color: #f0f0f0; padding: 15px; margin: 0; }}
        .container {{ max-width: 600px; margin: 0 auto; background: #ffffff; padding: 32px 28px; border: 1px solid #cccccc; }}
        h2 {{ margin-top: 0; }}
        .footer {{ margin-top: 40px; font-size: 12px; color: #888888; text-align: center; border-top: 1px solid #dddddd; padding-top: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        {htmlBody}
        <div class='footer'>
            <p>Email ini dihasilkan secara otomatis oleh<br><strong>Sistem Informasi Manajemen Berita Acara PT Pertamina Patra Niaga</strong>.</p>
            <p>Mohon tidak membalas email ini.</p>
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
        client.CheckCertificateRevocation = false;
        await client.ConnectAsync(_host, _port, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(_user, _pass);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
