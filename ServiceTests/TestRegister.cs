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
        }

        [Fact]
        async Task CatanRegisterReturnsSuccess()
        {
            string url = $"/api/catan/game/register/{_fixture.GameName}/Max";
            var response = await _fixture.HttpClient.PostAsync(url, new StringContent(CatanSerializer.Serialize<GameInfo>(new GameInfo()), Encoding.UTF8, "application/json"));
            string body = await response.Content.ReadAsStringAsync();
            
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

    }
}
