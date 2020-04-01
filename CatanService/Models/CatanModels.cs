using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

    public class UserId : IEqualityComparer<UserId>
    {
        public string GameName { get; set; }
        public string UserName { get; set; }
        public override string ToString()
        {
            return $"{GameName} - {UserName}";
        }
        public bool Equals([AllowNull] UserId x, [AllowNull] UserId y)
        {
            if (x is null || y is null) return false;

            if ((x.GameName == y.GameName) && (x.UserName == y.UserName)) return true;

            return false;
        }

        public int GetHashCode([DisallowNull] UserId obj)
        {
            return obj.GameName.GetHashCode() + obj.UserName.GetHashCode();
        }
    }

    public class UserResources
    {
        public Dictionary<DevCardType, int> DevCards { get; } = new Dictionary<DevCardType, int>();
        public Dictionary<ResourceType, int> ResourceCards { get; } = new Dictionary<ResourceType, int>();

        public UserResources()
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
    }
}
