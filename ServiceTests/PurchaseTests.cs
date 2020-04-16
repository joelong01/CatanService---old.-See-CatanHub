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
    public class PurchaseTests : IClassFixture<ServiceFixture>
    {
        [Fact]
        private async Task PurchaseRoads()
        {
            TestHelper helper = new TestHelper();
            using (helper)
            {
                var resources = await PurchaseAll(helper, Entitlement.Road, new TradeResources() { Brick = 1, Wood = 1 });

                Assert.Equal(helper.GameInfo.MaxRoads, resources.Roads);


            }


        }
        [Fact]
        private async Task PurchaseCities()
        {
            GameInfo gameInfo = new GameInfo()
            {
                MaxCities = 1
            };
            TestHelper helper = new TestHelper(gameInfo);
            using (helper)
            {
                var resources = await PurchaseAll(helper, Entitlement.City, new TradeResources() { Ore = 3, Wheat = 2 });

                Assert.Equal(helper.GameInfo.MaxCities, resources.Cities);
            }


        }
        [Fact]
        private async Task PurchaseSettlements()
        {
            TestHelper helper = new TestHelper();
            using (helper)
            {
                var resources = await PurchaseAll(helper, Entitlement.Settlement, new TradeResources() { Sheep = 1, Brick = 1, Wood = 1, Wheat = 1 });

                Assert.Equal(helper.GameInfo.MaxSettlements, resources.Settlements);
            }


        }
        /// <summary>
        ///     Buy all entitlemens of a given type.
        ///     Will Grant resources as needed. Does 3 things
        ///     1. Buys all the resources
        ///     2. Get the log and then "undoes" each of the actions
        ///     3. Gets the log and "redoes" each of the actions
        ///     4. verifies that the Undo/Redo path yeilds the same results as the intial buy path.
        /// </summary>
        /// <param name="helper">The Test Helper that holds the CatanProxy and the GameName</param>
        /// <param name="entitlement">The Entitlement to buy</param>
        /// <param name="tr">The Traderesource that represents the cost of the entitlement</param>
        /// <returns></returns>
        private async Task<PlayerResources> PurchaseAll(TestHelper helper, Entitlement entitlement, TradeResources tr)
        {

            string player = (await helper.CreateGame())[0];
            Assert.NotNull(player);
            Assert.NotEmpty(player);
            PlayerResources startingResources = await helper.Proxy.GetResources(helper.GameName, player);
            List<ServiceLogRecord> logCollection = await helper.Proxy.Monitor(helper.GameName, player); // this wipes out any logs up till now -- just a player log

            PlayerResources resources;
            do
            {
                resources = await helper.GrantResourcesAndAssertResult(player, tr);
                Assert.NotNull(resources);
                resources = await helper.Proxy.BuyEntitlement(helper.GameName, player, entitlement);

            } while (resources != null);

            Assert.Equal(CatanError.LimitExceeded, helper.Proxy.LastError.Error);
            PlayerResources resourcesAfterPurchaseLoop = await helper.Proxy.GetResources(helper.GameName, player);

            Assert.NotNull(resourcesAfterPurchaseLoop);


            //
            //  get the log records -- normally this is done in a monitor
            logCollection = await helper.Proxy.Monitor(helper.GameName, player); // this are only the ones we just added

            //
            //  now undo everything
            PlayerResources resourcesAfterUndo = null;
            for (int i = 0; i < logCollection.Count ; i++) 
            {
                ServiceLogRecord logEntry = logCollection[^(i+1)] as ServiceLogRecord;
                Assert.NotNull(logEntry);
                Assert.NotNull(logEntry.UndoRequest);
                Assert.NotNull(logEntry.UndoRequest.Url);
                resourcesAfterUndo = await helper.Proxy.PostUndoRequest<PlayerResources>(logEntry.UndoRequest);
                if (resourcesAfterUndo is  null)
                {
                    Debug.WriteLine($"Last Error: {helper.Proxy.LastErrorString}");
                }
                Assert.NotNull(resourcesAfterUndo);
            }
            //
            //  back to where we started
            Assert.True(resourcesAfterUndo.Equivalent(startingResources));


            //
            //  get the log records -- normally this is done in a monitor and will contain all the records that we just undid
            logCollection = await helper.Proxy.Monitor(helper.GameName, player); // this are only the ones we just added

            //
            //  now redo all the actions
            PlayerResources resourcesAfterRedo = null;

            for (int i = 0; i < logCollection.Count; i++)
            {
                ServiceLogRecord logEntry = logCollection[i] as ServiceLogRecord;
                Assert.NotNull(logEntry);
                Assert.NotNull(logEntry.UndoRequest);
                Assert.NotNull(logEntry.UndoRequest.Url);
                resourcesAfterRedo = await helper.Proxy.PostUndoRequest<PlayerResources>(logEntry.UndoRequest);
                Assert.NotNull(resourcesAfterUndo);
                Debug.WriteLine($"{i}: {logEntry.UndoRequest.Url} ");
            }

            //
            //  back to where we started
            Assert.True(resourcesAfterRedo.Equivalent(resourcesAfterPurchaseLoop));


            return resourcesAfterRedo;

        }

    }
}
