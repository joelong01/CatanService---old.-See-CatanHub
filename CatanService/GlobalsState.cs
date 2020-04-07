using CatanSharedModels;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace CatanService
{


    /// <summary>
    ///     this contains all the state assosiated with a particular player. Note that you have 1 player per client
    ///     so you should have one of these per client.  in theory only one thead at a time should be accessing this
    ///     class, but that just makes the locks cheeper.  i've made them all thread safe in case downstream requirements
    ///     make me need thread safety.
    /// </summary>
    public class ClientState : PlayerResources
    {
        //
        //  a lock to protect the resources
        public ReaderWriterLockSlim ResourceLock { get; } = new ReaderWriterLockSlim(); // protects access to this ResourceTracking
        public TaskCompletionSource<object> ResourceUpdateTaskCompletionSource { get; set; } = null;
        public TaskCompletionSource<object> LogTaskCompletionSource { get; set; } = null;
        private List<ServiceLogEntry> _log = new List<ServiceLogEntry>();
        private TaskCompletionSource<object> _tcs;
        public async Task<List<ServiceLogEntry>> TSWaitForLog()
        {
            if (_tcs != null)
            {
                throw new Exception("the TCS shoudl be null in ClientState!");
            }
            var logCopy = TSGetLogEntries();
            if (logCopy.Count != 0)
            {
                return logCopy;
            }

            _tcs = new TaskCompletionSource<object>();
            await _tcs.Task;
            _tcs = null;
            return TSGetLogEntries();

        }
        public void TSReleaseLogToClient()
        {
            if (_tcs != null) // if this is null, nobody is waiting
            {
                _tcs.SetResult(null);
            }
        }
        public void TSAdd(ClientState toAdd)
        {
            ResourceLock.EnterWriteLock();
            toAdd.ResourceLock.EnterReadLock();
            try
            {
                Wheat += toAdd.Wheat;
                Wood += toAdd.Wood;
                Brick += toAdd.Brick;
                Ore += toAdd.Ore;
                Sheep += toAdd.Sheep;
                GoldMine += toAdd.GoldMine;
            }
            finally
            {
                ResourceLock.ExitWriteLock();
                toAdd.ResourceLock.ExitReadLock();
            }
        }
        public void TSAdd(TradeResources toAdd)
        {
            ResourceLock.EnterWriteLock();
            try
            {
                Wheat += toAdd.Wheat;
                Wood += toAdd.Wood;
                Brick += toAdd.Brick;
                Ore += toAdd.Ore;
                Sheep += toAdd.Sheep;
                GoldMine += toAdd.GoldMine;
            }
            finally
            {
                ResourceLock.ExitWriteLock();
            }
        }
        public void TSAdd(PlayerResources toAdd)
        {
            ResourceLock.EnterWriteLock();
            try
            {
                Wheat += toAdd.Wheat;
                Wood += toAdd.Wood;
                Brick += toAdd.Brick;
                Ore += toAdd.Ore;
                Sheep += toAdd.Sheep;
                GoldMine += toAdd.GoldMine;
            }
            finally
            {
                ResourceLock.ExitWriteLock();
            }
        }
        public int TSAddResource(ResourceType resourceType, int count)
        {
            ResourceLock.EnterWriteLock();
            try
            {
                switch (resourceType)
                {
                    case ResourceType.Sheep:
                        this.Sheep += count;
                        return this.Sheep;
                    case ResourceType.Wood:
                        this.Wood += count;
                        return this.Wood;
                    case ResourceType.Ore:
                        this.Ore += count;
                        return this.Ore;
                    case ResourceType.Wheat:
                        this.Wheat += count;
                        return this.Wheat;
                    case ResourceType.Brick:
                        this.Brick += count;
                        return this.Brick;
                    case ResourceType.GoldMine:
                        this.GoldMine += count;
                        return this.GoldMine;
                    default:
                        throw new Exception($"Unexpected resource type passed into AddResource {resourceType}");
                }
            }
            finally
            {
                ResourceLock.ExitWriteLock();
            }
        }
        public int TSResourceCount(ResourceType resourceType)
        {
            ResourceLock.EnterReadLock();
            try
            {
                switch (resourceType)
                {
                    case ResourceType.Sheep:
                        return this.Sheep;
                    case ResourceType.Wood:
                        return this.Wood;
                    case ResourceType.Ore:
                        return this.Ore;
                    case ResourceType.Wheat:
                        return this.Wheat;
                    case ResourceType.Brick:
                        return this.Brick;
                    case ResourceType.GoldMine:
                        return this.GoldMine;
                    default:
                        throw new Exception($"Unexpected resource type passed into ResourceCount {resourceType}");
                }
            }
            finally
            {
                ResourceLock.ExitReadLock();
            }
        }
        public bool TSPlayDevCard(DevCardType devCardType)
        {


            ResourceLock.EnterWriteLock();
            try
            {
                foreach (var card in DevCards)
                {
                    if (card.DevCard == devCardType && card.Played == false)
                    {
                        card.Played = true;
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                ResourceLock.ExitWriteLock();
            }


        }
        public string TSSerialize()
        {
            ResourceLock.EnterReadLock();
            try
            {
                return TSGlobal.Serialize<PlayerResources>(this);
            }
            finally
            {
                ResourceLock.ExitReadLock();
            }
        }
        public T TSDeserialize<T>(string json)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            return JsonSerializer.Deserialize<T>(json, options);
        }
        /// <summary>
        ///     add a log entry in a thread safe way
        /// </summary>
        /// <param name="logEntry"></param>
        public void TSAddLogEntry(ServiceLogEntry logEntry)
        {
            ResourceLock.EnterWriteLock();
            try
            {

                switch (logEntry.LogType)
                {
                    case ServiceLogType.Resource:
                        logEntry.Data = TSGlobal.Serialize<ResourceLog>(logEntry as ResourceLog);
                        break;
                    case ServiceLogType.Game:
                         logEntry.Data = TSGlobal.Serialize<GameLog>(logEntry as GameLog);                        
                        break;
                    case ServiceLogType.Purchase:
                        logEntry.Data = TSGlobal.Serialize<PurchaseLog>(logEntry as PurchaseLog);
                        break;
                    case ServiceLogType.Trade:
                        logEntry.Data = TSGlobal.Serialize<TradeLog>(logEntry as TradeLog);
                        break;
                    case ServiceLogType.MeritimeTrade:
                        logEntry.Data = TSGlobal.Serialize<MeritimeTradeLog>(logEntry as MeritimeTradeLog);
                        break;
                    case ServiceLogType.Monopoly:
                        logEntry.Data = TSGlobal.Serialize<MonopolyLog>(logEntry as MonopolyLog);
                        break;
                    default:
                        break;
                }

                _log.Add(logEntry);
            }
            finally
            {
                ResourceLock.ExitWriteLock();
            }
        }
        /// <summary>
        ///     return a copy of the log - note that there is a 1:1 correspondence to kvp<game,player> and PlayerResources
        ///     and we keep a copy of the log in every ClientState object.  Each client will have one Monitor, which monitors *all*
        ///     the client changes (because each client runs the UI). easy to cheat by looking at the messages.  will assume good actors
        ///     in the system.
        /// </summary>
        private List<ServiceLogEntry> TSGetLogEntries()
        {
            ResourceLock.EnterWriteLock();
            try
            {
                var ret = new List<ServiceLogEntry>(_log);
                _log.Clear();
                return ret;
            }
            finally
            {
                ResourceLock.ExitWriteLock();
            }
        }
        /// <summary>
        ///     A thread safe way to get a copy of the PlayerResources so that they can be held and serialized in a thread safe way
        /// </summary>
        /// <returns></returns>
        public PlayerResources TSGetPlayerResourcesCopy()
        {
            ResourceLock.EnterReadLock();
            try
            {
                var pr = new PlayerResources()
                {
                    Wheat = this.Wheat,
                    Wood = this.Wood,
                    Brick = this.Brick,
                    Ore = this.Ore,
                    Sheep = this.Sheep,
                    GoldMine = this.GoldMine,
                    PlayerName = this.PlayerName,
                    GameName = this.GameName,
                    Entitlements = new List<Entitlement>(this.Entitlements),
                    DevCards = new List<DevelopmentCard>(this.DevCards),
                };

                return pr;
            }
            finally
            {
                ResourceLock.ExitReadLock();
            }
        }
        public void TSAddEntitlement(Entitlement entitlement)
        {
            ResourceLock.EnterWriteLock();
            try
            {
                this.Entitlements.Add(entitlement);
            }
            finally
            {
                ResourceLock.ExitWriteLock();
            }
        }
        public int TSTakeAll(ResourceType resType)
        {
            int total = 0;
            ResourceLock.EnterWriteLock();
            try
            {
                switch (resType)
                {
                    case ResourceType.Sheep:
                        total += this.Sheep;
                        this.Sheep = 0;
                        break;
                    case ResourceType.Wood:
                        total += this.Wood;
                        this.Wood = 0;
                        break;
                    case ResourceType.Ore:
                        total += this.Ore;
                        this.Ore = 0;
                        break;
                    case ResourceType.Wheat:
                        total += this.Wheat;
                        this.Wheat = 0;
                        break;
                    case ResourceType.Brick:
                        total += this.Brick;
                        this.Brick = 0;
                        break;
                    default:
                        return 0;
                }
                if (total > 0)
                {
                    TSGlobal.PlayerState.TSAddLogEntry(new MonopolyLog() { PlayerResources = this, Action = ServiceAction.LostToMonopoly, PlayerName = this.PlayerName, Count = total, ResourceType = resType });
                }
                return total;
            }
            finally
            {
                ResourceLock.ExitWriteLock();
            }
        }
        public bool TSRemoveEntitlement(Entitlement entitlement)
        {
            ResourceLock.EnterWriteLock();
            try
            {
                return this.Entitlements.Remove(entitlement);
            }
            finally
            {
                ResourceLock.ExitWriteLock();
            }
        }

        public void TSAdd(int count, ResourceType resourceType)
        {
            ResourceLock.EnterWriteLock();
            try
            {
                switch (resourceType)
                {
                    case ResourceType.Sheep:
                        this.Sheep += count;
                        break;
                    case ResourceType.Wood:
                        this.Wood += count;
                        break;
                    case ResourceType.Ore:
                        this.Ore += count;
                        break;
                    case ResourceType.Wheat:
                        this.Wheat += count;
                        break;
                    case ResourceType.Brick:
                        this.Brick += count;
                        break;
                    default:
                        throw new Exception($"Unexpected resource type: {resourceType}");
                }
            }
            finally
            {
                ResourceLock.ExitWriteLock();
            }
        }
    }
    /// <summary>
    ///     this contains all the state about players that is global to the game.  all APIs need to be thread safe.
    /// </summary>
    public class GlobalPlayerState
    {
        private ReaderWriterLockSlim DictionaryLock { get; } = new ReaderWriterLockSlim();
        private Dictionary<PlayerId, ClientState> PlayersToResourcesDictionary { get; } = new Dictionary<PlayerId, ClientState>(new PlayerId()); // given a game, give me a list of users

        public bool TSGetPlayerResources(string gameName, string playerName, out ClientState resources)
        {

            var playerId = new PlayerId
            {
                GameName = gameName.ToLower(),
                PlayerName = playerName.ToLower()
            };

            DictionaryLock.EnterReadLock();
            try
            {
                return PlayersToResourcesDictionary.TryGetValue(playerId, out resources);

            }
            finally
            {
                DictionaryLock.ExitReadLock();
            }
        }
        public void TSSetPlayerResources(string gameName, string playerName, ClientState resources)
        {
            var playerId = new PlayerId
            {
                GameName = gameName.ToLower(),
                PlayerName = playerName.ToLower()
            };

            TSSetPlayerResources(playerId, resources);
        }
        public void TSSetPlayerResources(PlayerId playerId, ClientState resources)
        {
            DictionaryLock.EnterWriteLock();
            try
            {
                PlayersToResourcesDictionary.Add(playerId, resources);
            }
            finally
            {
                DictionaryLock.ExitWriteLock();
            }
        }
        /// <summary>
        ///     Adds a logEntry to every log.  Note that this is by reference, so the logEntry shoudl be read only.
        /// </summary>
        /// <param name="logEntry"></param>
        public void TSAddLogEntry(ServiceLogEntry logEntry)
        {
            DictionaryLock.EnterReadLock();
            try
            {
                switch (logEntry.LogType)
                {
                    case ServiceLogType.Resource:
                        logEntry.Data = TSGlobal.Serialize<ResourceLog>(logEntry as ResourceLog);
                        break;
                    case ServiceLogType.Game:
                        logEntry.Data = TSGlobal.Serialize<GameLog>(logEntry as GameLog);
                        break;
                    case ServiceLogType.Purchase:
                        logEntry.Data = TSGlobal.Serialize<PurchaseLog>(logEntry as PurchaseLog);
                        break;
                    case ServiceLogType.Trade:
                        logEntry.Data = TSGlobal.Serialize<TradeLog>(logEntry as TradeLog);
                        break;
                    case ServiceLogType.TakeCard:
                        logEntry.Data = TSGlobal.Serialize<TakeLog>(logEntry as TakeLog);
                        break;
                    default:
                        break;
                }

                foreach (var kvp in PlayersToResourcesDictionary)
                {
                    ClientState tracker = kvp.Value;
                    tracker.TSAddLogEntry(logEntry);
                }
            }
            finally
            {
                DictionaryLock.ExitReadLock();
            }
        }
        /// <summary>
        ///     Releases all the monitors
        /// </summary>
        /// <param name="gameName"></param>
        /// <returns></returns>
        public void TSReleaseMonitors(string gameName)
        {
            DictionaryLock.EnterReadLock();
            try
            {

                foreach (var kvp in PlayersToResourcesDictionary)
                {
                    if (gameName == kvp.Key.GameName)
                    {
                        kvp.Value.TSReleaseLogToClient();
                    }
                }
            }
            finally
            {
                DictionaryLock.ExitReadLock();
            }
        }
        public List<string> TSGetPlayers(string gameName)
        {
            DictionaryLock.EnterReadLock();
            try
            {
                List<string> players = new List<string>();
                foreach (var kvp in PlayersToResourcesDictionary)
                {
                    if (gameName == kvp.Key.GameName)
                    {
                        players.Add(kvp.Key.PlayerName);
                    }

                }
                return players;
            }
            finally
            {
                DictionaryLock.ExitReadLock();
            }
        }
        public List<string> TSGetGames()
        {
            DictionaryLock.EnterReadLock();
            try
            {
                List<string> games = new List<string>();
                foreach (var kvp in PlayersToResourcesDictionary)
                {
                    if (!games.Contains(kvp.Key.GameName))
                    {
                        games.Add(kvp.Key.GameName);
                    }

                }
                return games;
            }
            finally
            {
                DictionaryLock.ExitReadLock();
            }
        }
        public bool TSDeleteGame(string gameName)
        {
            DictionaryLock.EnterWriteLock();
            try
            {
                List<KeyValuePair<PlayerId, ClientState>> toRemove = new List<KeyValuePair<PlayerId, ClientState>>();
                foreach (KeyValuePair<PlayerId, ClientState> kvp in PlayersToResourcesDictionary)
                {
                    if (kvp.Key.GameName == gameName)
                    {
                        if (!toRemove.Contains(kvp))
                        {
                            toRemove.Add(kvp);
                        }
                    }
                }

                if (toRemove.Count == 0) return false;

                foreach (var kvp in toRemove)
                {
                    PlayersToResourcesDictionary.Remove(kvp.Key);
                }

                return true;
            }
            finally
            {
                DictionaryLock.ExitWriteLock();
            }
        }


    }

    /// <summary>
    ///     State for a specific game
    /// 
    /// </summary>

    public class GameState
    {

    }

    /// <summary>
    ///     This class holds state specific to a particular game 
    /// </summary>
    ///     
    public class GlobalGameState
    {

    }
    public static class TSGlobal
    {
        public static GlobalPlayerState PlayerState { get; } = new GlobalPlayerState();
        public static string Serialize<T>(T obj)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return JsonSerializer.Serialize<T>(obj, options);
        }



    }


}

