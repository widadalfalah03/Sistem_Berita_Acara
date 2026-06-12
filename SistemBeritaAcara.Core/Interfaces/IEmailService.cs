namespace SistemBeritaAcara.Core.Interfaces;

public interface IEmailService
{
    Task SendMagicLinkAsync(string toEmail, string toPjName, int baId, string token);
    Task SendNotificationAsync(string toEmail, string subject, string htmlBody);
    Task SendTtdUsedNotificationAsync(string toEmail, string pegawaiNama, string nomorSurat);
}
