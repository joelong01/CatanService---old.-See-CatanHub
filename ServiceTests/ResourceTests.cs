using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Catan.Proxy;
using CatanService;
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



                    var resourcesAfterGrant = await helper.Proxy.GrantResources(helper.GameName, players[0], loopTempResourceAsk);
                    Assert.NotNull(resourcesAfterGrant);
                    //
                    //  make sure we got what we asked for
                    Assert.True(resourcesAfterGrant.Equivalent(loopTempResourceAsk + startingResource));

                    //
                    //  get the log record - it should have resources we just granted
                    List<ServiceLogRecord> logCollection = await helper.Proxy.Monitor(helper.GameName, players[0]);
                    var resourceLog = logCollection[^1] as ResourceLog;
                    Assert.True(resourceLog.PlayerResources.Equivalent(resourcesAfterGrant));
                    Assert.True(resourceLog.TradeResource.Equivalent(loopTempResourceAsk));


                    // make sure the Undo CatanRequest is set
                    Assert.NotNull(resourceLog.UndoRequest);
                    Assert.Equal(BodyType.TradeResources, resourceLog.UndoRequest.BodyType);
                    Assert.NotNull(resourceLog.UndoRequest.Body);

                    // use the Undo Request
                    var resourcesAfterUndo = await helper.Proxy.PostUndoRequest<PlayerResources>(resourceLog.UndoRequest);
                    Assert.NotNull(resourcesAfterUndo);
                    Assert.True(resourcesAfterUndo.Equivalent(startingResources));
                    resourceLog = null; // to make sure we don't use it below
                    //
                    //  get the log for the Undo Action
                    logCollection = await helper.Proxy.Monitor(helper.GameName, players[0]);
                    ResourceLog undoLog = logCollection[^1] as ResourceLog;
                    Assert.Equal(ServiceAction.ReturnResources, undoLog.Action);
                    Assert.True(undoLog.PlayerResources.Equivalent(resourcesAfterUndo));
                    Assert.True(undoLog.PlayerResources.Equivalent(startingResource));
                    Assert.Equal(players[0], undoLog.PlayerName);
                    Assert.NotNull(undoLog.UndoRequest);
                    Assert.NotNull(undoLog.UndoRequest.Body);
                    Assert.Equal(BodyType.TradeResources, undoLog.UndoRequest.BodyType);


                    //
                    //  double check by getting the resources from the serice
                    //  we should be back where we started
                    resourcesAfterUndo = await helper.Proxy.GetResources(helper.GameName, players[0]);
                    Assert.True(resourcesAfterUndo.Equivalent(startingResources));
                    
                    
                    //
                    // undo the request
                    var resourcesAfterRedo = await helper.Proxy.PostUndoRequest<PlayerResources>(undoLog.UndoRequest);
                    Assert.NotNull(resourcesAfterRedo);
                    //
                    // ...and in the end we are back to the resources we granted the user
                    Assert.True(resourcesAfterGrant.Equivalent(resourcesAfterRedo));

                    // Undo one more time to make the looping work
                    //
                    //  get the log record - it should have resources we just granted
                    logCollection = await helper.Proxy.Monitor(helper.GameName, players[0]);
                    var redoLog = logCollection[^1] as ResourceLog;
                    Assert.True(redoLog.PlayerResources.Equivalent(resourcesAfterGrant));
                    Assert.True(redoLog.TradeResource.Equivalent(loopTempResourceAsk));


                    // make sure the Undo CatanRequest is set
                    Assert.NotNull(redoLog.UndoRequest);
                    Assert.Equal(BodyType.TradeResources, redoLog.UndoRequest.BodyType);
                    Assert.NotNull(redoLog.UndoRequest.Body);

                    // use the Undo Request
                    resourcesAfterUndo = await helper.Proxy.PostUndoRequest<PlayerResources>(redoLog.UndoRequest);
                    Assert.NotNull(resourcesAfterUndo);
                    Assert.True(resourcesAfterUndo.Equivalent(startingResources));
                }
            }
        }

        [Fact]
        public async Task TradeGold()
        {

            using (var helper = new TestHelper())
            {
                var players = await helper.CreateGame();
                var tr = new TradeResources()
                {
                    GoldMine = 2
                };

                _ = await helper.GrantResourcesAndAssertResult(players[0], tr);
                TradeResources tradeResources = new TradeResources() { };
                //
                //  try to trade with bad body
                var resources = await helper.Proxy.TradeGold(helper.GameName, players[0], tradeResources);
            }
        }
    }
}
