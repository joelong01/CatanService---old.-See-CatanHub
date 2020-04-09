using System;
using System.Collections.Generic;


namespace CatanSharedModels
{
    /// <summary>
    ///     This enum tells us what the data shape is 
    /// </summary>
    public enum ServiceLogType
    {
        Undefined, Resource, Game, Purchase,
        Trade, TakeCard, MeritimeTrade,
        UpdateTurn,
        Monopoly
    }
    /// <summary>
    ///     this enum tells us what the data was used for. We often have data shapes for only one reason...
    /// </summary>
    public enum ServiceAction
    {
        Undefined, Purchased, PlayerAdded, UserRemoved, GameCreated, GameDeleted,
        TradeGold, GrantResources, TradeResources, TakeCard, Refund, MeritimeTrade, UpdatedTurn,
        LostToMonopoly,
        PlayedMonopoly,
        PlayedRoadBuilding,
        PlayedKnight,
        PlayedYearOfPlenty
    }

    public class ServiceLogEntry
    {
        public Guid LogId { get; set; } = Guid.NewGuid();
        public ServiceLogType LogType { get; set; } = ServiceLogType.Undefined;
        public ServiceAction Action { get; set; } = ServiceAction.Undefined;
        public string PlayerName { get; set; }
        public string RequestUrl { get; set; }


    }
    public class ResourceLog : ServiceLogEntry
    {
        public PlayerResources PlayerResources { get; set; } // this is not needed for Undo, but is needed for each of the games to update their UI
        public TradeResources TradeResource { get; set; } // needed for Undo

        public ResourceLog() { LogType = ServiceLogType.Resource; }

    }

    public class MonopolyLog : ResourceLog
    {
        public ResourceType ResourceType { get; set; }
        public int Count { get; set; } = 0;
        public MonopolyLog() { }
    }

    public class TurnLog : ServiceLogEntry
    {
        public string NewPlayer { get; set; } = "";
        public TurnLog() { LogType = ServiceLogType.UpdateTurn; Action = ServiceAction.UpdatedTurn; }
    }

    public class TradeLog : ServiceLogEntry
    {
        public TradeLog() { LogType = ServiceLogType.Trade; }
        public TradeResources FromTrade { get; set; }
        public TradeResources ToTrade { get; set; }
        public PlayerResources FromResources { get; set; }
        public PlayerResources ToResources { get; set; }

        public string FromName { get; set; }
        public string ToName { get; set; }

    }
    public class TakeLog : ServiceLogEntry
    {
        public TakeLog() { LogType = ServiceLogType.TakeCard; }
        public ResourceType Taken { get; set; }
        public PlayerResources FromResources { get; set; }
        public PlayerResources ToResources { get; set; }

        public string FromName { get; set; }
        public string ToName { get; set; }

    }


    public class MeritimeTradeLog : ServiceLogEntry
    {
        public MeritimeTradeLog() { LogType = ServiceLogType.MeritimeTrade; Action = ServiceAction.MeritimeTrade; }
        public ResourceType Traded { get; set; }
        public int Cost { get; set; }
        public PlayerResources Resources { get; set; }

    }
    public class PurchaseLog : ServiceLogEntry
    {
        public Entitlement Entitlement { get; set; }
        public PlayerResources PlayerResources { get; set; }
        public PurchaseLog() { LogType = ServiceLogType.Purchase; }
    }
    public class GameLog : ServiceLogEntry
    {
        public IEnumerable<string> Players { get; set; }
        public GameLog() { LogType = ServiceLogType.Game; }
    }
}
