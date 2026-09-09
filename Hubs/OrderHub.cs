using Microsoft.AspNetCore.SignalR;

namespace LaundryMVC.Hubs;

public class OrderHub : Hub
{
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }
}
