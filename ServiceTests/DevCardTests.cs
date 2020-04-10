using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Catan.Proxy;
using CatanService;
using CatanSharedModels;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ServiceTests
{
    public class DevCardTests : IClassFixture<ServiceFixture>
    {
        private readonly ServiceFixture _fixture;


        public DevCardTests(ServiceFixture fixture)
        {
            _fixture = fixture;

        }
        [Fact]
        async Task DevCardPurchase()
        {
            TestHelper helper = new TestHelper();
            using (helper)
            {
                string player = (await helper.CreateGame())[0];

                var tr = new TradeResources()
                {
                    Ore = 25,
                    Wheat = 25,
                    Sheep = 25
                };

                var resources = await helper.GrantResourcesAndAssertResult(player, tr);

                Assert.Empty(resources.DevCards);
                for (int i = 0; i < 25; i++)
                {
                    resources = await helper.Proxy.DevCardPurchase(helper.GameName, player);
                    Assert.Equal(24 - i, resources.Ore);
                    Assert.Equal(24 - i, resources.Wheat);
                    Assert.Equal(24 - i, resources.Sheep);
                    Assert.Equal(i, resources.DevCards.Count - 1);
                    Assert.False(resources.DevCards[i].Played);
                }

                Assert.Equal(0, resources.Wood);
                Assert.Equal(0, resources.Wheat);
                Assert.Equal(0, resources.Sheep);
                Assert.Equal(0, resources.Brick);
                Assert.Equal(0, resources.Ore);
                Assert.Equal(0, resources.GoldMine);
                int vp = 0;
                int yop = 0;
                int knight = 0;
                int mono = 0;
                int rb = 0;

                foreach (var dc in resources.DevCards)
                {
                    switch (dc.DevCard)
                    {
                        case DevCardType.Knight:
                            knight++;
                            break;
                        case DevCardType.VictoryPoint:
                            vp++;
                            break;
                        case DevCardType.YearOfPlenty:
                            yop++;
                            break;
                        case DevCardType.RoadBuilding:
                            rb++;
                            break;
                        case DevCardType.Monopoly:
                            mono++;
                            break;
                        case DevCardType.Unknown:
                        default:
                            Assert.True(false);
                            break;
                    }
                }
                Assert.Equal(14, knight);
                Assert.Equal(5, vp);
                Assert.Equal(2, yop);
                Assert.Equal(2, mono);
                Assert.Equal(2, rb);

                tr.Ore = tr.Wheat = tr.Sheep = 1;
                resources = await helper.Proxy.GrantResources(helper.GameName, player, tr);
                Assert.Equal(1, resources.Ore);
                Assert.Equal(1, resources.Wheat);
                Assert.Equal(1, resources.Sheep);

                resources = await helper.Proxy.DevCardPurchase(helper.GameName, player);
                Assert.Null(resources);
                Assert.Equal(CatanError.DevCardsSoldOut, helper.Proxy.LastError.Error);
                tr = new TradeResources()
                {
                    Wood = 1,
                    Brick = 0
                };
                resources = await helper.Proxy.DevCardPurchase(helper.GameName, player);
                Assert.Null(resources);
                Assert.Equal(CatanError.DevCardsSoldOut, helper.Proxy.LastError.Error);
                tr.Brick = 1;
                for (int i = 0; i < yop; i++)
                {
                    resources = await helper.Proxy.PlayYearOfPlenty(helper.GameName, player, tr);
                    Assert.NotNull(resources);
                    Assert.Equal(1, resources.Ore);
                    Assert.Equal(1, resources.Wheat);
                    Assert.Equal(1, resources.Sheep);
                    Assert.Equal(i + 1, resources.Wood);
                    Assert.Equal(i + 1, resources.Brick);
                    Assert.Equal(0, resources.GoldMine);

                }

                resources = await helper.Proxy.PlayRoadBuilding(helper.GameName, player);
                Assert.NotNull(resources);
                resources = await helper.Proxy.PlayRoadBuilding(helper.GameName, player);
                Assert.NotNull(resources);
                resources = await helper.Proxy.PlayRoadBuilding(helper.GameName, player);
                Assert.Equal(CatanError.NoMoreResource, helper.Proxy.LastError.Error);
            }

        }
        [Fact]
        async Task MonopolyTest()
        {

            TestHelper helper = new TestHelper();
            using (helper)
            {
                var players = await helper.CreateGame();
                var tr = new TradeResources()
                {
                    Ore = 1,
                    Wheat = 1,
                    Sheep = 1,
                    Brick = 1,
                    Wood = 1
                };
                //
                //  give 1 of each resource to everybody
                foreach (string p in players)
                {
                    _ = await helper.GrantResourcesAndAssertResult(p, tr);
                }
                DevelopmentCard monopoly = new DevelopmentCard()
                {
                    Played = false,
                    DevCard = DevCardType.Monopoly,
                };
                tr = new TradeResources()
                {
                    Ore = 1,
                    Wheat = 1,
                    Sheep = 1,
                    Brick = 0,
                    Wood = 0
                };
                while (true) // have to keep buying until i randomly get a monopoly card
                {
                    // buy a dev card
                    var resources = await helper.Proxy.DevCardPurchase(helper.GameName, players[0]);
                    Assert.NotNull(resources);
                    // is it monopoly?
                    if (resources.DevCards.Contains(monopoly))
                    {
                        resources = await helper.Proxy.PlayMonopoly(helper.GameName, players[0], ResourceType.Wood);
                        Assert.Equal(4, resources.Wood);
                        Assert.Equal(0, resources.Ore);
                        Assert.Equal(0, resources.Wheat);
                        Assert.Equal(0, resources.Sheep);
                        Assert.Equal(1, resources.Brick);
                        Assert.Equal(0, resources.GoldMine);
                        break;
                    }
                    //
                    //  get resources for another dev card
                    _ = await helper.GrantResourcesAndAssertResult(players[0], tr);
                }
                for (int i = 1; i < players.Count; i++)
                {
                    var resources = await helper.Proxy.GetResources(helper.GameName, players[i]);
                    Assert.Equal(1, resources.Ore);
                    Assert.Equal(1, resources.Wheat);
                    Assert.Equal(1, resources.Sheep);
                    Assert.Equal(0, resources.Wood);
                    Assert.Equal(1, resources.Brick);
                    Assert.Equal(0, resources.GoldMine);
                }

            }
        }
        [Fact]
        async Task YearOfPlenty()
        {

            TestHelper helper = new TestHelper();
            using (helper)
            {
                var players = await helper.CreateGame();
                var tr = new TradeResources()
                {
                    Ore = 1,
                    Wheat = 1,
                    Sheep = 1,
                    Brick = 1,
                    Wood = 0
                };
                //
                //  give 1 of each resource to everybody
                foreach (string p in players)
                {
                    _ = await helper.GrantResourcesAndAssertResult(p, tr);
                }

                PlayerResources resources = await helper.BuyDevCard(players[0], DevCardType.YearOfPlenty);
                Assert.NotNull(resources);
                resources = await helper.Proxy.PlayYearOfPlenty(helper.GameName, players[0], tr);
                Assert.Null(resources);
                Assert.Equal(CatanError.BadTradeResources, helper.Proxy.LastError.Error);
                tr = new TradeResources()
                {
                    Ore = 1,
                    Wheat = 1,
                    Sheep = 0,
                    Brick = 0,
                    Wood = 0
                };
                resources = await helper.Proxy.PlayYearOfPlenty(helper.GameName, players[0], tr);
                if (resources == null)
                {
                    Debug.WriteLine($"{CatanProxy.Serialize(helper.Proxy.LastError, true)}");
                }
                Assert.NotNull(resources);
                Assert.Equal(2, resources.Ore);
                Assert.Equal(2, resources.Wheat);
                Assert.Equal(1, resources.Sheep);
                Assert.Equal(1, resources.Brick);
                Assert.Equal(0, resources.Wood);
                Assert.Equal(0, resources.GoldMine);
                //
                //  make sure we didn't impact other players
                for (int i = 1; i < players.Count; i++)
                {
                    resources = await helper.Proxy.GetResources(helper.GameName, players[i]);
                    Assert.Equal(1, resources.Ore);
                    Assert.Equal(1, resources.Wheat);
                    Assert.Equal(1, resources.Sheep);
                    Assert.Equal(1, resources.Brick);
                    Assert.Equal(0, resources.Wood);                    
                    Assert.Equal(0, resources.GoldMine);
                }

            }
        }
        [Fact]
        async Task RoadBuilding()
        {

            TestHelper helper = new TestHelper();
            using (helper)
            {
                var players = await helper.CreateGame();
                PlayerResources resources = await helper.BuyDevCard(players[0], DevCardType.RoadBuilding);
                Assert.NotNull(resources);
                Assert.NotEmpty(resources.DevCards);
                var roadBuilding = new DevelopmentCard() { DevCard = DevCardType.RoadBuilding, Played = false };
                DevelopmentCard devCard = null;
                foreach (var card in resources.DevCards)
                {
                    if (card.Equals(roadBuilding))
                    {
                        devCard = card;
                        break;
                    }
                }
                Assert.NotNull(devCard);
                Assert.False(devCard.Played);
                
                resources = await helper.Proxy.PlayRoadBuilding(helper.GameName, players[0]);
                Assert.NotNull(resources);
                Assert.Equal(2, resources.Roads);
                
            }
        }
    }
}
