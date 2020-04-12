using CatanSharedModels;
using System;
using System.Collections.Generic;
using System.Threading;


namespace CatanService.State
{

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
        private GameInfo _gameInfo = new GameInfo() { Knight = 0 };// this will force operator == to false w/o causing an exception
        public GameInfo GameInfo
        {
            get => _gameInfo;
            set
            {
                if (value != _gameInfo)
                {
                    _devCards.Clear();
                    _gameInfo = value;
                    lock (_devCardLock)
                    {

                        for (int i = 0; i < _gameInfo.Knight; i++)
                        {
                            _devCards.Add(DevCardType.Knight);
                        }
                        for (int i = 0; i < _gameInfo.VictoryPoint; i++)
                        {
                            _devCards.Add(DevCardType.VictoryPoint);
                        }
                        for (int i = 0; i < _gameInfo.YearOfPlenty; i++)
                        {
                            _devCards.Add(DevCardType.YearOfPlenty);
                        }
                        for (int i = 0; i < _gameInfo.Monopoly; i++)
                        {
                            _devCards.Add(DevCardType.Monopoly);
                        }
                        for (int i = 0; i < _gameInfo.RoadBuilding; i++)
                        {
                            _devCards.Add(DevCardType.RoadBuilding);
                        }

                    }
                }
            }
        }
        public bool Started { get; set; } = false;
        public string Name { get; set; }

        private object _devCardLock = new object();
        private readonly List<DevCardType> _devCards = new List<DevCardType>();
        private Random _rand = new Random((int)DateTime.Now.Ticks);
        private ReaderWriterLockSlim PlayerLock { get; } = new ReaderWriterLockSlim();
        private Dictionary<string, PlayerState> PlayerDictionary { get; } = new Dictionary<string, PlayerState>();
        private List<string> PlayerOrder { get; set; } = new List<string>();
        private int _logSequenceNumber = 0;

        

        public string CurrentPlayer { get; private set; } = "";

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
        public bool TSTryGetPlayer(string playerName, out PlayerState playerState)
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
        public PlayerState GetPlayer(string playerName)
        {
            TSTryGetPlayer(playerName, out PlayerState state);
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
            List<ServiceLogRecord> logList = new List<ServiceLogRecord>();
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
                        logList.Add(new MonopolyLog() { PlayerResources = kvp.Value, Action = ServiceAction.LostToMonopoly, PlayerName = kvp.Value.PlayerName, Count = total, ResourceType = resType, RequestUrl = Url });
                    }

                }
                return total; //NOTE:  We took from the player who played it, but will grant it back to them
            }
            finally
            {
                PlayerLock.ExitReadLock();
                //
                //  can't do this under the Read lock!
                foreach (var o in logList)
                {
                    TSAddLogRecord(o);
                }
            }
        }

        public bool TSSetPlayerResources(string playerName, PlayerState clientState)
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
        ///     Adds a logRecord to every log.  Note that this is by reference, so the logRecord shoudl be read only.
        /// </summary>
        /// <param name="logRecord"></param>
        public bool TSAddLogRecord(ServiceLogRecord logRecord)
        {
            PlayerLock.EnterReadLock();
            try
            {
                logRecord.Sequence = _logSequenceNumber++;
                foreach (var kvp in PlayerDictionary)
                {
                    PlayerState tracker = kvp.Value;
                    tracker.TSAddLogRecord(logRecord);
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

        public string TSNextPlayer()
        {
            return SetCurrentPlayer(+1);
        }
        public string TSPreviousPlayer()
        {
            return SetCurrentPlayer(-1);
        }

        private string SetCurrentPlayer(int n)
        {
            PlayerLock.EnterWriteLock();
            try
            {
                int index = PlayerOrder.IndexOf(CurrentPlayer);
                index += n;
                if (index >= PlayerOrder.Count) index = index - PlayerOrder.Count;
                if (index < 0) index = index + PlayerOrder.Count;
                CurrentPlayer = PlayerOrder[index];
                return CurrentPlayer;

            }
            finally
            {
                PlayerLock.ExitReadLock();
            }
        }

        internal bool TSSetPlayerOrder(List<string> players)
        {

            PlayerLock.EnterWriteLock();
            try
            {
                foreach (var p in PlayerDictionary.Keys)
                {
                    if (players.Contains(p) == false)
                    {
                        return false;
                    }
                }

                if (PlayerDictionary.Keys.Count != players.Count) return false;
                PlayerOrder = players;
                return true;

            }
            finally
            {
                PlayerLock.ExitReadLock();
            }

        }

        public ICollection<string> Players
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

}
