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

    public class CatanTest : IClassFixture<ServiceFixture>
    {
        private readonly ServiceFixture _fixture;

        public CatanTest(ServiceFixture fixture)
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
                //
                //  remember everything is in lower case
                Assert.Contains("max", users);
                Assert.Contains("wallace", users);
                Assert.Contains("joe", users);
                
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
