using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Catan.Proxy;
using CatanSharedModels;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Xunit;


namespace ServiceTests
{

    public class GameTest : IClassFixture<ServiceFixture>
    {
        private readonly ServiceFixture _fixture;

        public GameTest(ServiceFixture fixture)
        {
            _fixture = fixture;
        }



        [Fact]
        async Task VerifyGame()
        {
            TestHelper helper = new TestHelper();
            using (helper)
            {
                _ = await helper.CreateGame();
                var games = await helper.Proxy.GetGames();
                bool contains = games.Contains(helper.GameName.ToLower());
                Assert.True(contains);
                games = await helper.Proxy.GetGames();

                //
                //  get the game log and verify that we have all the same players

                GameLog gameLog = await helper.GetLogRecordsFromEnd<GameLog>();
                Assert.Equal(ServiceAction.PlayerAdded, gameLog.Action);
                Assert.Equal(ServiceLogType.Game, gameLog.LogType);
                Assert.NotEmpty(gameLog.Players);
                List<string> allPlayers = new List<string>(helper.Players);
                foreach (var player in gameLog.Players)
                {
                    Assert.Contains(player, allPlayers);
                    allPlayers.Remove(player);
                }
                //
                //  make sure that there aren't any left over players
                Assert.Empty(allPlayers);


            }
        }

        [Fact]
        async Task CatanRegisterReturnsSuccess()
        {
            TestHelper helper = new TestHelper();
            Debug.WriteLine($"main thread id = {Thread.CurrentThread.ManagedThreadId}");
            using (helper)
            {
                var players = await helper.CreateGame(false);
                var gameInfo = new GameInfo();
                var resources = await helper.Proxy.Register(helper.GameInfo, helper.GameName, "Miller"); 
                Assert.Equal("Miller", resources.PlayerName);
                Assert.Equal(helper.GameName, resources.GameName);


                await helper.StartMonitoring(1);
                
                await helper.Proxy.StartGame(helper.GameName);
                var log = await helper.GetLogRecordsFromEnd<GameLog>();
                Assert.Equal(ServiceAction.GameStarted, log.Action);
                Assert.Equal(5, log.Players.Count);

                

                await helper.WaitForMonitorCompletion();
                Assert.Single(helper.LogCollection);

                Debug.WriteLine($"Monitor fetched: {CatanProxy.Serialize(helper.LogCollection)}");


            }
        }

        [Fact]
        async Task CatanGetUsers()
        {
            TestHelper helper = new TestHelper();
            using (helper)
            {
                var users = await helper.CreateGame();
                foreach (var player in helper.Players)
                {
                    Assert.Contains(player.ToLower(), users);
                }
            }
        }

        [Fact]
        async Task CatanGetGameInfo()
        {
            TestHelper helper = new TestHelper();
            using (helper)
            {
                var users = await helper.CreateGame();
                var info = await helper.Proxy.GetGameInfo(helper.GameName);
                //
                //  remember everything is in lower case
                Assert.Equal(info, new GameInfo());
            }

        }



    }
}
