using Catan.Proxy;
using Microsoft.AspNetCore.Http;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace CatanService.State
{

    public static class TaskExtensions
    {
        public static async Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, TimeSpan timeout)
        {

            using (var timeoutCancellationTokenSource = new CancellationTokenSource())
            {

                var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));
                if (completedTask == task)
                {
                    timeoutCancellationTokenSource.Cancel();
                    return await task;  // Very important in order to propagate exceptions
                }
                else
                {
                    throw new TimeoutException();
                }
            }
        }
    }
   

    public class Player
    {
        //
        // when a player joins a game, we create a Player object.  we start with all the Messages for the whole game so that the player can catch up.
        //
        public Player(ConcurrentQueue<CatanMessage> gameLog)
        {
            foreach (var message in gameLog)
            {
                PlayerLog.Enqueue(message);
            }
        }

        private Player() { } // made private so it can't be created by mistake

        [JsonIgnore]
        public ConcurrentQueue<CatanMessage> PlayerLog { get; } = new ConcurrentQueue<CatanMessage>();
        [JsonIgnore]
        public TaskCompletionSource<object> TCS { get; private set; } = null;

        public TaskCompletionSource<object> WsTcs { get; private set; } = null;

        /// <summary>
        ///     in a threadsafe way, return the list of all of the log entries since the last time the API was called.
        /// </summary>
        /// <returns></returns>
        public List<CatanMessage> GetLogEntries()
        {
            var list = new List<CatanMessage>();
            while (PlayerLog.IsEmpty == false)
            {
                if (PlayerLog.TryDequeue(out CatanMessage message))
                {
                    list.Add(message);
                }
            }
            return list;
        }

        public async Task<List<CatanMessage>> WaitForLogEntries()
        {
            var list = GetLogEntries();
            if (list.Count != 0)
            {
                return list;
            }
            TCS = new TaskCompletionSource<object>();
            await TCS.Task;
            TCS = null;
            return GetLogEntries();

        }

        internal void ReleaseLog()
        {
            if (TCS != null && !TCS.Task.IsCompleted)
            {
                TCS.SetResult(true);
            }

            if (WsTcs != null && !WsTcs.Task.IsCompleted)
            {
                WsTcs.SetResult(true);
            }
        }

        internal async Task RegisterWebSocket(HttpContext context, WebSocket webSocket)
        {
            try
            {
                while (true)
                {
                    WsTcs = new TaskCompletionSource<object>();
                    await WsTcs.Task;
                    var list = GetLogEntries();
                    var json = JsonSerializer.SerializeToUtf8Bytes(list, typeof(List<CatanMessage>), CatanProxy.GetJsonOptions());
                    await webSocket.SendAsync(json, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            catch
            {
                //eat it
            }


        }

        
    }


    public class Game
    {

        private int GlobalSequnceNumber = 0;
     
        /// <summary>
        ///     All the logs for the entire game
        /// </summary>
        [JsonIgnore]
        public ConcurrentQueue<CatanMessage> GameLog { get; } = new ConcurrentQueue<CatanMessage>();
        /// <summary>
        ///     Given a playerName (CASE SENSItiVE), get the PlayerObject
        /// </summary>

        public ConcurrentDictionary<string, Player> NameToPlayerDictionary { get; } = new ConcurrentDictionary<string, Player>();
        public GameInfo GameInfo { get; set; }
        public bool Started { get; set; } = false;

       

        public int GetNextSequenceNumber()
        {
            return Interlocked.Increment(ref GlobalSequnceNumber);
        }

        public bool PostLog(CatanMessage message)
        {
            message.Sequence = Interlocked.Increment(ref GlobalSequnceNumber);
            GameLog.Enqueue(message);
            foreach (var player in NameToPlayerDictionary.Values)
            {
                player.PlayerLog.Enqueue(message);
            }
            return true;
        }

        internal void ReleaseLogs()
        {
            foreach (var player in NameToPlayerDictionary.Values)
            {
                player.ReleaseLog();
            }
        }
    }

   

    public class Games
    {
        private ConcurrentDictionary<Guid, Game> GameDictionary { get; } = new ConcurrentDictionary<Guid, Game>();
        private List<WebSocketData> WsCallbacks { get; } = new List<WebSocketData>();
        private ConcurrentQueue<(Guid, byte[])> HistoricalMessages { get; set; } = new ConcurrentQueue<(Guid, byte[])>();
        private int ClientMessageSequence = 0;
        public Game GetGame(Guid id)
        {
            bool exists = GameDictionary.TryGetValue(id, out Game game);
            if (!exists) return null;
            return game;
        }

        public List<GameInfo> GetGames()
        {
            List<GameInfo> games = new List<GameInfo>();
            foreach (var kvp in GameDictionary)
            {
                games.Add(kvp.Value.GameInfo);
            }
            return games;
        }

        public bool DeleteGame(Guid id, out Game game)
        {
            bool success = GameDictionary.TryRemove(id, out game); ;
            if (success)
            {
                // need to remove the deleted item from the historical queue
                var queu = new ConcurrentQueue<(Guid, byte[])>();
                while (HistoricalMessages.TryDequeue(out (Guid id, byte[] message) msg))
                {
                    if (msg.id != id)
                    {
                        queu.Enqueue(msg);
                    }
                }
                HistoricalMessages = queu;

                var message = new WsMessage() { Data = new WsGameMessage() { GameInfo = game.GameInfo }, DataType = typeof(WsGameMessage).FullName, MessageType = CatanWsMessageType.GameDeleted };
                PostToAllClients(id, message);
                
            }
            return success;
        }

        public Player GetPlayer(Guid key, string playerName)
        {
            var game = GetGame(key);
            if (game == default) return null;
            game.NameToPlayerDictionary.TryGetValue(playerName, out Player player);
            return player;

        }

        public bool AddGame(Guid id, Game game)
        {
            if (GameDictionary.ContainsKey(id))
                return false;

            GameDictionary.TryAdd(id, game);
            
            PostToAllClients(id, new WsMessage() { Data = new WsGameMessage() { GameInfo = game.GameInfo }, DataType = typeof(WsGameMessage).FullName, MessageType = CatanWsMessageType.GameAdded });
            return true;
        }

        private void PostToAllClients(Guid id, WsMessage message)
        {
            message.Sequence = Interlocked.Increment(ref ClientMessageSequence);
            var msg = JsonSerializer.SerializeToUtf8Bytes(message, typeof(WsMessage), CatanProxy.GetJsonOptions());
            HistoricalMessages.Enqueue((id, msg));
            foreach (var cb in WsCallbacks)
            {
                cb.PostMessage(msg);
            }


        }


        public async Task RegisterWebSocket(HttpContext context, WebSocket webSocket)
        {
            var wsData = new WebSocketData(HistoricalMessages) { HttpContext = context, WebSocket = webSocket };
            
            WsCallbacks.Add(wsData);
            await  wsData.ProcessMessages();
            WsCallbacks.Remove(wsData);


        }





    }



}
