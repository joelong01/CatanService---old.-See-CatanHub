﻿using Catan.Proxy;
using CatanService;
using CatanSharedModels;
using Microsoft.AspNetCore.Mvc.Testing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ServiceTests
{
    public class TestHelper : IDisposable
    {
        public CatanProxy Proxy { get; }
        public string GameName { get; }
        public GameInfo GameInfo { get; set; } = new GameInfo();
        public List<string> Players { get; set; }
        public List<List<ServiceLogRecord>> LogCollection { get; } = new List<List<ServiceLogRecord>>();
        public TestHelper()
        {
            GameName = Guid.NewGuid().ToString();
            Proxy = new CatanProxy();
            var factory = new CustomWebApplicationFactory<Startup>();
            Proxy.Client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }
        private int _monitorIterations = 1;
        private TaskCompletionSource<object> _monitorTCS = new TaskCompletionSource<object>();
        private TaskCompletionSource<object> _monitorStart = new TaskCompletionSource<object>();
        public async Task<List<string>> CreateGame(bool startGame = true)
        {
            var gameInfo = new GameInfo();
            var response = await Proxy.Register(gameInfo, GameName, "Doug");
            Assert.NotNull(response);
            response = await Proxy.Register(gameInfo, GameName, "Max");
            Assert.NotNull(response);
            response = await Proxy.Register(gameInfo, GameName, "Wallace");
            Assert.NotNull(response);
            response = await Proxy.Register(gameInfo, GameName, "Joe");
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
            Debug.WriteLine($"Monitor_Callback started. iterating {_monitorIterations} thread id = {Thread.CurrentThread.ManagedThreadId} ");
            
            List <ServiceLogRecord> logs;
            int count = 0;
            while (count < _monitorIterations)
            {
                if (count == 0) _monitorStart.SetResult(null);
                logs = await Proxy.Monitor(this.GameName, Players[0]);
                
                count++;
                if (logs != null)
                {
                    Debug.WriteLine($"Monitor returned with {logs.Count} records");
                    LogCollection.Add(logs);                    
                }
                else
                {
                    Debug.WriteLine($"Monitor returned with Zero records!!");
                }
                

            }
            if (count == _monitorIterations)
            {
                _monitorTCS.SetResult(null);
                Debug.WriteLine("Exiting worker thread");
            }
        }
        public async Task<T> GetLogRecordsFromEnd<T>(int offset = 1)
        {
            List<ServiceLogRecord> logCollection = await Proxy.GetAllLogs(GameName, Players[0], 0);
            if (logCollection == null)
            {
                Debug.WriteLine($"LogCollection is null! {this.Proxy.LastError} {this.Proxy.LastErrorString}");
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
                Debug.WriteLine($"Invalid Cast in GetLogRecordsFromEnd.  Wanted type {typeof(T).UnderlyingSystemType} got {logCollection[^offset].GetType().UnderlyingSystemType}");
            }

            return default;




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
            Proxy.DeleteGame(GameName);
        }
    }
}
