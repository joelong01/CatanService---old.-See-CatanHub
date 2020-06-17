using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

using Catan.Proxy;

using CatanService.Controllers;
using CatanService.State;

using Microsoft.AspNetCore.SignalR;

namespace CatanService

{
    /// <summary>
    ///     these are the signatures of the "OnRecieved" calls on the client
    /// </summary>
    public interface ICatanClient
    {
        #region Methods

        Task AllGames(List<GameInfo> games);

        Task CreateGame(GameInfo gameInfo, string by);

        Task DeleteGame(Guid id, string by);

        Task JoinGame(GameInfo gameInfo, string playerName);

        Task LeaveGame(GameInfo gameInfo, string playerName);

        Task ToAllClients(string message);

        Task ToOneClient(string message);

        #endregion Methods
    }

    public static class SignalRConnectionToGroupsMap
    {
        #region Properties + Fields

        private static readonly ConcurrentDictionary<string, List<string>> Map = new ConcurrentDictionary<string, List<string>>();

        #endregion Properties + Fields



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

    public class CatanHub : Hub<ICatanClient>
    {
        #region Properties

        private static string AllUsers { get; } = "{158B5187-959E-4A81-A8F9-CD9BE0D30300}";

        #endregion Properties

        #region Methods

        public Task BroadcastMessage(string gameId, string message)
        {
            return Clients.Group(gameId).ToAllClients(message);
        }

        /// <summary>
        ///     Create the datastructures needed to communicate across machines
        ///     Typically the first API to call in the service
        ///
        /// </summary>
        /// <param name="gameId">a stringified Guid</param>
        /// <param name="clientPlayerId">the name of the player - must be unique inside of game</param>
        /// <param name="jsonGameInfo">he JSON string representing a GameInfo objec</param>
        /// <returns></returns>
        public async Task CreateGame(GameInfo gameInfo)
        {
            Game game = GameController.Games.GetGame(gameInfo.Id);
            if (game == default)
            {
                GameController.Games.AddGame(gameInfo.Id, new Game() { GameInfo = gameInfo });
                await Groups.AddToGroupAsync(Context.ConnectionId, gameInfo.Id.ToString());
                //
                //  tell *all* the clients that a game was created
                await Clients.All.CreateGame(gameInfo, gameInfo.Creator);
                return;
            }
            //
            //  send the client an error?
        }

        public async Task DeleteGame(Guid id, string by)
        {
            try
            {
                bool success = GameController.Games.DeleteGame(id, out Game game);
                if (!success)
                {
                    //
                    // send error
                    return;
                }
                //
                //  tell *all* the clients that a game was deleted
                await Clients.All.DeleteGame(id, by);
            }
            catch (Exception e)
            {
                // send error
                Console.Out.WriteLine(e.ToString());
            }
        }

        public async Task GetAllGames()
        {
            var games = GameController.Games.GetGames();
            await Clients.Caller.AllGames(games);
        }

        public async Task JoinGame(GameInfo gameInfo, string playerName)
        {
            try
            {
                Game game = GameController.Games.GetGame(gameInfo.Id);

                if (game == null)
                {
                    //
                    //   send error?
                    return;
                }

                bool success = game.NameToPlayerDictionary.TryAdd(playerName, new Player(game.GameLog));
                string gameId = gameInfo.Id.ToString();
                //
                //  add the client to the game SignalR Group
                await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
                //
                //  notify everybody else in the group that somebody has joined the game
                await Clients.Group(gameId).JoinGame(gameInfo, playerName);

                if (!success)
                {
                    //
                    //    error?
                    return;
                }
            }
            catch (Exception e)
            {
                //
                //  client error
                Console.Out.WriteLine(e.ToString());
            }
        }

        public async Task LeaveGame(GameInfo gameInfo, string playerName)
        {
            try
            {
                Game game = GameController.Games.GetGame(gameInfo.Id);

                if (game == null)
                {
                    // send error
                    return;
                }

                if (game.Started)
                {
                    // different error
                    // Description = $"Player '{playerName}' can't be removed from '{gameInfo.Name}' because it has already been started.",

                    return;
                }

                if (game.GameInfo.Creator == playerName)
                {
                    //
                    //    Description = $"The Creator can't leave their own game.",

                    return;
                }

                //
                //  should already be in here since you shoudl have called Monitor()
                bool success = game.NameToPlayerDictionary.TryRemove(playerName, out Player player);

                if (!success)
                {
                    // Description = $"Player '{playerName}' can't be removed from '{gameInfo.Name}'.",
                    return;
                }

                await Clients.Group(gameInfo.Id.ToString()).LeaveGame(gameInfo, playerName);
            }
            catch (Exception e)
            {
                // Description = $"{this.Request.Path} threw an exception. {e}",
                Console.Out.WriteLine(e.ToString());
            }
        }

        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AllUsers);
            await base.OnConnectedAsync();            
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, AllUsers);
            await base.OnDisconnectedAsync(exception);
        }
        public Task SendPrivateMessage(string gameId, string message)
        {
            return Clients.User(gameId).ToOneClient(message);
        }

        #endregion Methods
    }
}