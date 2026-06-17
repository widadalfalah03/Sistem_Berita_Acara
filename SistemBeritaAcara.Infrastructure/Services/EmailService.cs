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
    private readonly string _baseUrl = config["App:BaseUrl"] ?? "http://localhost:5000";

    public async Task SendMagicLinkAsync(string toEmail, string toPjName, int baId, string token)
    {
        string link = $"{_baseUrl}/pj/sign/{token}";
        string body = $"""
            <h2 style="color: #0f172a;">Pemberitahuan Sistem: Persetujuan Dokumen</h2>
            <p>Halo <strong>{toPjName}</strong>,</p>
            <p>Terdapat satu dokumen Berita Acara baru yang membutuhkan otorisasi dan tanda tangan digital Anda untuk dapat diproses lebih lanjut.</p>
            
            <div style="text-align: center; margin: 40px 0;">
                <a href="{link}" style="background-color: #0284c7; color: #ffffff; padding: 15px 30px; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 16px; border: 1px solid #0369a1;">Akses Dokumen Sekarang</a>
            </div>
            
            <p style="font-size: 13px; color: #64748b;"><em>Catatan: Akses ini bersifat privat dan token akan otomatis kedaluwarsa dalam waktu 7 hari demi keamanan data.</em></p>
            <br>
            <p>Salam hangat,<br><strong>Administrator IT Pertamina</strong></p>
            """;

        await SendEmailAsync(toEmail, "Penting: Otorisasi Dokumen Berita Acara Terbaru", body);
    }

    public async Task SendNotificationAsync(string toEmail, string subject, string htmlBody)
        => await SendEmailAsync(toEmail, subject, htmlBody);

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
        message.From.Add(new MailboxAddress("Sistem Berita Acara IT", _from));
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
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #334155; background-color: #f8fafc; padding: 20px; }}
        .container {{ max-width: 600px; margin: 0 auto; background: #ffffff; padding: 30px; border: 1px solid #e2e8f0; border-radius: 8px; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1); }}
        .footer {{ margin-top: 40px; font-size: 12px; color: #94a3b8; text-align: center; border-top: 1px solid #f1f5f9; padding-top: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        {htmlBody}
        <div class='footer'>
            <p>Email ini dihasilkan secara otomatis oleh <strong>Sistem Berita Acara IT</strong> PT Pertamina Patra Niaga.</p>
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
