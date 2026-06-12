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
            <p>Yth. <strong>{toPjName}</strong>,</p>
            <p>Anda diminta menandatangani Berita Acara. Klik tautan berikut untuk melihat dokumen dan mengunggah tanda tangan Anda:</p>
            <p><a href="{link}">{link}</a></p>
            <p>Tautan ini berlaku selama 7 hari.</p>
            <br><p>Hormat kami,<br>Tim IT PT Pertamina Patra Niaga</p>
            """;

        await SendEmailAsync(toEmail, "Permintaan Tanda Tangan Berita Acara", body);
    }

    public async Task SendNotificationAsync(string toEmail, string subject, string htmlBody)
        => await SendEmailAsync(toEmail, subject, htmlBody);

    public async Task SendTtdUsedNotificationAsync(string toEmail, string pegawaiNama, string nomorSurat)
    {
        string body = $"""
            <p>Yth. <strong>{pegawaiNama}</strong>,</p>
            <p>Tanda tangan Anda telah digunakan pada dokumen Berita Acara dengan nomor surat: <strong>{nomorSurat}</strong>.</p>
            <p>Jika Anda merasa tidak melakukan ini, segera hubungi Admin IT.</p>
            <br><p>Hormat kami,<br>Tim IT PT Pertamina Patra Niaga</p>
            """;

        await SendEmailAsync(toEmail, $"Notifikasi Penggunaan Tanda Tangan - {nomorSurat}", body);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_from));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(_host, _port, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(_user, _pass);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
