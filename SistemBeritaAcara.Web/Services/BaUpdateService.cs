using Microsoft.AspNetCore.SignalR;
using SistemBeritaAcara.Web.Hubs;

namespace SistemBeritaAcara.Web.Services;

/// <summary>
/// Singleton service untuk mengirim notifikasi real-time via SignalR.
/// Di-inject ke pages/services yang melakukan perubahan status BA,
/// lalu ia broadcast ke semua klien yang mendengarkan.
/// </summary>
public class BaUpdateService(IHubContext<BaHub> hubContext)
{
    private readonly IHubContext<BaHub> _hub = hubContext;

    /// <summary>
    /// Siarkan ke SEMUA klien bahwa ada BA yang statusnya berubah.
    /// Klien yang mendengarkan event "BaListUpdated" akan reload list mereka.
    /// </summary>
    public async Task NotifyBaListUpdated()
        => await _hub.Clients.All.SendAsync("BaListUpdated");

    /// <summary>
    /// Siarkan ke group specific BA bahwa detail BA tertentu berubah.
    /// Hanya klien di halaman detail BA tersebut yang akan ter-trigger.
    /// </summary>
    public async Task NotifyBaDetailUpdated(int baId)
    {
        await _hub.Clients.Group($"ba-{baId}").SendAsync("BaDetailUpdated");
        await NotifyBaListUpdated(); // juga refresh list di halaman lain
    }
}
