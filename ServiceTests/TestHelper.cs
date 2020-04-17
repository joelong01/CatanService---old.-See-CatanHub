using Catan.Proxy;
using CatanService;
using Microsoft.AspNetCore.Mvc.Testing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace ServiceTests
{
    public class TestHelper : IDisposable
    {
        public CatanProxy Proxy { get; }
        public string GameName { get; }
        public GameInfo GameInfo { get; set; } = new GameInfo();
        public List<string> Players { get; set; }
        public List<List<ServiceLogRecord>> LogCollection { get; } = new List<List<ServiceLogRecord>>();
        ITestOutputHelper Output { get; set; }
        public TestHelper() : this(null, new GameInfo())
        {
            
        }

        public TestHelper(GameInfo gameInfo) : this(null, gameInfo) { }

        
        public TestHelper(ITestOutputHelper output = null, GameInfo gameInfo = null) 
        {
            if (!(gameInfo is null)) GameInfo = gameInfo;
            
            GameName = Guid.NewGuid().ToString();
            Proxy = new CatanProxy();
            var factory = new CustomWebApplicationFactory<Startup>();
            Proxy.Client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        public void TraceMessage(string toWrite, [CallerMemberName] string cmb = "", [CallerLineNumber] int cln = 0, [CallerFilePath] string cfp = "")
        {
            if (Output != null)
            {
                Output.WriteLine($"{cfp}({cln}):{toWrite}\t\t[Caller={cmb}]");
            }


        }
        private void TraceObject(object o, [CallerMemberName] string cmb = "", [CallerLineNumber] int cln = 0, [CallerFilePath] string cfp = "")
        {
            if (o is null)
            {
                TraceMessage("Null object value", cmb, cln, cfp);
            }
            else
            {
                TraceMessage(JsonSerializer.Serialize<object>(o), cmb, cln, cfp);
            }
        }

        private int _monitorIterations = 1;
        private TaskCompletionSource<object> _monitorTCS = new TaskCompletionSource<object>();
        private TaskCompletionSource<object> _monitorStart = new TaskCompletionSource<object>();
        public async Task<List<string>> CreateGame(bool startGame = true)
        {
            List<string> games = await Proxy.CreateGame(GameName, GameInfo);

            var response = await Proxy.JoinGame(GameName, "Doug");
            Assert.NotNull(response);
            response = await Proxy.JoinGame(GameName, "Max");
            Assert.NotNull(response);
            response = await Proxy.JoinGame(GameName, "Wallace");
            Assert.NotNull(response);
            response = await Proxy.JoinGame(GameName, "Joe");
            Assert.NotNull(response);
            if (startGame) await Proxy.StartGame(GameName);

            Players = await Proxy.GetUsers(GameName);
            Assert.Equal(4, Players.Count);

            return Players;
        }

        public Task StartMonitoring(int interations)
        {
            _monitorIterations = interations;
            ThreadPool.QueueUserWorkItem(Monitor_Callback, interations);
            return _monitorStart.Task;
        }

        public Task WaitForMonitorCompletion()
        {
            return _monitorTCS.Task;
        }


        private async void Monitor_Callback(object state)
        {
            // Debug.WriteLine($"Monitor_Callback started. iterating {_monitorIterations} thread id = {Thread.CurrentThread.ManagedThreadId} ");

            List<ServiceLogRecord> logs;
            int count = 0;
            while (count < _monitorIterations)
            {
                if (count == 0) _monitorStart.SetResult(null);
                logs = await Proxy.Monitor(this.GameName, Players[0]);

                count++;
                if (logs != null)
                {
                    // Debug.WriteLine($"Monitor returned with {logs.Count} records");
                    LogCollection.Add(logs);
                }
                else
                {
                    // Debug.WriteLine($"Monitor returned with Zero records!!");
                }


            }
            if (count == _monitorIterations)
            {
                _monitorTCS.SetResult(null);
                // Debug.WriteLine("Exiting worker thread");
            }
        }
        public async Task<T> GetLogRecordsFromEnd<T>(int offset = 1)
        {
            List<ServiceLogRecord> logCollection = await Proxy.GetAllLogs(GameName, Players[0], offset);
            if (logCollection == null)
            {
                // Debug.WriteLine($"LogCollection is null! {this.Proxy.LastError} {this.Proxy.LastErrorString}");
            }
            Assert.NotNull(logCollection);
            Assert.NotEmpty(logCollection);

            try
            {
                T ret = (T)(object)logCollection[^offset];
                Assert.NotNull(ret);
                return ret;
            }
            catch (InvalidCastException)
            {
                // Debug.WriteLine($"Invalid Cast in GetLogRecordsFromEnd.  Wanted type {typeof(T).UnderlyingSystemType} got {logCollection[^offset].GetType().UnderlyingSystemType}");
            }

            return default;

        }

        /// <summary>
        ///     Note if you call this and there are no records, you will hang...
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="player"></param>
        /// <returns></returns>
        public async Task<T> MonitorGetLastRecord<T>(string player)
        {
            List<ServiceLogRecord> logCollection = await Proxy.Monitor(GameName, player);
            T ret = (T)(object)logCollection[^1];
            return ret;
        }

      
        public async Task<PurchaseLog> GetAndValidatePurchaseLog(string player)
        {
            var purchaseLog = await MonitorGetLastRecord<PurchaseLog>(player);


            return purchaseLog;
        }

        /// <summary>
        ///     Get the log for a purchase that just happened 
        ///     this needs to be called after a Purchase and before the next service call that logs, or you will hang
        ///     Validates
        ///         1. log is not null
        ///         2. PlayerResources in log are the same as passed in
        ///         
        /// </summary>
        /// <param name="player"></param>
        /// <param name="resources"></param>
        /// <returns></returns>
        public async Task<ResourceLog> GetAndValidateResourceLog(string player, PlayerResources resources)
        {
            var resourceLog = await MonitorGetLastRecord<ResourceLog>(player);
            Assert.NotNull(resourceLog);
            Assert.True(resourceLog.PlayerResources.Equivalent(resources));
            return resourceLog;
        }
        public async Task<PlayerResources> GrantResourcesAndAssertResult(string player, TradeResources tr)
        {
            PlayerResources oldResources = await Proxy.GetResources(GameName, player);
            var resources = await Proxy.GrantResources(GameName, player, tr);
            Assert.Equal(tr.Ore + oldResources.Ore, resources.Ore);
            Assert.Equal(tr.Wheat + oldResources.Wheat, resources.Wheat);
            Assert.Equal(tr.Sheep + oldResources.Sheep, resources.Sheep);
            Assert.Equal(tr.Wood + oldResources.Wood, resources.Wood);
            Assert.Equal(tr.Brick + oldResources.Brick, resources.Brick);
            Assert.Equal(tr.GoldMine + oldResources.GoldMine, resources.GoldMine);
            return resources;
        }

        public async Task<PlayerResources> BuyDevCard(string player, DevCardType devCard)
        {
            var tr = new TradeResources()
            {
                Ore = 1,
                Wheat = 1,
                Sheep = 1,
                Brick = 0,
                Wood = 0
            };
            DevelopmentCard card = new DevelopmentCard()
            {
                Played = false,
                DevCard = devCard,
            };
            PlayerResources resources;
            do
            {
                _ = await GrantResourcesAndAssertResult(player, tr);
                resources = await Proxy.DevCardPurchase(GameName, player);
                Assert.NotNull(resources);
            } while (resources.DevCards.Contains(card) == false);
            return resources;

        }

        public void Dispose()
        {

            var result = Proxy.DeleteGame(GameName).Result;
            Assert.NotNull(result);
            Assert.Equal(CatanError.NoError, result.Error);
            Proxy.Dispose();
        }
    }
}
