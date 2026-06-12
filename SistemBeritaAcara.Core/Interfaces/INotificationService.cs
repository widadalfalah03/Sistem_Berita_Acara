namespace SistemBeritaAcara.Core.Interfaces;

public interface INotificationService
{
    Task SendAsync(int userId, string tipe, string message, int? baId = null);
    Task SendToRoleAsync(string role, string tipe, string message, int? baId = null);
    Task MarkReadAsync(int notificationId);
    Task MarkAllReadAsync(int userId);
}
