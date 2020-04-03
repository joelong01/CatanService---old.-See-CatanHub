using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace CatanService.Models
{
    public enum ResourceType { Sheep, Wood, Ore, Wheat, Brick, Desert, Back, None, Sea, GoldMine };
    public enum DevCardType { Knight, VictoryPoint, YearOfPlenty, RoadBuilding, Monopoly, Unknown };
    public class ResourceCountClass
    {
        public int Wheat { get; set; }
        public int Wood { get; set; }
        public int Ore { get; set; }
        public int Sheep { get; set; }
        public int Brick { get; set; }
        public int GoldMine { get; set; }

        public void Negate()
        {
            Wheat = -Wheat;
            Wood = -Wood;
            Ore = -Ore;
            Sheep = -Sheep;
            Brick = -Brick;
        }

    }

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

    public class PlayerResources
    {
        public string PlayerName { get; set; }
        public string GameName { get; set; }
        public int Wheat { get; set; } = 0;
        public int Wood { get; set; } = 0;
        public int Ore { get; set; } = 0;
        public int Sheep { get; set; } = 0;
        public int Brick { get; set; } = 0;
        public int GoldMine { get; set; } = 0;
        public List<DevelopmentCard> DevCards { get; set; } = new List<DevelopmentCard>();

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

        public void SubtractResources(PlayerResources toSubtract)
        {
            Wheat -= toSubtract.Wheat;
            Wood -= toSubtract.Wood;
            Brick -= toSubtract.Brick;
            Ore -= toSubtract.Ore;
            Sheep -= toSubtract.Sheep;
            GoldMine -= toSubtract.GoldMine;
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

        [JsonIgnore]
        public int TotalResources
        {
            get
            {
                return Wheat + Wood + Brick + Ore + Sheep + GoldMine;
            }
        }


        [JsonIgnore]
        public TaskCompletionSource<object> ResourceUpdateTCS { get; set; } // used in the monitor controller whenever a resource is updated
        public TaskCompletionSource<object> DevCardUpdateTCS { get; set; } // used in the monitor controller whenever a resource is updated
    }
}
