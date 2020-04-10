using Catan.Proxy;
using CatanService;
using CatanSharedModels;
using Microsoft.AspNetCore.Mvc.Testing;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ServiceTests
{
    public class TestHelper : IDisposable
    {
        public CatanProxy Proxy { get; }
        public string GameName { get; }
        public GameInfo GameInfo { get; set; } = new GameInfo();
        public List<string> Players { get; set; }
        public TestHelper()
        {
            GameName = Guid.NewGuid().ToString();
            Proxy = new CatanProxy();
            var factory = new CustomWebApplicationFactory<Startup>();
            Proxy.Client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }
        public async Task<List<string>> CreateGame()
        {
            var gameInfo = new GameInfo();
            _ = await Proxy.Register(gameInfo, GameName, "Doug");
            _ = await Proxy.Register(gameInfo, GameName, "Max");
            _ = await Proxy.Register(gameInfo, GameName, "Wallace");
            _ = await Proxy.Register(gameInfo, GameName, "Joe");
            Players = await Proxy.GetUsers(GameName);
            return Players;
        }


        public async Task<PlayerResources> GrantResourcesAndAssertResult(string player, TradeResources tr)
        {
            PlayerResources oldResources = await Proxy.GetResources(GameName, player);
            var resources = await Proxy.GrantResources(GameName, player, tr);
            Assert.Equal(tr.Ore + oldResources.Ore, resources.Ore);
            Assert.Equal(tr.Wheat + oldResources.Wheat, resources.Wheat);
            Assert.Equal(tr.Sheep + oldResources.Sheep, resources.Sheep);
            Assert.Equal(tr.Wood + oldResources.Wood, resources.Wood);
            Assert.Equal(tr.Brick + oldResources.Brick, resources.Brick);
            Assert.Equal(tr.GoldMine + oldResources.GoldMine, resources.GoldMine);
            return resources;
        }

        public async Task<PlayerResources> BuyDevCard(string player, DevCardType devCard)
        {
            var tr = new TradeResources()
            {
                Ore = 1,
                Wheat = 1,
                Sheep = 1,
                Brick = 0,
                Wood = 0
            };
            DevelopmentCard card = new DevelopmentCard()
            {
                Played = false,
                DevCard = devCard,
            };
            PlayerResources resources;
            do
            {                
                _ = await GrantResourcesAndAssertResult(player, tr);
                resources = await Proxy.DevCardPurchase(GameName, player);
                Assert.NotNull(resources);
            } while (resources.DevCards.Contains(card) == false);
            return resources;

        }

        public void Dispose()
        {
            Proxy.DeleteGame(GameName);
        }
    }
}
