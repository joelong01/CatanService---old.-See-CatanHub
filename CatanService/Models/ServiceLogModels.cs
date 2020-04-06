using System.Collections.Generic;


namespace CatanSharedModels
{
    /// <summary>
    ///     This enum tells us what the data shape is 
    /// </summary>
    public enum ServiceLogType
    {
        Undefined, Resource, Game, Purchase,
        Trade, TakeCard, MeritimeTrade
    }
    /// <summary>
    ///     this enum tells us what the data was used for. We often have data shapes for only one reason...
    /// </summary>
    public enum ServiceAction
    {
        Undefined, Purchased, PlayerAdded, UserRemoved, GameCreated, GameDeleted,
        TradeGold, GrantResources, TradeResources, TakeCard, Refund, MeritimeTrade
    }

    public class ServiceLogEntry
    {
        public ServiceLogType LogType { get; set; } = ServiceLogType.Undefined;
        public ServiceAction Action { get; set; } = ServiceAction.Undefined;
        public string PlayerName { get; set; }
        public string Data { get; set; } = "";
    }

    public class ResourceLog : ServiceLogEntry
    {
        public PlayerResources PlayerResources { get; set; }
        public ResourceLog() { LogType = ServiceLogType.Resource; }
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
        public List<string> Players { get; set; }
        public GameLog() { LogType = ServiceLogType.Game; }
    }
}
