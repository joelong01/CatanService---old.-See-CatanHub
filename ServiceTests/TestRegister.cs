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
    
    public class TestRegister: IClassFixture<ServiceFixture>
    {
        private readonly ServiceFixture _fixture;
        
        public TestRegister(ServiceFixture fixture)
        {
            _fixture = fixture;
            var gameInfo = new GameInfo();
            _ = _fixture.Proxy.Register(gameInfo, _fixture.GameName, "Max").Result;
            _ = _fixture.Proxy.Register(gameInfo, _fixture.GameName, "Wallace").Result;
            _ = _fixture.Proxy.Register(gameInfo, _fixture.GameName, "Joe").Result;
        }

        [Fact]
        async Task VerifyGame()
        {
            var games = await _fixture.Proxy.GetGames();
            Assert.Equal(games[0], _fixture.GameName.ToLower());
            var count = games.Count;
            Assert.Equal(1, count);

        }

        [Fact]
        async Task CatanRegisterReturnsSuccess()
        {
            var gameInfo = new GameInfo();
            var resources = await _fixture.Proxy.Register(gameInfo, _fixture.GameName, "Miller");
            Assert.Equal("Miller", resources.PlayerName);
            Assert.Equal(_fixture.GameName, resources.GameName);
        }

        [Fact]
        async Task CatanGetUsers()
        {
            var users = await _fixture.Proxy.GetUsers(_fixture.GameName);
            //
            //  remember everything is in lower case
            Assert.Contains("max", users);
            Assert.Contains("wallace", users);
            Assert.Contains("joe", users);
            //
            //  can't check for Miller because of ordering issues
           
        }

        [Fact]
        async Task CatanGetGameInfo()
        {
            var info = await _fixture.Proxy.GetGameInfo(_fixture.GameName);
            //
            //  remember everything is in lower case
            Assert.Equal(info, new GameInfo());
            
            

        }

    }
}
