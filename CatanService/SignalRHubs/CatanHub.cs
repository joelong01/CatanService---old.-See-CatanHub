using System.Threading.Tasks;
using Catan.Proxy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;


namespace CatanService
{

    public class CatanHub : Hub
    {
        public Task BroadcastMessage(string gameName, string playerName, string message)
        {
            return Clients.Group(gameName).SendAsync("BroadcastMessage", playerName, message);
        }

        public async Task JoinGame(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            
            await Clients.Group(groupName).SendAsync("JoinedGame", $"{Context.ConnectionId} has joined the group {groupName}.");
        }

        public async Task LeaveGame(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

            await Clients.Group(groupName).SendAsync("LeftGame", $"{Context.ConnectionId} has left the group {groupName}.");
        }

        public Task SendPrivateMessage(string playerName, string message)
        {
            return Clients.User(playerName).SendAsync("PrivateMessage", message);
        }
    }

}
