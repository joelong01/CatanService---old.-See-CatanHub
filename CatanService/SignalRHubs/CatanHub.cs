﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR;

namespace CatanService
{
    public static class SignalRConnectionToGroupsMap
    {
        #region Properties + Fields 

        private static readonly ConcurrentDictionary<string, List<string>> Map = new ConcurrentDictionary<string, List<string>>();

        #endregion Properties + Fields 

        #region Constructors

        #endregion Constructors

        #region Delegates  + Events + Enums

        #endregion Delegates  + Events + Enums

        #region Methods

        public static List<string> Connections(string gameName)
        {
            if (Map.TryGetValue(gameName, out List<string> list))
            {
                return list;
            }

            return new List<string>();
        }

        public static bool TryAddGroup(string connectionId, string gameName)
        {
            List<string> groups;

            if (!Map.TryGetValue(connectionId, out groups))
            {
                return Map.TryAdd(connectionId, new List<string>() { gameName });
            }

            if (!groups.Contains(gameName))
            {
                groups.Add(gameName);
            }

            return true;
        }

        // since for this use case we will only want to get the List of group names
        // when we're removing the mapping - we might as well remove the mapping while
        // we're grabbing the List
        public static bool TryRemoveConnection(string connectionId, out List<string> result)
        {
            return Map.TryRemove(connectionId, out result);
        }

        #endregion Methods

      
    }

    public class CatanHub : Hub
    {
       
        #region Methods

        public Task BroadcastMessage(string gameName, string playerName, string message)
        {
            return Clients.Group(gameName).SendAsync("BroadcastMessage", playerName, message);
        }

        public async Task CreateGame(string gameName, string playerName)
        {
            if (SignalRConnectionToGroupsMap.TryAddGroup(Context.ConnectionId, gameName))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, gameName);
                await Clients.Group(gameName).SendAsync("CreateGame", playerName);
            }
        }

        public async Task DeleteGame(string gameName)
        {
            await Clients.Group(gameName).SendAsync("DeletingGame", gameName);
            foreach (var id in SignalRConnectionToGroupsMap.Connections(gameName))
            {
                await Groups.RemoveFromGroupAsync(id, gameName);
            }
        }

        public async Task JoinGame(string gameName, string playerName)
        {
            if (SignalRConnectionToGroupsMap.TryAddGroup(Context.ConnectionId, gameName))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, gameName);
                await Clients.Group(gameName).SendAsync("JoinedGame", gameName, playerName);
            }

            
        }

        public Task SendPrivateMessage(string playerName, string message)
        {
            return Clients.User(playerName).SendAsync("PrivateMessage", message);
        }

        #endregion Methods

       
    }
}
