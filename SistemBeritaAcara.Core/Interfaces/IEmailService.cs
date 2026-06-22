namespace SistemBeritaAcara.Core.Interfaces;

public interface IEmailService
{
    Task SendMagicLinkAsync(string toEmail, string toPjName, int baId, string token, string baseUrl);
    Task SendNotificationAsync(string toEmail, string subject, string htmlBody);
    Task SendTtdUsedNotificationAsync(string toEmail, string pegawaiNama, string nomorSurat);
    Task SendApprovalRequestAsync(string toEmail, string approverName, string baseUrl, int baId);
    Task SendApprovalResultAsync(string toEmail, string recipientName, string nomorSurat, bool approved, string? alasan = null);
    Task SendDueDateReminderAsync(string toEmail, string recipientName, string nomorSurat, string pjNama, string tanggalKembali, bool isOverdue, bool isForPj);
    Task SendUserInvitationAsync(string toEmail, string userName, string token, string baseUrl);
    Task SendPasswordResetLinkAsync(string toEmail, string userName, string token, string baseUrl);
}
