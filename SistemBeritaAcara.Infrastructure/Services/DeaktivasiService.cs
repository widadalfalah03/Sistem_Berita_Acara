using Microsoft.EntityFrameworkCore;
using SistemBeritaAcara.Core.Interfaces;
using SistemBeritaAcara.Infrastructure.Data;

namespace SistemBeritaAcara.Infrastructure.Services;

public class DeaktivasiService(AppDbContext db, INotificationService notificationService) : IDeaktivasiService
{
    public async Task DeaktivasiPegawaiAsync(int pegawaiId)
    {
        var pegawai = await db.Pegawai.FindAsync(pegawaiId)
            ?? throw new InvalidOperationException($"Pegawai {pegawaiId} tidak ditemukan.");

        pegawai.IsAktif = false;

        var linkedUsers = await db.Users
            .Where(u => u.PegawaiId == pegawaiId && !u.IsDeleted && u.Role != "AdminIT")
            .ToListAsync();

        foreach (var user in linkedUsers)
        {
            user.IsDeleted = true;
            user.DeletedAt = DateTime.Now;

            var pendingApprovalBAs = await db.BeritaAcara
                .Where(ba => ba.MengetahuiId == user.Id && ba.Status == "WaitingApproval")
                .ToListAsync();

            if (pendingApprovalBAs.Count > 0)
            {
                string msg = $"Ada {pendingApprovalBAs.Count} BA menunggu approval dari akun yang dinonaktifkan ({user.Nama}).";
                await notificationService.SendToRoleAsync("AdminIT", "REVIEWER_DEACTIVATED", msg);
            }

            var pendingPjSignBAs = await db.BeritaAcara
                .Where(ba => ba.MengetahuiId == user.Id && ba.Status == "WaitingPJSign")
                .ToListAsync();

            if (pendingPjSignBAs.Count > 0)
            {
                string msg = $"Ada {pendingPjSignBAs.Count} BA menunggu TTD PJ yang Reviewernya telah dinonaktifkan ({user.Nama}).";
                await notificationService.SendToRoleAsync("AdminIT", "REVIEWER_DEACTIVATED", msg);
            }
        }

        await db.SaveChangesAsync();
    }
}
