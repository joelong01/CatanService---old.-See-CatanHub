using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Catan.Proxy;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Xunit;
using Xunit.Abstractions;

namespace ServiceTests
{

    public class GameTest 
    {
        
        private readonly ITestOutputHelper output;

        public GameTest(ITestOutputHelper output)
        {
            this.output = output;
        }
        


        [Fact]
        async Task VerifyGame()
        {
            TestHelper helper = new TestHelper(output);
            using (helper)
            {
                _ = await helper.CreateGame();
                var games = await helper.Proxy.GetGames();
                bool contains = games.Contains(helper.GameName.ToLower());
                Assert.True(contains);
                games = await helper.Proxy.GetGames();

                //
                //  get the game log and verify that we have all the same players
                List<ServiceLogRecord> logCollection = await helper.Proxy.Monitor(helper.GameName, helper.Players[0]);
                GameLog gameLog = logCollection[^1] as GameLog;
                Assert.Equal(ServiceAction.GameStarted, gameLog.Action);
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
            TestHelper helper = new TestHelper(output, null);
            using (helper)
            {
                var players = await helper.CreateGame(false);
                var gameInfo = new GameInfo();
                var resources = await helper.Proxy.JoinGame(helper.GameName, "Miller");
                if (resources is null ) helper.TraceMessage($"{helper.Proxy.LastError.Description}");
                Assert.Equal("Miller", resources.PlayerName);
                Assert.Equal(helper.GameName, resources.GameName);


                //   await helper.StartMonitoring(1);

                await helper.Proxy.StartGame(helper.GameName);
                List<ServiceLogRecord> logCollection = await helper.Proxy.Monitor(helper.GameName, players[0]);
                GameLog gameLog = logCollection[^1] as GameLog;
                Assert.Equal(ServiceAction.GameStarted, gameLog.Action);
                Assert.Equal(5, gameLog.Players.Count);



                //await helper.WaitForMonitorCompletion();
                //Assert.Single(helper.LogCollection);

                Debug.WriteLine($"Monitor fetched: {CatanProxy.Serialize(helper.LogCollection)}");


            }
        }

        [Fact]
        async Task CatanGetUsers()
        {
            TestHelper helper = new TestHelper(output);
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
            TestHelper helper = new TestHelper(output);
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
