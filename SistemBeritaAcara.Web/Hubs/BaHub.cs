using Microsoft.AspNetCore.SignalR;

namespace SistemBeritaAcara.Web.Hubs;

/// <summary>
/// SignalR Hub untuk menyiarkan (broadcast) perubahan status Berita Acara
/// ke semua klien yang sedang membuka halaman terkait.
/// </summary>
public class BaHub : Hub
{
    /// <summary>
    /// Klien memanggil ini untuk bergabung ke group BA tertentu
    /// (contoh: saat membuka halaman detail BA dengan ID 42).
    /// </summary>
    public async Task JoinBaGroup(int baId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"ba-{baId}");

    /// <summary>
    /// Klien memanggil ini untuk keluar dari group BA tertentu.
    /// </summary>
    public async Task LeaveBaGroup(int baId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ba-{baId}");
}
