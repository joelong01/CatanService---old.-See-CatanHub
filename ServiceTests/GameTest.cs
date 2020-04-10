using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
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
                _  = await helper.CreateGame();
                var games = await helper.Proxy.GetGames();
                bool contains = games.Contains(helper.GameName.ToLower());
                Assert.True(contains);
                games = await helper.Proxy.GetGames();
                List<List<ServiceLogRecord>> logCollection = await helper.Proxy.GetAllLogs(helper.GameName, helper.Players[0], 0);
                Assert.NotNull(logCollection);
                Assert.NotEmpty(logCollection);
                Assert.NotEmpty(logCollection[0]);
                GameLog gameLog = logCollection[0][0] as GameLog;
                Assert.Equal(ServiceAction.PlayerAdded, gameLog.Action);
                Assert.Equal(ServiceLogType.Game, gameLog.LogType);
                Assert.NotEmpty(gameLog.Players);
                List<string> allPlayers = new List<string>(helper.Players);
                foreach (var player in gameLog.Players )
                {
                    Assert.Contains(player, allPlayers);
                    allPlayers.Remove(player);
                }

                Assert.Empty(allPlayers);
                
                
            }                      
        }

        [Fact]
        async Task CatanRegisterReturnsSuccess()
        {
            TestHelper helper = new TestHelper();
            using (helper)
            {
                string player = (await helper.CreateGame())[0];
                var gameInfo = new GameInfo();
                var resources = await helper.Proxy.Register(helper.GameInfo, helper.GameName, "Miller");
                Assert.Equal("Miller", resources.PlayerName);
                Assert.Equal(helper.GameName, resources.GameName);
                
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
