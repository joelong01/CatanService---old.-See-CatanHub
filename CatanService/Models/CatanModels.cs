using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace CatanService.Models
{
    public enum ResourceType { Sheep, Wood, Ore, Wheat, Brick, Desert, Back, None, Sea, GoldMine };
    public enum DevCardType { Knight, VictoryPoint, YearOfPlenty, RoadBuilding, Monopoly };
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
        public bool Equals([AllowNull] PlayerId x, [AllowNull] PlayerId y)
        {
            if (x is null || y is null) return false;

            if ((x.GameName == y.GameName) && (x.PlayerName == y.PlayerName)) return true;

            return false;
        }

        public int GetHashCode([DisallowNull] PlayerId obj)
        {
            return obj.GameName.GetHashCode() + obj.PlayerName.GetHashCode();
        }
    }

    public class PlayerResources
    {
        public Dictionary<DevCardType, int> DevCards { get; } = new Dictionary<DevCardType, int>();
        public Dictionary<ResourceType, int> ResourceCards { get; } = new Dictionary<ResourceType, int>();
        public string PlayerName { get; set; }
        public string GameName { get; set; }

        public PlayerResources()
        {
            foreach (ResourceType key in Enum.GetValues(typeof(ResourceType)))
            {
                ResourceCards[key] = 0;
            }

            foreach (DevCardType key in Enum.GetValues(typeof(DevCardType)))
            {
                DevCards[key] = 0;
            }

            
        }
        public ResourceCountClass ResourceCount
        {
            get
            {
                ResourceCountClass ret = new ResourceCountClass
                {
                    GoldMine = ResourceCards[ResourceType.GoldMine],
                    Wood = ResourceCards[ResourceType.Wood],
                    Brick = ResourceCards[ResourceType.Brick],
                    Sheep = ResourceCards[ResourceType.Sheep],
                    Wheat = ResourceCards[ResourceType.Wheat],
                    Ore = ResourceCards[ResourceType.Ore]
                };

                return ret;
            }
        }

        [JsonIgnore]
        public int TotalResources
        {
            get
            {
                int count = 0;
                foreach (var kvp in ResourceCards)
                {
                    if (kvp.Key == ResourceType.GoldMine) continue;
                    count += kvp.Value;
                }
                return count;
            }
        }

        [JsonIgnore]
        public WebSocket WebSocket { get; set; }
        
        [JsonIgnore]
        public TaskCompletionSource<object> TCS { get; set; }
    }
}
