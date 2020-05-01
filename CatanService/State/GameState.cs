using Catan.Proxy;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Threading;


namespace CatanService.State
{

    public class Games
    {
        private ConcurrentDictionary<string, Game> NameToGameDictionary { get; } = new ConcurrentDictionary<string, Game>();

        public Game TSFindOrCreateGame(string gameName, GameInfo gameInfo)
        {


            var game = TSGetGame(gameName);
            if (game != null) return game;
            return TSCreateGame(gameName, gameInfo);


        }
        public Game TSCreateGame(string gameName, GameInfo gameInfo)
        {
            var game = TSGetGame(gameName);
            if (game != null) return null;

            game = new Game()
            {
                GameInfo = gameInfo,
                Name = gameName
            };

            NameToGameDictionary[gameName.ToLower()] = game;
            return game;



        }
        public Game TSGetGame(string gameName)
        {
            string name = gameName.ToLower();
            var _ = NameToGameDictionary.TryGetValue(name, out Game game);
            return game;

        }
        public IEnumerable<string> TSGetGameNames()
        {

            return NameToGameDictionary.Keys;

        }
        public IEnumerable<Game> TSGetGames()
        {

            return NameToGameDictionary.Values;

        }

        public bool TSDeleteGame(string gameName)
        {
            bool ret = NameToGameDictionary.TryRemove(gameName.ToLower(), out Game game);
            if (game != null)
            {
                game.Dispose();                
            }
            return ret;

        }
    }

    /// <summary>
    ///     this contains all the state about players in a particular game
    /// </summary>
    public class Game : IDisposable
    {
        private GameInfo _gameInfo = new GameInfo() { Knight = 0 };// this will force operator == to false w/o causing an exception
        public GameInfo GameInfo
        {
            get => _gameInfo;
            set
            {
                //
                //  The DevCard List has to be populated with the right number of DevCards
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
        private readonly ConcurrentBag<DevCardType> _devCards = new ConcurrentBag<DevCardType> ();
        private Random _rand = new Random((int)DateTime.Now.Ticks);
        private ConcurrentDictionary<string, PlayerState> PlayerDictionary { get; } = new ConcurrentDictionary<string, PlayerState>();
        private ConcurrentDictionary<int, string> PlayerOrderDictionary { get; set; } = new ConcurrentDictionary<int, string>();
        public int CurrentPlayerIndex { get; private set; } = 0;

        public void Dispose()
        {
            foreach (var kvp in PlayerDictionary)
            {
                kvp.Value.Dispose();
            }

            PlayerDictionary.Clear();
            PlayerOrderDictionary.Clear();
        }
        public ICollection<PlayerState> TSGetPlayers()
        {

            return PlayerDictionary.Values;

        }
        public override string ToString()
        {
            return $"GameName:{Name} [Started={Started}] [Type={GameInfo.GameName}]";
        }
        

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

            playerState = null;
            bool ret = PlayerDictionary.TryGetValue(player.ToLower(), out playerState);
            return ret;

        }
        public PlayerState GetPlayer(string playerName)
        {
            TSTryGetPlayer(playerName, out PlayerState state);
            return state;
        }

        public DevCardType TSGetDevCard()
        {
            _devCards.TryTake(out DevCardType ret);
            return ret;

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
        public (int total, Dictionary<string, int> impactedPlayers) TSTakeAll(string Url, ResourceType resType)
        {

            int total = 0;

            Dictionary<string, int> dictionary = new Dictionary<string, int>();

            foreach (var kvp in PlayerDictionary)
            {

                switch (resType)
                {
                    case ResourceType.Sheep:
                        dictionary[kvp.Key] = kvp.Value.Sheep;
                        total += kvp.Value.Sheep;
                        kvp.Value.Sheep = 0;                        
                        break;
                    case ResourceType.Wood:
                        dictionary[kvp.Key] = kvp.Value.Wood;
                        total += kvp.Value.Wood;
                        kvp.Value.Wood = 0;
                        break;
                    case ResourceType.Ore:
                        dictionary[kvp.Key] = kvp.Value.Ore;
                        total += kvp.Value.Ore;
                        kvp.Value.Ore = 0;
                        break;
                    case ResourceType.Wheat:
                        dictionary[kvp.Key] = kvp.Value.Wheat;
                        total += kvp.Value.Wheat;
                        kvp.Value.Wheat = 0;
                        break;
                    case ResourceType.Brick:
                        dictionary[kvp.Key] = kvp.Value.Brick;
                        total += kvp.Value.Brick;
                        kvp.Value.Brick = 0;
                        break;
                    default:
                        throw new ArgumentException("Unknown and unexpected resources type in TSTakeAll.");               
                }
            }

            return (total, dictionary); //NOTE:  We took from the player who played it, but will grant it back to them after this returns

        }

        public bool TSSetPlayerResources(string playerName, PlayerState clientState)
        {
            playerName = playerName.ToLower();
            return PlayerDictionary.TryAdd(playerName, clientState);
        }

        /// <summary>
        ///     Adds a logRecord to every log.  Note that this is by reference, so the logRecord shoudl be read only.
        /// </summary>
        /// <param name="logRecord"></param>
        public bool TSAddLogRecord(LogHeader logRecord)
        {

            
            foreach (var kvp in PlayerDictionary)
            {
                PlayerState tracker = kvp.Value;
                tracker.TSAddLogRecord(logRecord);
            }
            return true;
        }
        /// <summary>
        ///     Releases all the monitors
        /// </summary>
        /// <param name="gameName"></param>
        /// <returns></returns>
        public bool TSReleaseMonitors()
        {
            foreach (var kvp in PlayerDictionary)
            {
                kvp.Value.TSReleaseLogToClient();
            }
            return true;


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
            lock (this)
            {
                int index = CurrentPlayerIndex;
                index += n;
                if (index >= PlayerOrderDictionary.Count) index -= PlayerOrderDictionary.Count;
                if (index < 0) index += PlayerOrderDictionary.Count;
                CurrentPlayerIndex = index;
                return PlayerOrderDictionary[CurrentPlayerIndex];
            }
            


        }

        internal bool TSSetPlayerOrder(List<string> players)
        {

            PlayerOrderDictionary.Clear();
            for (int i=0; i< players.Count; i++)
            {
                PlayerOrderDictionary[i] = players[i];
            } 

            return true;

        }

       

        public ICollection<string> Players
        {
            get
            {
               return PlayerDictionary.Keys;

            }
        }



    }

}
