﻿using Catan.Proxy;
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

    public class WebSocketData
    {
        private ConcurrentQueue<byte[]> MessageQueue { get; } = new ConcurrentQueue<byte[]>();
        public HttpContext HttpContext { get; set; }
        public WebSocket WebSocket { get; set; }
        public TaskCompletionSource<object> Tcs { get; private set; } = new TaskCompletionSource<object>();

        public WebSocketData(ConcurrentQueue<(string, byte[])> messages)
        {
            foreach(var (_, message) in messages)
            {

                MessageQueue.Enqueue(message);
            }
        }

        public void PostMessage(byte[] msg)
        {
                      
            MessageQueue.Enqueue(msg); // note this is not a copy, but it is readonly
            if (Tcs != null && Tcs.Task.IsCompleted == false)
            {
                Tcs.SetResult(null);
            }
        }
        public async Task PostClientMessages()
        {

            while (true)
            {
                Contract.Assert(Tcs != null);
                Contract.Assert(Tcs.Task.IsCompleted == false);

                if (MessageQueue.Count == 0)
                {
                    await Tcs.Task;
                    if (Tcs.Task.IsCanceled) break;
                }
                while (MessageQueue.TryDequeue(out byte[] byteMessage))
                {
                    Contract.Assert(byteMessage != null);
                    Contract.Assert(WebSocket != null);
                    await WebSocket.SendAsync(byteMessage, WebSocketMessageType.Text, true, CancellationToken.None);
                }

                Tcs = new TaskCompletionSource<object>();

                //  there is a race condition where messages are posted faster than we can send them to the client
                //  and then they stop posting -- so we end up with message in the MessageQueu but we don't have
                //  anything to release them.  this is very unlikely t happen in our scenario as games don't get
                //  created all that often.



                //
                //  wait for the client to ack -- this needs a timeout in case the client dies
                //  We expect the client to get the message and immediately ack back -- if you are dedugging on the client, 
                //  don't break between recieving the message and sending the ack...
                //
                var buffer = new byte[1024 * 4];
                Task<WebSocketReceiveResult> task = WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                try
                {
                    var result = await task.TimeoutAfter<WebSocketReceiveResult>(TimeSpan.FromSeconds(60));
                    string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    WsMessage message = CatanProxy.Deserialize<WsMessage>(json); // need the CatanProxies JsonOptions
                    if (message == null) break;
                    if (message.MessageType != WebSocketMessage.Ack) break;
                    if (result.CloseStatus != null && result.CloseStatus.HasValue == false) break;
                }
                catch(TimeoutException)
                {
                    break;
                }

            }
            //
            //  return will close the socket
        }

      

    }

    public class Games
    {
        private ConcurrentDictionary<string, Game> GameDictionary { get; } = new ConcurrentDictionary<string, Game>();
        private List<WebSocketData> WsCallbacks { get; } = new List<WebSocketData>();
        private ConcurrentQueue<(string, byte[])> HistoricalMessages { get; set; } = new ConcurrentQueue<(string, byte[])>();
        private int ClientMessageSequence = 0;
        public Game GetGame(string gameKey)
        {
            GameDictionary.TryGetValue(gameKey, out Game game);
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

        public bool DeleteGame(string gameId, out Game game)
        {
            bool success = GameDictionary.TryRemove(gameId, out game); ;
            if (success)
            {
                // need to remove the deleted item from the historical queue
                var queu = new ConcurrentQueue<(string, byte[])>();
                while (HistoricalMessages.TryDequeue(out (string id, byte[] message) msg))
                {
                    if (msg.id != gameId)
                    {
                        queu.Enqueue(msg);
                    }
                }
                HistoricalMessages = queu;

                var message = new WsMessage() { Data = new WsGameMessage() { GameInfo = game.GameInfo }, DataType = typeof(WsGameMessage).FullName, MessageType = WebSocketMessage.GameDeleted };
                PostToAllClients(gameId, message);
                
            }
            return success;
        }

        public Player GetPlayer(string gameKey, string playerName)
        {
            var game = GetGame(gameKey);
            if (game == default) return null;
            game.NameToPlayerDictionary.TryGetValue(playerName, out Player player);
            return player;

        }

        public bool AddGame(string gameName, Game game)
        {
            if (GameDictionary.ContainsKey(gameName))
                return false;

            GameDictionary.TryAdd(gameName, game);
            
            PostToAllClients(game.GameInfo.Id, new WsMessage() { Data = new WsGameMessage() { GameInfo = game.GameInfo }, DataType = typeof(WsGameMessage).FullName, MessageType = WebSocketMessage.GameAdded });
            return true;
        }

        private void PostToAllClients(string id, WsMessage message)
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
            await  wsData.PostClientMessages();
            WsCallbacks.Remove(wsData);


        }





    }



}
