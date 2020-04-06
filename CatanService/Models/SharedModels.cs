using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading;

namespace CatanSharedModels
{
    public enum TileOrientation { FaceDown, FaceUp, None };
    public enum HarborType { Sheep, Wood, Ore, Wheat, Brick, ThreeForOne, Uninitialized, None };
    
    public enum Entitlement { Undefined, DevCard, Settlement, City }

    public enum ResourceType { Sheep, Wood, Ore, Wheat, Brick, Desert, Back, None, Sea, GoldMine, VictoryPoint, Knight, YearOfPlenty, RoadBuilding, Monopoly };
    public enum DevCardType { Knight, VictoryPoint, YearOfPlenty, RoadBuilding, Monopoly, Unknown };


    public class PlayerId : IEqualityComparer<PlayerId>
    {
        public string GameName { get; set; }
        public string PlayerName { get; set; }

        public override string ToString()
        {
            return $"{GameName} - {PlayerName}";
        }
        public bool Equals(PlayerId x, PlayerId y)
        {
            if (x is null || y is null) return false;

            if ((x.GameName == y.GameName) && (x.PlayerName == y.PlayerName)) return true;

            return false;
        }

        public int GetHashCode(PlayerId obj)
        {
            return obj.GameName.GetHashCode() + obj.PlayerName.GetHashCode();
        }
    }

    public class DevelopmentCard
    {
        public DevCardType DevCard { get; set; } = DevCardType.Unknown;
        public bool Played { get; set; } = false;
    }

    public class TradeResources : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private int _wheat = 0;
        private int _wood = 0;
        private int _ore = 0;
        private int _sheep = 0;
        private int _brick = 0;
        private int _goldMine = 0;

        public TradeResources() { }

        public TradeResources(TradeResources tradeResources)
        {
            Wheat = this.Wheat;
            Wood = this.Wood;
            Brick = this.Brick;
            Ore = this.Ore;
            Sheep = this.Sheep;
            GoldMine = this.GoldMine;

        }

        public int Wheat
        {
            get
            {
                return _wheat;
            }
            set
            {
                if (value != _wheat)
                {
                    _wheat = value;
                    NotifyPropertyChanged();
                }
            }
        }
        public int Wood
        {
            get
            {
                return _wood;
            }
            set
            {
                if (value != _wood)
                {
                    _wood = value;
                    NotifyPropertyChanged();
                }
            }
        }
        public int Ore
        {
            get
            {
                return _ore;
            }
            set
            {
                if (value != _ore)
                {
                    _ore = value;
                    NotifyPropertyChanged();
                }
            }
        }
        public int Sheep
        {
            get
            {
                return _sheep;
            }
            set
            {
                if (value != _sheep)
                {
                    _sheep = value;
                }
            }
        }

        public int Brick
        {
            get
            {
                return _brick;
            }
            set
            {
                if (value != _brick)
                {
                    _brick = value;
                    NotifyPropertyChanged();
                }
            }
        }
        public int GoldMine
        {
            get
            {
                return _goldMine;
            }
            set
            {
                if (value != _goldMine)
                {
                    _goldMine = value;
                    NotifyPropertyChanged();
                }
            }
        }
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        }
        public TradeResources GetNegated()
        {
            return new TradeResources()
            {
                Wheat = -Wheat,
                Wood = -Wood,
                Ore = -Ore,
                Sheep = -Sheep,
                Brick = -Brick,
            };
        }
    }

    public class PlayerResources : INotifyPropertyChanged
    {
        private string _playerName = "";
        private string _gameName = "";
        private int _wheat = 0;
        private int _wood = 0;
        private int _ore = 0;
        private int _sheep = 0;
        private int _brick = 0;
        private int _goldMine = 0;
        private List<DevelopmentCard> _devCards = new List<DevelopmentCard>();
        private TradeResources _tradeResources = new TradeResources();
        public event PropertyChangedEventHandler PropertyChanged;
        private List<Entitlement> _entitlements = new List<Entitlement>();
        public List<Entitlement> Entitlements
        {
            get
            {
                return _entitlements;
            }
            set
            {
                if (value != _entitlements)
                {
                    _entitlements = value;
                    NotifyPropertyChanged();
                }
            }
        }
        [JsonIgnore]
        public int TotalResources => Wheat + Wood + Brick + Ore + Sheep + GoldMine;

        public PlayerResources(){}
        public List<DevelopmentCard> DevCards
        {
            get
            {
                return _devCards;
            }
            set
            {
                if (value != _devCards)
                {
                    _devCards = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string PlayerName
        {
            get
            {
                return _playerName;
            }
            set
            {
                if (value != _playerName)
                {
                    _playerName = value;
                    NotifyPropertyChanged();
                }
            }
        }
        /**
         * 
         *  the list of all the players playing.  not needed everytime, but it is small in size and useful for the databinding in the clients
         * 
         */
        private List<string> _players = new List<string>();
        public List<string> Players
        {
            get
            {
                return _players;
            }
            set
            {
                if (value != _players)
                {
                    _players = value;
                }
            }
        }
        public TradeResources TradeResources
        {
            get => _tradeResources;
            set
            {
                _tradeResources = value;
                NotifyPropertyChanged();
            }
        }
        private int GetDevCardCount(DevCardType cardType, bool played)
        {
            int count = 0;
            foreach (var cards in _devCards)
            {
                if (cards.DevCard == cardType && cards.Played == played)
                {
                    count++;
                }
            }
            return count;
        }
        public int VictoryPoints => GetDevCardCount(DevCardType.VictoryPoint, false);
        public int KnightsPlayed => GetDevCardCount(DevCardType.Knight, true);
        public int MonopolyPlayed => GetDevCardCount(DevCardType.Monopoly, true);
        public int RoadBuildingPlayed => GetDevCardCount(DevCardType.RoadBuilding, true);
        public int YearOfPlentyPlayed => GetDevCardCount(DevCardType.YearOfPlenty, true);
        public int KnightsNotPlayed => GetDevCardCount(DevCardType.Knight, false);
        public int MonopolyNotPlayed => GetDevCardCount(DevCardType.Monopoly, false);
        public int RoadBuildingNotPlayed => GetDevCardCount(DevCardType.RoadBuilding, false);
        public int YearOfPlentyNotPlayed => GetDevCardCount(DevCardType.YearOfPlenty, false);
        public int TotalNotPlayed => KnightsNotPlayed + RoadBuildingNotPlayed + MonopolyNotPlayed + YearOfPlentyNotPlayed;

        public string GameName
        {
            get
            {
                return _gameName;
            }
            set
            {
                if (value != _gameName)
                {
                    _gameName = value;
                    NotifyPropertyChanged();
                }
            }
        }
        public int Wheat
        {
            get
            {
                return _wheat;
            }
            set
            {
                if (value != _wheat)
                {
                    _wheat = value;
                    NotifyPropertyChanged();
                }
            }
        }
        public int Wood
        {
            get
            {
                return _wood;
            }
            set
            {
                if (value != _wood)
                {
                    _wood = value;
                    NotifyPropertyChanged();
                }
            }
        }
        public int Ore
        {
            get
            {
                return _ore;
            }
            set
            {
                if (value != _ore)
                {
                    _ore = value;
                    NotifyPropertyChanged();
                }
            }
        }
        public int Sheep
        {
            get
            {
                return _sheep;
            }
            set
            {
                if (value != _sheep)
                {
                    _sheep = value;
                }
            }
        }

        public int Brick
        {
            get
            {
                return _brick;
            }
            set
            {
                if (value != _brick)
                {
                    _brick = value;
                    NotifyPropertyChanged();
                }
            }
        }
        public int GoldMine
        {
            get
            {
                return _goldMine;
            }
            set
            {
                if (value != _goldMine)
                {
                    _goldMine = value;
                    NotifyPropertyChanged();
                }
            }
        }
       


        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        }

        public void Negate()
        {
            Wheat = -Wheat;
            Wood = -Wood;
            Ore = -Ore;
            Sheep = -Sheep;
            Brick = -Brick;
        }

        public void AddResources(PlayerResources toAdd)
        {
            Wheat += toAdd.Wheat;
            Wood += toAdd.Wood;
            Brick += toAdd.Brick;
            Ore += toAdd.Ore;
            Sheep += toAdd.Sheep;
            GoldMine += toAdd.GoldMine;
        }


        public int ResourceCount(ResourceType resourceType)
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


        static public string Serialize<T>(T obj)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            return JsonSerializer.Serialize<T>(obj, options);
        }
        static public T Deserialize<T>(string json)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            return JsonSerializer.Deserialize<T>(json, options);
        }
    }
}
