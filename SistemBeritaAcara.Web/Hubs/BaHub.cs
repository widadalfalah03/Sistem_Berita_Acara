using Microsoft.AspNetCore.SignalR;

namespace SistemBeritaAcara.Web.Hubs;





public class BaHub : Hub
{
    
    
    
    
    public async Task JoinBaGroup(int baId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"ba-{baId}");

    
    
    
    public async Task LeaveBaGroup(int baId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ba-{baId}");
}
