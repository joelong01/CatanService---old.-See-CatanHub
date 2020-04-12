using System;
using System.Collections.Generic;
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
                const int startingResourceCount = 1;
                var startingResource = new TradeResources()
                {
                    Wood = startingResourceCount,
                    Wheat = startingResourceCount,
                    Brick = startingResourceCount,
                    Ore = startingResourceCount,
                    GoldMine = startingResourceCount,
                    Sheep = startingResourceCount
                };
                var startingResources = await helper.Proxy.GrantResources(helper.GameName, players[0], startingResource);
                Assert.True(startingResources.Equivalent(startingResource));
                foreach (ResourceType restype in Enum.GetValues(typeof(ResourceType)))
                {
                    // 
                    // grant each resource one at a time, then undo it.                    
                    var loopTempResourceAsk = new TradeResources() { };
                    switch (restype)
                    {
                        case ResourceType.Sheep:
                            loopTempResourceAsk.Sheep++;
                            break;
                        case ResourceType.Wood:
                            loopTempResourceAsk.Wood++;
                            break;
                        case ResourceType.Ore:
                            loopTempResourceAsk.Ore++;
                            break;
                        case ResourceType.Wheat:
                            loopTempResourceAsk.Wheat++;
                            break;
                        case ResourceType.Brick:
                            loopTempResourceAsk.Brick++;
                            break;
                        case ResourceType.GoldMine:
                            loopTempResourceAsk.GoldMine++;
                            break;
                        case ResourceType.Desert:
                        case ResourceType.Back:
                        case ResourceType.None:
                        case ResourceType.Sea:
                        default:
                            continue;
                    }



                    var resource = await helper.Proxy.GrantResources(helper.GameName, players[0], loopTempResourceAsk);
                    Assert.NotNull(resource);
                    //
                    //  make sure we got what we asked for
                    Assert.True(resource.Equivalent(loopTempResourceAsk + startingResource));

                    //
                    //  get the log record - it should have resources we just granted
                    var lastLogRecord = await helper.GetLogRecordsFromEnd<ResourceLog>();
                    Assert.True(lastLogRecord.PlayerResources.Equivalent(resource));
                    Assert.True(lastLogRecord.TradeResource.Equivalent(loopTempResourceAsk));

                    // undo the grant -- we should be back where we started
                    var resourcesAfterUndo = await helper.Proxy.UndoGrantResource(helper.GameName, lastLogRecord);
                    Assert.True(resourcesAfterUndo.Equivalent(startingResources));

                    //
                    //  get the log for the Undo Action
                    lastLogRecord = await helper.GetLogRecordsFromEnd<ResourceLog>();
                    Assert.Equal(ServiceAction.ReturnResources, lastLogRecord.Action);
                    Assert.True(lastLogRecord.PlayerResources.Equivalent(resourcesAfterUndo));
                    Assert.True(lastLogRecord.PlayerResources.Equivalent(startingResource));
                    Assert.Equal(players[0], lastLogRecord.PlayerName);



                    //
                    //  double check by getting the resources from the serice
                    //  we should be back where we started
                    resourcesAfterUndo = await helper.Proxy.GetResources(helper.GameName, players[0]);
                    Assert.True(resourcesAfterUndo.Equivalent(startingResources));




                }
            }
        }
    }
}
