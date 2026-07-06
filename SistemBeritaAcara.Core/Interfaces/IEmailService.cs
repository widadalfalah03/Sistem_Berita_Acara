namespace SistemBeritaAcara.Core.Interfaces;

public interface IEmailService
{
    Task SendMagicLinkAsync(string toEmail, string toPjName, int baId, string token, string baseUrl, string jenisBA, List<string> barangList);
    Task SendNotificationAsync(string toEmail, string subject, string htmlBody);
    Task SendTtdUsedNotificationAsync(string toEmail, string pegawaiNama, string nomorSurat);
    Task SendApprovalRequestAsync(string toEmail, string reviewerName, string baseUrl, int baId, string jenisBA, List<string> barangList);
    Task SendApprovalResultAsync(string toEmail, string recipientName, string jenisBA, bool approved, int baId, string baseUrl, bool isForPj, string? nomorSurat = null, string? alasan = null, List<string>? barangList = null);
    Task SendDueDateReminderAsync(string toEmail, string recipientName, string nomorSurat, string pjNama, string tanggalKembali, int daysUntilDue, bool isForPj, int baId, string baseUrl, List<string>? barangList = null);
    Task SendUserInvitationAsync(string toEmail, string userName, string token, string baseUrl);
    Task SendPasswordResetLinkAsync(string toEmail, string userName, string token, string baseUrl);
}
