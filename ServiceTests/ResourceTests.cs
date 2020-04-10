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
    public class ResourceTests : IClassFixture<ServiceFixture>
    {
        private readonly ServiceFixture _fixture;
        public ResourceTests(ServiceFixture fixture)
        {
            _fixture = fixture;

        }
        [Fact]
        public async Task GrantResources()
        {
            TestHelper helper = new TestHelper();
            using (helper)
            {
                var players = await helper.CreateGame();
                foreach (ResourceType restype in Enum.GetValues(typeof(ResourceType)))
                {
                    var tr = new TradeResources() { };
                    switch (restype)
                    {
                        case ResourceType.Sheep:
                            tr.Sheep++;
                            break;
                        case ResourceType.Wood:
                            tr.Wood++;
                            break;
                        case ResourceType.Ore:
                            tr.Ore++;
                            break;
                        case ResourceType.Wheat:
                            tr.Wheat++;
                            break;
                        case ResourceType.Brick:
                            tr.Brick++;
                            break;
                        case ResourceType.GoldMine:
                            tr.GoldMine++;
                            break;
                        case ResourceType.Desert:
                        case ResourceType.Back:
                        case ResourceType.None:
                        case ResourceType.Sea:
                        default:
                            continue;
                    }

                    var resource = await helper.Proxy.GrantResources(helper.GameName, players[0], tr);
                    Assert.NotNull(resource);
                    Assert.Equal(1, resource.GetResourceCount(restype));
                    resource = await helper.Proxy.ReturnResource(helper.GameName, players[0], tr);
                }
            }
        }
    }
}
