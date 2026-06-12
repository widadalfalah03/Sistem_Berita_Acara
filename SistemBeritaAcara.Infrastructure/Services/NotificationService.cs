using Microsoft.EntityFrameworkCore;
using SistemBeritaAcara.Core.Entities;
using SistemBeritaAcara.Core.Interfaces;
using SistemBeritaAcara.Infrastructure.Data;

namespace SistemBeritaAcara.Infrastructure.Services;

public class NotificationService(AppDbContext db) : INotificationService
{
    public async Task SendAsync(int userId, string tipe, string message, int? baId = null)
    {
        db.Notification.Add(new Notification
        {
            UserId = userId,
            Tipe = tipe,
            Message = message,
            BaId = baId
        });
        await db.SaveChangesAsync();
    }

    public async Task SendToRoleAsync(string role, string tipe, string message, int? baId = null)
    {
        var users = await db.Users
            .Where(u => u.Role == role && !u.IsDeleted)
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var userId in users)
        {
            db.Notification.Add(new Notification
            {
                UserId = userId,
                Tipe = tipe,
                Message = message,
                BaId = baId
            });
        }
        await db.SaveChangesAsync();
    }

    public async Task MarkReadAsync(int notificationId)
    {
        await db.Notification
            .Where(n => n.Id == notificationId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    public async Task MarkAllReadAsync(int userId)
    {
        await db.Notification
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }
}
