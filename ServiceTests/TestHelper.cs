using Catan.Proxy;
using CatanService;
using CatanSharedModels;
using Microsoft.AspNetCore.Mvc.Testing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
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
        public List<List<ServiceLogRecord>> LogCollection { get; } = new List<List<ServiceLogRecord>>();
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
        Timer _timer = null;
        public async Task<List<string>> CreateGame()
        {
            var gameInfo = new GameInfo();
            var response = await Proxy.Register(gameInfo, GameName, "Doug");
            Assert.NotNull(response);
            response = await Proxy.Register(gameInfo, GameName, "Max");
            Assert.NotNull(response);
            response = await Proxy.Register(gameInfo, GameName, "Wallace");
            Assert.NotNull(response);
            response = await Proxy.Register(gameInfo, GameName, "Joe");
            Assert.NotNull(response);
            await Proxy.StartGame(GameName);

            Players = await Proxy.GetUsers(GameName);
            Assert.Equal(4, Players.Count);
            //  _timer = new Timer(OnTimerCallback, _timer, 0, 0);

            return Players;
        }

        private async void OnTimerCallback(object state)
        {
            _timer.Change(-1, -1);
            List<ServiceLogRecord> logs;
            do
            {
                logs = await Proxy.Monitor(this.GameName, Players[0]);
                if (logs != null)
                {
                    LogCollection.Add(logs);
                    break;
                }

            } while (logs != null);

            Debug.WriteLine("Exiting Timer thread");
        }
        public async Task<T> GetLastLogRecord<T>()
        {
            List<ServiceLogRecord> logCollection = await Proxy.GetAllLogs(GameName, Players[0], 0);
            if (logCollection == null)
            {
                Debug.WriteLine($"LogCollection is null! {this.Proxy.LastError} {this.Proxy.LastErrorString}");
            }
            Assert.NotNull(logCollection);
            Assert.NotEmpty(logCollection);
            
            T ret =  (T)(object)logCollection[^1];
            Assert.NotNull(ret);
            return ret;


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
