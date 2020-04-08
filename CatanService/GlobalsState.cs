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

        private ReaderWriterLockSlim ResourceLock { get; } = new ReaderWriterLockSlim(); // protects access to this ResourceTracking
       
        private List<object> _log = new List<object>();
        private TaskCompletionSource<object> _tcs;
        public async Task<List<object>> TSWaitForLog()
        {
            if (_tcs != null)
            {
                //throw new Exception("the TCS shoudl be null in ClientState!");
                return new List<object>();
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

        public void TSAddDevCard(DevCardType card)
        {
            ResourceLock.EnterWriteLock();
            try
            {
                DevCards.Add(new DevelopmentCard() { DevCard = card, Played = false });
            }
            finally
            {
                ResourceLock.ExitWriteLock();
            }
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
        public void TSAddLogEntry(object helper)
        {
            bool tookLock = false;
            if (!ResourceLock.IsWriteLockHeld)
            { 
                ResourceLock.EnterWriteLock();
                tookLock = true;
            }
            try
            {
                _log.Add(helper);
            }
            finally
            {
                if (tookLock) ResourceLock.ExitWriteLock();
            }
        }
        /// <summary>
        ///     return a copy of the log - note that there is a 1:1 correspondence to kvp<game,player> and PlayerResources
        ///     and we keep a copy of the log in every ClientState object.  Each client will have one Monitor, which monitors *all*
        ///     the client changes (because each client runs the UI). easy to cheat by looking at the messages.  will assume good actors
        ///     in the system.
        /// </summary>
        private List<object> TSGetLogEntries()
        {
            ResourceLock.EnterWriteLock();
            try
            {
                var ret = new List<object>(_log);
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
        public int TSTakeAll(string gameName, string Url, ResourceType resType)
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
                    TSGlobal.PlayerState.TSAddLogEntry(gameName, new MonopolyLog() { PlayerResources = this, Action = ServiceAction.LostToMonopoly, PlayerName = this.PlayerName, Count = total, ResourceType = resType, RequestUrl=Url });
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
        private Dictionary<string, Dictionary<string, ClientState>> GameToPlayerDictionary { get; } = new Dictionary<string, Dictionary<string, ClientState>>();

        public bool TSGetPlayerResources(string gameName, string playerName, out ClientState resources)
        {
            gameName = gameName.ToLower();
            playerName = playerName.ToLower();

            DictionaryLock.EnterReadLock();
            try
            {
                resources = null;
                bool ret = GameToPlayerDictionary.TryGetValue(gameName.ToLower(), out Dictionary<string, ClientState> playerDictionary);
                if (!ret) return false;
                return playerDictionary.TryGetValue(playerName.ToLower(), out resources);

            }
            finally
            {
                DictionaryLock.ExitReadLock();
            }
        }



        public bool TSSetPlayerResources(string gameName, string playerName, ClientState clientState)
        {
            gameName = gameName.ToLower();
            playerName = playerName.ToLower();

            DictionaryLock.EnterWriteLock();
            try
            {

                bool ret = GameToPlayerDictionary.TryGetValue(gameName, out Dictionary<string, ClientState> playerDictionary);
                if (!ret)
                {
                    playerDictionary = new Dictionary<string, ClientState>();
                    GameToPlayerDictionary.Add(gameName, playerDictionary);
                }

                
                ret = playerDictionary.TryGetValue(playerName, out ClientState _);
                if (ret)
                {
                    throw new Exception("You shouldn't add the resources twice!");
                }

                playerDictionary.Add(playerName, clientState);
                return true;
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
        public bool TSAddLogEntry(string gameName, ServiceLogEntry logEntry)
        {
            gameName = gameName.ToLower();
            
            DictionaryLock.EnterReadLock();
            try
            {
                bool ret = GameToPlayerDictionary.TryGetValue(gameName.ToLower(), out Dictionary<string, ClientState> playerDictionary);
                if (!ret) return false;
                foreach (var kvp in playerDictionary)
                {
                    ClientState tracker = kvp.Value;
                    tracker.TSAddLogEntry(logEntry);
                }
                return true;
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
        public bool TSReleaseMonitors(string gameName)
        {
            gameName = gameName.ToLower();
            
            DictionaryLock.EnterReadLock();
            try
            {
                bool ret = GameToPlayerDictionary.TryGetValue(gameName.ToLower(), out Dictionary<string, ClientState> playerDictionary);
                if (!ret) return false;
                foreach (var kvp in playerDictionary)
                {
                    kvp.Value.TSReleaseLogToClient();
                }
                return true;

            }
            finally
            {
                DictionaryLock.ExitReadLock();
            }
        }
        public List<string> TSGetPlayers(string gameName)
        {
            gameName = gameName.ToLower();
      
            DictionaryLock.EnterReadLock();
            try
            {
                List<string> players = new List<string>();
                bool ret = GameToPlayerDictionary.TryGetValue(gameName.ToLower(), out Dictionary<string, ClientState> playerDictionary);
                if (!ret) return players;
                players.AddRange(playerDictionary.Keys);
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
                List<string> games = new List<string>(GameToPlayerDictionary.Keys);                
                return games;
            }
            finally
            {
                DictionaryLock.ExitReadLock();
            }
        }
        public bool TSDeleteGame(string gameName)
        {
            gameName = gameName.ToLower();
            
            DictionaryLock.EnterWriteLock();
            try
            {
                return GameToPlayerDictionary.Remove(gameName);                              
            }
            finally
            {
                DictionaryLock.ExitWriteLock();
            }
        }


    }

    public class GameState
    {
        private object _devCardLock = new object();
        private List<DevCardType> _devCards = new List<DevCardType>();
        private Random _rand = new Random((int)DateTime.Now.Ticks);
        public GameState()
        {
            const int Knights = 13;
            const int VictoryPoint = 6;
            const int YearOfPlenty = 2;
            const int Monopoly = 2;
            const int RoadBuilding = 2;

            lock (_devCardLock)
            {

                for (int i = 0; i < Knights; i++)
                {
                    _devCards.Add(DevCardType.Knight);
                }
                for (int i = 0; i < VictoryPoint; i++)
                {
                    _devCards.Add(DevCardType.VictoryPoint);
                }
                for (int i = 0; i < YearOfPlenty; i++)
                {
                    _devCards.Add(DevCardType.YearOfPlenty);
                }
                for (int i = 0; i < Monopoly; i++)
                {
                    _devCards.Add(DevCardType.Monopoly);
                }
                for (int i = 0; i < RoadBuilding; i++)
                {
                    _devCards.Add(DevCardType.RoadBuilding);
                }

            }

        }

        public DevCardType TSGetDevCard()
        {

            lock (_devCardLock)
            {
                if (_devCards.Count == 0)
                {
                    return DevCardType.Unknown;
                }

                int index = _rand.Next(_devCards.Count - 1);
                var ret = _devCards[index];
                _devCards.RemoveAt(index);
                return ret;
            }
        }
    }

    public static class TSGlobal
    {
        public static GlobalPlayerState PlayerState { get; } = new GlobalPlayerState();
        public static GameState GameState { get; } = new GameState();
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

