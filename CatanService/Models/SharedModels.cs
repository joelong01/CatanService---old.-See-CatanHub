using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace CatanSharedModels
{
    public enum TileOrientation { FaceDown, FaceUp, None };
    public enum HarborType { Sheep, Wood, Ore, Wheat, Brick, ThreeForOne, Uninitialized, None };


    public enum ResourceType { Sheep, Wood, Ore, Wheat, Brick, Desert, Back, None, Sea, GoldMine };
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

        public event PropertyChangedEventHandler PropertyChanged;
        [JsonIgnore]
        public int TotalResources => Wheat + Wood + Brick + Ore + Sheep + GoldMine;

        [JsonIgnore]
        public TaskCompletionSource<object> ResourceUpdateTCS { get; set; } = null;
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
                }
            }
        }

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

        public PlayerResources()
        {


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


        public int AddResource(ResourceType resourceType, int count)
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

    }
}
