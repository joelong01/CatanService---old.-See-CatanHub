﻿using Catan.Proxy;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CatanService.State
{
    public class PlayerState : PlayerResources
    {

        //
        //  a lock to protect the resources
        private ReaderWriterLockSlim ResourceLock { get; } = new ReaderWriterLockSlim(); // protects access to this ResourceTracking
        private List<object> _log = new List<object>(); // this one gets wiped every time TSWaitForLog returns
        private List<object> _permLog = new List<object>(); // this one has *everything*
        private TaskCompletionSource<object> _tcs = new TaskCompletionSource<object>();
        private ReaderWriterLockSlim TCSLock { get; } = new ReaderWriterLockSlim(); // protects access to this ResourceTracking

        public GameInfo ResourcesLeft { get; set; } // a game info where we will keep track of how many resources we can allocate

        public bool TSFreeEntitlement(Entitlement entitlement)
        {
            ResourceLock.EnterWriteLock();
            try
            {
                switch (entitlement)
                {


                    case Entitlement.Settlement:
                        ResourcesLeft.MaxSettlements++;
                        break;
                    case Entitlement.City:
                        ResourcesLeft.MaxCities++;
                        break;
                    case Entitlement.Road:
                        ResourcesLeft.MaxRoads++;
                        break;
                    case Entitlement.Undefined:
                    case Entitlement.DevCard:
                    default:
                        break;
                }
                return true;
            }
            finally
            {
                ResourceLock.ExitWriteLock();
            }
        }
        public bool TSAllocateEntitlement(Entitlement entitlement)
        {
            ResourceLock.EnterWriteLock();
            try
            {
                switch (entitlement)
                {


                    case Entitlement.Settlement:
                        if (ResourcesLeft.MaxSettlements > 0)
                        {
                            ResourcesLeft.MaxSettlements--;
                            return true;
                        }
                        break;
                    case Entitlement.City:
                        if (ResourcesLeft.MaxCities > 0)
                        {
                            ResourcesLeft.MaxCities--;
                            return true;
                        }
                        break;
                    case Entitlement.Road:
                        if (ResourcesLeft.MaxRoads > 0)
                        {
                            ResourcesLeft.MaxRoads--;
                            return true;
                        }
                        break;
                    case Entitlement.Undefined:
                    case Entitlement.DevCard:
                    default:
                        break;
                }
                return false;
            }
            finally
            {
                ResourceLock.ExitWriteLock();
            }
        }
        public ServiceLogCollection GetLogCollection(int startAtSequenceNumber)
        {
            ResourceLock.EnterReadLock();
            try
            {

                var list = new List<object>();
                for (int i = startAtSequenceNumber; i < _permLog.Count - 1; i++)
                {
                    list.Add(_permLog[i]);
                }

                return new ServiceLogCollection()
                {
                    LogRecords = new List<object>(_permLog),
                    Count = _permLog.Count - startAtSequenceNumber,
                    CollectionId = Guid.NewGuid()
                };
            }
            finally
            {
                ResourceLock.ExitReadLock();
            }
        }
         
        public async Task<ServiceLogCollection> TSWaitForLog()
        {
         //   Console.WriteLine($"Waiting for log for {PlayerName}");
            ServiceLogCollection logCollection = null;
            try
            {
                var list = TSGetLogEntries();
                if (list.Count == 0)
                {
                    await _tcs.Task;
                    list = TSGetLogEntries();
                    _tcs = new TaskCompletionSource<object>();
                }

                logCollection = new ServiceLogCollection()
                {
                    LogRecords = list,
                    Count = list.Count,
                    CollectionId = Guid.NewGuid()

                };


                return logCollection;

            }
            finally
            {
               // Debug.WriteLine($"returning log for {PlayerName} returning {logCollection?.Count} records ");
            }

        }

        public void TSAddDevCard(DevCardType card)
        {
            ResourceLock.EnterWriteLock();
            try
            {
                DevCards.Add(new DevelopmentCard() { DevCard = card, Played = false });
            }
            finally
            {
                ResourceLock.ExitWriteLock();
            }
        }

        public void TSReleaseLogToClient()
        {
            //            Debug.WriteLine($"Releasing log for {PlayerName}");
            bool tookLock = false;
            if (!TCSLock.IsWriteLockHeld)
            {
                TCSLock.EnterWriteLock();
                tookLock = true;
            }
            try
            {
                _tcs.SetResult(null);
                _tcs = new TaskCompletionSource<object>();

            }
            finally
            {
                if (tookLock)
                {
                    TCSLock.ExitWriteLock();
                }
                //Debug.WriteLine($"Released log for {PlayerName}");
            }

        }
        public void TSAdd(PlayerState toAdd)
        {
            ResourceLock.EnterWriteLock();
            toAdd.ResourceLock.EnterReadLock();
            try
            {
                Wheat += toAdd.Wheat;
                Wood += toAdd.Wood;
                Brick += toAdd.Brick;
                Ore += toAdd.Ore;
                Sheep += toAdd.Sheep;
                GoldMine += toAdd.GoldMine;
            }
            finally
            {
                ResourceLock.ExitWriteLock();
                toAdd.ResourceLock.ExitReadLock();
            }
        }
        public void TSAdd(TradeResources toAdd)
        {
            ResourceLock.EnterWriteLock();
            try
            {
                Wheat += toAdd.Wheat;
                Wood += toAdd.Wood;
                Brick += toAdd.Brick;
                Ore += toAdd.Ore;
                Sheep += toAdd.Sheep;
                GoldMine += toAdd.GoldMine;
            }
            finally
            {
                ResourceLock.ExitWriteLock();
            }
        }
        public void TSAdd(PlayerResources toAdd)
        {
            ResourceLock.EnterWriteLock();
            try
            {
                Wheat += toAdd.Wheat;
                Wood += toAdd.Wood;
                Brick += toAdd.Brick;
                Ore += toAdd.Ore;
                Sheep += toAdd.Sheep;
                GoldMine += toAdd.GoldMine;
            }
            finally
            {
                ResourceLock.ExitWriteLock();
            }
        }
        public int TSAddResource(ResourceType resourceType, int count)
        {
            ResourceLock.EnterWriteLock();
            try
            {
                switch (resourceType)
                {
                    case ResourceType.Sheep:
                        this.Sheep += count;
                        return this.Sheep;
                    case ResourceType.Wood:
                        this.Wood += count;
                        return this.Wood;
                    case ResourceType.Ore:
                        this.Ore += count;
                        return this.Ore;
                    case ResourceType.Wheat:
                        this.Wheat += count;
                        return this.Wheat;
                    case ResourceType.Brick:
                        this.Brick += count;
                        return this.Brick;
                    case ResourceType.GoldMine:
                        this.GoldMine += count;
                        return this.GoldMine;
                    default:
                        throw new Exception($"Unexpected resource type passed into AddResource {resourceType}");
                }
            }
            finally
            {
                ResourceLock.ExitWriteLock();
            }
        }
        public int TSResourceCount(ResourceType resourceType)
        {
            ResourceLock.EnterReadLock();
            try
            {
                switch (resourceType)
                {
                    case ResourceType.Sheep:
                        return this.Sheep;
                    case ResourceType.Wood:
                        return this.Wood;
                    case ResourceType.Ore:
                        return this.Ore;
                    case ResourceType.Wheat:
                        return this.Wheat;
                    case ResourceType.Brick:
                        return this.Brick;
                    case ResourceType.GoldMine:
                        return this.GoldMine;
                    default:
                        throw new Exception($"Unexpected resource type passed into ResourceCount {resourceType}");
                }
            }
            finally
            {
                ResourceLock.ExitReadLock();
            }
        }
        public bool TSPlayDevCard(DevCardType devCardType)
        {


            ResourceLock.EnterWriteLock();
            try
            {
                foreach (var card in DevCards)
                {
                    if (card.DevCard == devCardType && card.Played == false)
                    {
                        card.Played = true;
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                ResourceLock.ExitWriteLock();
            }


        }

        /// <summary>
        ///     add a log entry in a thread safe way
        /// </summary>
        /// <param name="logEntry"></param>
        public void TSAddLogRecord(object logEntry)
        {

            if (!ResourceLock.IsWriteLockHeld)
            {
                ResourceLock.EnterWriteLock();

            }
            try
            {
                _log.Add(logEntry);
                _permLog.Add(logEntry);
               // Debug.WriteLine($"Added log for {PlayerName}. [LogId={((ServiceLogRecord)logEntry).LogId}] LogCount = {_log.Count}. LogType={((ServiceLogRecord)logEntry).LogType}");
            }
            finally
            {
                if (ResourceLock.IsWriteLockHeld)
                {
                    ResourceLock.ExitWriteLock();
                }
            }
        }
        /// <summary>
        ///     return a copy of the log - note that there is a 1:1 correspondence to kvp<game,player> and PlayerResources
        ///     and we keep a copy of the log in every ClientState object.  Each client will have one Monitor, which monitors *all*
        ///     the client changes (because each client runs the UI). easy to cheat by looking at the messages.  will assume good actors
        ///     in the system.
        /// </summary>
        private List<object> TSGetLogEntries()
        {
            ResourceLock.EnterWriteLock();
            try
            {
                var ret = new List<object>(_log);
                _log.Clear();
                return ret;
            }
            finally
            {
                ResourceLock.ExitWriteLock();
            }
        }
        /// <summary>
        ///     A thread safe way to get a copy of the PlayerResources so that they can be held and serialized in a thread safe way
        /// </summary>
        /// <returns></returns>
        public PlayerResources TSGetPlayerResourcesCopy()
        {
            ResourceLock.EnterReadLock();
            try
            {
                var pr = new PlayerResources()
                {
                    Wheat = this.Wheat,
                    Wood = this.Wood,
                    Brick = this.Brick,
                    Ore = this.Ore,
                    Sheep = this.Sheep,
                    GoldMine = this.GoldMine,
                    PlayerName = this.PlayerName,
                    GameName = this.GameName,
                };

                return pr;
            }
            finally
            {
                ResourceLock.ExitReadLock();
            }
        }
        public void TSAddEntitlement(Entitlement entitlement)
        {
            ResourceLock.EnterWriteLock();
            try
            {
                switch (entitlement)
                {

                    case Entitlement.DevCard:
                        break;
                    case Entitlement.Settlement:
                        this.Settlements++;
                        break;
                    case Entitlement.City:
                        this.Cities++;
                        break;
                    case Entitlement.Road:
                        this.Roads++;
                        break;
                    case Entitlement.Undefined:
                    default:
                        throw new Exception("Undefined Entitlment in TSAddEntitlement");
                }
            }
            finally
            {
                ResourceLock.ExitWriteLock();
            }
        }

        public bool TSRemoveEntitlement(Entitlement entitlement)
        {
            ResourceLock.EnterWriteLock();
            try
            {

                switch (entitlement)
                {

                    case Entitlement.DevCard:
                        break;
                    case Entitlement.Settlement:
                        if (this.Settlements == 0) return false;
                        this.Settlements--;
                        break;
                    case Entitlement.City:
                        if (this.Cities == 0) return false;
                        this.Cities--;
                        break;
                    case Entitlement.Road:
                        if (this.Roads == 0) return false;
                        this.Roads--;
                        break;
                    case Entitlement.Undefined:
                    default:
                        throw new Exception("Undefined Entitlment in TSAddEntitlement");
                }
                return true;
            }
            finally
            {
                ResourceLock.ExitWriteLock();
            }
        }

        public void TSAdd(int count, ResourceType resourceType)
        {
            ResourceLock.EnterWriteLock();
            try
            {
                switch (resourceType)
                {
                    case ResourceType.Sheep:
                        this.Sheep += count;
                        break;
                    case ResourceType.Wood:
                        this.Wood += count;
                        break;
                    case ResourceType.Ore:
                        this.Ore += count;
                        break;
                    case ResourceType.Wheat:
                        this.Wheat += count;
                        break;
                    case ResourceType.Brick:
                        this.Brick += count;
                        break;
                    default:
                        throw new Exception($"Unexpected resource type: {resourceType}");
                }
            }
            finally
            {
                ResourceLock.ExitWriteLock();
            }
        }

    }

}
