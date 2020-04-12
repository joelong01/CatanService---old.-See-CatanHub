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
            int maxCards = 2;
            GameInfo gameInfo = new GameInfo()
            {
                YearOfPlenty = maxCards,
                Knight = maxCards,
                VictoryPoint = maxCards,
                Monopoly = maxCards,
                RoadBuilding = maxCards
            };



            using (var helper = new TestHelper(gameInfo))
            {
                string player = (await helper.CreateGame())[0]; // game is now started

                //
                //  get enough resources to buy all the cards - 5 * maxCards worth of devcarts
                var tr = new TradeResources()
                {
                    Ore = maxCards * 5,
                    Wheat = maxCards * 5,
                    Sheep = maxCards * 5
                };

                var resources = await helper.GrantResourcesAndAssertResult(player, tr);

                Assert.Empty(resources.DevCards);

                for (int i = 0; i < maxCards * 5; i++)
                {
                    resources = await helper.Proxy.DevCardPurchase(helper.GameName, player);
                    Assert.Equal(maxCards * 5 - i - 1, resources.Ore);
                    Assert.Equal(maxCards * 5 - i - 1, resources.Wheat);
                    Assert.Equal(maxCards * 5 - i - 1, resources.Sheep);
                    Assert.Equal(i, resources.DevCards.Count - 1);
                    Assert.False(resources.DevCards[i].Played);
                    Assert.Null(helper.Proxy.LastError);
                    Assert.Empty(helper.Proxy.LastErrorString);
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
                //
                //  these are for the "normal" game
                Assert.Equal(gameInfo.Knight, knight);
                Assert.Equal(gameInfo.VictoryPoint, vp);
                Assert.Equal(gameInfo.YearOfPlenty, yop);
                Assert.Equal(gameInfo.Monopoly, mono);
                Assert.Equal(gameInfo.RoadBuilding, rb);

                //
                //  try to buy a card w/ no resources
                resources = await helper.Proxy.DevCardPurchase(helper.GameName, player);
                Assert.Null(resources);
                Assert.NotNull(helper.Proxy.LastError);
                Assert.NotEmpty(helper.Proxy.LastErrorString);
                Assert.Equal(CatanError.DevCardsSoldOut, helper.Proxy.LastError.Error);

                //
                //  grant resources for a devcard
                tr.Ore = tr.Wheat = tr.Sheep = 1;
                resources = await helper.Proxy.GrantResources(helper.GameName, player, tr);
                Assert.Equal(1, resources.Ore);
                Assert.Equal(1, resources.Wheat);
                Assert.Equal(1, resources.Sheep);
                Assert.Null(helper.Proxy.LastError);
                Assert.Empty(helper.Proxy.LastErrorString);

                //
                // try to buy when you have resources -- still get an error
                resources = await helper.Proxy.DevCardPurchase(helper.GameName, player);
                Assert.Null(resources);
                Assert.NotNull(helper.Proxy.LastError);
                Assert.NotEmpty(helper.Proxy.LastErrorString);
                Assert.Equal(CatanError.DevCardsSoldOut, helper.Proxy.LastError.Error);

                //
                //  setup to play YoP
                tr = new TradeResources()
                {
                    Wood = 1,
                    Brick = 1
                };

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

            GameInfo gameInfo = new GameInfo()
            {
                YearOfPlenty = 0,
                Knight = 0,
                VictoryPoint = 0,
                Monopoly = 1,
                RoadBuilding = 0
            };

            using (var helper = new TestHelper(gameInfo))
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

                // buy a dev card - gameInfo says there is only 1 of them and it is Monopoly

                var resources = await helper.Proxy.DevCardPurchase(helper.GameName, players[0]);
                Assert.NotNull(resources);
                Assert.Contains(monopoly, resources.DevCards);
                resources = await helper.Proxy.PlayMonopoly(helper.GameName, players[0], ResourceType.Wood);
                Assert.Equal(4, resources.Wood);
                Assert.Equal(0, resources.Ore);
                Assert.Equal(0, resources.Wheat);
                Assert.Equal(0, resources.Sheep);
                Assert.Equal(1, resources.Brick);
                Assert.Equal(0, resources.GoldMine);


                for (int i = 1; i < players.Count; i++)
                {
                    resources = await helper.Proxy.GetResources(helper.GameName, players[i]);
                    Assert.Equal(1, resources.Ore);
                    Assert.Equal(1, resources.Wheat);
                    Assert.Equal(1, resources.Sheep);
                    Assert.Equal(0, resources.Wood);
                    Assert.Equal(1, resources.Brick);
                    Assert.Equal(0, resources.GoldMine);
                }
                //
                //  try to play it again
                resources = await helper.Proxy.PlayMonopoly(helper.GameName, players[0], ResourceType.Wood);
                Assert.Null(resources);
                Assert.NotEmpty(helper.Proxy.LastErrorString);
                Assert.NotNull(helper.Proxy.LastError);
                Assert.Equal(CatanError.NoResource, helper.Proxy.LastError.Error);

            }
        }
        [Fact]
        async Task YearOfPlenty()
        {
            GameInfo gameInfo = new GameInfo()
            {
                YearOfPlenty = 1,
                Knight = 0,
                VictoryPoint = 0,
                Monopoly = 0,
                RoadBuilding = 0
            };

            using (var helper = new TestHelper(gameInfo))
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
                DevelopmentCard YoP = new DevelopmentCard()
                {
                    Played = false,
                    DevCard = DevCardType.YearOfPlenty,
                };
                PlayerResources resources = await helper.BuyDevCard(players[0], DevCardType.YearOfPlenty);
                Assert.NotNull(resources);
                Assert.Contains(YoP, resources.DevCards);

                resources = await helper.Proxy.PlayYearOfPlenty(helper.GameName, players[0], tr);
                Assert.Null(resources);
                Assert.Equal(CatanError.BadTradeResources, helper.Proxy.LastError.Error);
                Assert.NotNull(helper.Proxy.LastError.CantanRequest.Body);
                Assert.Equal(BodyType.TradeResources, helper.Proxy.LastError.CantanRequest.BodyType);
                tr = helper.Proxy.LastError.CantanRequest.Body as TradeResources;
                Assert.NotNull(tr);
                tr = new TradeResources()
                {
                    Ore = 1,
                    Wheat = 1,
                    Sheep = 0,
                    Brick = 0,
                    Wood = 0
                };
                resources = await helper.Proxy.PlayYearOfPlenty(helper.GameName, players[0], tr);
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

            GameInfo gameInfo = new GameInfo()
            {
                YearOfPlenty = 0,
                Knight = 0,
                VictoryPoint = 0,
                Monopoly = 0,
                RoadBuilding = 1
            };

            using (var helper = new TestHelper(gameInfo))
            {
                var players = await helper.CreateGame();
                var tr = new TradeResources()
                {
                    Ore = 1,
                    Wheat = 1,
                    Sheep = 1
                };

                _ = await helper.GrantResourcesAndAssertResult(players[0], tr);
                PlayerResources resources = await helper.BuyDevCard(players[0], DevCardType.RoadBuilding);
                Assert.Equal(0, resources.Roads);
                Assert.NotNull(resources);
                Assert.NotEmpty(resources.DevCards);
                var roadBuilding = new DevelopmentCard() { DevCard = DevCardType.RoadBuilding, Played = false };
                Assert.Contains(roadBuilding, resources.DevCards);
                resources = await helper.Proxy.PlayRoadBuilding(helper.GameName, players[0]);
                Assert.NotNull(resources);
                Assert.Equal(2, resources.Roads);

            }
        }
        [Fact]
        async Task Knight()
        {

            GameInfo gameInfo = new GameInfo()
            {
                YearOfPlenty = 0,
                Knight = 1,
                VictoryPoint = 0,
                Monopoly = 0,
                RoadBuilding = 0
            };

            using (var helper = new TestHelper(gameInfo))
            {
                var players = await helper.CreateGame();
                var tr = new TradeResources()
                {
                    Ore = 1,
                    Wheat = 1,
                    Sheep = 1
                };

                _ = await helper.GrantResourcesAndAssertResult(players[0], tr);
                PlayerResources resources = await helper.BuyDevCard(players[0], DevCardType.Knight);
                Assert.Equal(0, resources.Roads);
                Assert.NotNull(resources);
                Assert.NotEmpty(resources.DevCards);
                var roadBuilding = new DevelopmentCard() { DevCard = DevCardType.Knight, Played = false };
                Assert.Contains(roadBuilding, resources.DevCards);
                Assert.False(resources.DevCards[0].Played);
                resources = await helper.Proxy.PlayKnight(helper.GameName, players[0]);
                Assert.NotNull(resources);
                Assert.True(resources.DevCards[0].Played);

            }
        }

    }
}
