using CatanSharedModels;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private TaskCompletionSource<object> _tcs = null;
        private ReaderWriterLockSlim TCSLock { get; } = new ReaderWriterLockSlim(); // protects access to this ResourceTracking

        public async Task<List<object>> TSWaitForLog()
        {
            Debug.WriteLine($"Waiting for log for {PlayerName}");
            var logCopy = new List<object>();
            try
            {

                if (_tcs == null)
                {
                    
                    _tcs = new TaskCompletionSource<object>();
                    
                }
                else
                {
                    Debug.WriteLine($"{PlayerName} has a non null TCS!");

                }
                await _tcs.Task;
                
                _tcs = null;
                

                logCopy =  TSGetLogEntries();
                return logCopy;
            }
            finally
            {
                Debug.WriteLine($"returning log for {PlayerName} Count={logCopy.Count} ");
            } 

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
            Debug.WriteLine($"Releasing log for {PlayerName}");
            //TCSLock.EnterWriteLock();
            try
            {
                if (_tcs != null) // if this is null, nobody is waiting
                {
                    _tcs.SetResult(null);
                }
            }
            finally
            {
                if (TCSLock.IsWriteLockHeld)
                {
                    TCSLock.ExitWriteLock();
                }
                Debug.WriteLine($"Released log for {PlayerName}");
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
                return CatanSerializer.Serialize<PlayerResources>(this);
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
            
            if (!ResourceLock.IsWriteLockHeld)
            {
                ResourceLock.EnterWriteLock();
            
            }
            try
            {                
                _log.Add(helper);
                Debug.WriteLine($"Added log for {PlayerName}. [LogId={((ServiceLogEntry)helper).LogId}] LogCount = {_log.Count}. LogType={((ServiceLogEntry)helper).LogType}");
            }
            finally
            {
                if (ResourceLock.IsWriteLockHeld)
                {
                    ResourceLock.ExitWriteLock();
                }
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

    public class Games
    {
        private Dictionary<string, Game> NameToGameDictionary { get; } = new Dictionary<string, Game>();
        private ReaderWriterLockSlim GameLock { get; } = new ReaderWriterLockSlim();
        public Game TSFindOrCreateGame(string gameName, GameInfo gameInfo)
        {

            try
            {
                var game = TSGetGame(gameName);
                if (game != null) return game;
                GameLock.EnterWriteLock();
                game = new Game()
                {
                    GameInfo = gameInfo
                };

                NameToGameDictionary[gameName.ToLower()] = game;
                return game;
            }
            finally
            {
                if (GameLock.IsWriteLockHeld)
                {
                    GameLock.ExitWriteLock();
                }

            }
        }
        public Game TSGetGame(string gameName)
        {
            GameLock.EnterReadLock();
            try
            {
                string name = gameName.ToLower();
                bool ret = NameToGameDictionary.TryGetValue(name, out Game game);
                return game;
            }
            finally
            {
                GameLock.ExitReadLock();
            }
        }
        public IEnumerable<string> TSGetGames()
        {
            GameLock.EnterReadLock();
            try
            {
                return NameToGameDictionary.Keys;
            }
            finally
            {
                GameLock.ExitReadLock();
            }
        }

        public bool TSDeleteGame(string gameName)
        {
            gameName = gameName.ToLower();

            GameLock.EnterWriteLock();
            try
            {
                return NameToGameDictionary.Remove(gameName);
            }
            finally
            {
                GameLock.ExitWriteLock();
            }
        }
    }

    /// <summary>
    ///     this contains all the state about players in a particular game
    /// </summary>
    public class Game
    {
        public GameInfo GameInfo { get; set; } = null;
        public bool Started { get; set; } = false;
        public string Name { get; set; }

        private object _devCardLock = new object();
        private readonly List<DevCardType> _devCards = new List<DevCardType>();
        private Random _rand = new Random((int)DateTime.Now.Ticks);
        private ReaderWriterLockSlim PlayerLock { get; } = new ReaderWriterLockSlim();
        private Dictionary<string, ClientState> PlayerDictionary { get; } = new Dictionary<string, ClientState>();



        public Game()
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
        public bool TSTryGetPlayer(string playerName, out ClientState playerState)
        {

            var player = playerName.ToLower();

            PlayerLock.EnterReadLock();
            try
            {
                playerState = null;
                bool ret = PlayerDictionary.TryGetValue(player.ToLower(), out playerState);
                return ret;

            }
            finally
            {
                PlayerLock.ExitReadLock();
            }
        }
        public ClientState GetPlayer(string playerName)
        {
            TSTryGetPlayer(playerName, out ClientState state);
            return state;
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
        /// <summary>
        ///     keeps track of how many resources the game has handed out
        /// </summary>
        /// <param name="resType"></param>
        /// <returns></returns>
        public bool TSGetResource(ResourceType resType)
        {
            return true;
        }
        public void TSReturnResource(ResourceType resType)
        {

        }
        public int TSTakeAll(string Url, ResourceType resType)
        {

            int total = 0;
            PlayerLock.EnterReadLock();

            try
            {
                foreach (var kvp in PlayerDictionary)
                {

                    switch (resType)
                    {
                        case ResourceType.Sheep:
                            total += kvp.Value.Sheep;
                            kvp.Value.Sheep = 0;
                            break;
                        case ResourceType.Wood:
                            total += kvp.Value.Wood;
                            kvp.Value.Wood = 0;
                            break;
                        case ResourceType.Ore:
                            total += kvp.Value.Ore;
                            kvp.Value.Ore = 0;
                            break;
                        case ResourceType.Wheat:
                            total += kvp.Value.Wheat;
                            kvp.Value.Wheat = 0;
                            break;
                        case ResourceType.Brick:
                            total += kvp.Value.Brick;
                            kvp.Value.Brick = 0;
                            break;
                        default:
                            return 0;
                    }

                    if (total > 0)
                    {
                        TSAddLogEntry(new MonopolyLog() { PlayerResources = kvp.Value, Action = ServiceAction.LostToMonopoly, PlayerName = kvp.Value.PlayerName, Count = total, ResourceType = resType, RequestUrl = Url });
                    }

                }
                return total; //NOTE:  We took from the player who played it, but will grant it back to them
            }
            finally
            {
                PlayerLock.ExitReadLock();
            }
        }

        public bool TSSetPlayerResources(string playerName, ClientState clientState)
        {

            playerName = playerName.ToLower();

            PlayerLock.EnterWriteLock();
            try
            {
                return PlayerDictionary.TryAdd(playerName, clientState);

            }
            finally
            {
                PlayerLock.ExitWriteLock();
            }

        }

        /// <summary>
        ///     Adds a logEntry to every log.  Note that this is by reference, so the logEntry shoudl be read only.
        /// </summary>
        /// <param name="logEntry"></param>
        public bool TSAddLogEntry(ServiceLogEntry logEntry)
        {
            PlayerLock.EnterReadLock();
            try
            {
                
                foreach (var kvp in PlayerDictionary)
                {
                    ClientState tracker = kvp.Value;
                    tracker.TSAddLogEntry(logEntry);
                }
                return true;
            }
            finally
            {
                PlayerLock.ExitReadLock();
            }
        }
        /// <summary>
        ///     Releases all the monitors
        /// </summary>
        /// <param name="gameName"></param>
        /// <returns></returns>
        public bool TSReleaseMonitors()
        {
            PlayerLock.EnterReadLock();
            try
            {

                foreach (var kvp in PlayerDictionary)
                {
                    kvp.Value.TSReleaseLogToClient();
                }
                return true;

            }
            finally
            {
                PlayerLock.ExitReadLock();
            }
        }
        public IEnumerable<string> Players
        {
            get
            {
                PlayerLock.EnterReadLock();
                try
                {
                    return PlayerDictionary.Keys;

                }
                finally
                {
                    PlayerLock.ExitReadLock();
                }
            }
        }



    }


    /// <summary>
    ///     This class contains the state for the service.  
    ///     
    ///     TSGlobal
    ///              Games
    ///                 Game1
    ///                 Game2
    ///                 Game3
    ///                     Players
    ///                         Player1
    ///                         Player2
    ///                         Player3
    /// </summary>
    public static class TSGlobal
    {
        public static Games Games { get; } = new Games();
        public static Game GetGame(string gameName) { return Games.TSGetGame(gameName); }
    }


}

