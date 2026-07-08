using Microsoft.AspNetCore.SignalR;
using SistemBeritaAcara.Web.Hubs;

namespace SistemBeritaAcara.Web.Services;






public class BaUpdateService(IHubContext<BaHub> hubContext)
{
    private readonly IHubContext<BaHub> _hub = hubContext;

    
    
    
    
    public async Task NotifyBaListUpdated()
        => await _hub.Clients.All.SendAsync("BaListUpdated");

    
    
    
    
    public async Task NotifyBaDetailUpdated(int baId)
    {
        await _hub.Clients.Group($"ba-{baId}").SendAsync("BaDetailUpdated");
        await NotifyBaListUpdated(); 
    }
}
