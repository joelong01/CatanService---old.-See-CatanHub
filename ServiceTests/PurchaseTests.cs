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
            TestHelper helper = new TestHelper();
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

        private async Task<PlayerResources> PurchaseAll(TestHelper helper, Entitlement entitlement, TradeResources tr)
        {

            string player = (await helper.CreateGame())[0];
            Assert.NotNull(player);
            Assert.NotEmpty(player);


            PlayerResources resources;
            do
            {
                resources = await helper.GrantResourcesAndAssertResult(player, tr);
                Assert.NotNull(resources);
                resources = await helper.Proxy.BuyEntitlement(helper.GameName, player, entitlement);

            } while (resources != null);

            Assert.Equal(CatanError.LimitExceeded, helper.Proxy.LastError.Error);
            resources = await helper.Proxy.GetResources(helper.GameName, player);
            Assert.NotNull(resources);


            var resourceLog = await helper.GetLogRecordsFromEnd<ResourceLog>();
            Assert.True(resourceLog.PlayerResources.Equivalent(tr));

            var purchaseLog = await helper.GetLogRecordsFromEnd<PurchaseLog>(2);
            var refundResources = await helper.Proxy.RefundEntitlement(helper.GameName, purchaseLog);
            Assert.NotNull(refundResources);
            

            return resources;

        }

    }
}
