using CatanService.State;
using CatanSharedModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;


// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace CatanService.Controllers
{
    [Route("api/catan/resource")]
    public class ResourceController : Controller
    {

        private readonly ILogger<GameController> _logger;

        public ResourceController(ILogger<GameController> logger)
        {
            _logger = logger;
        }
        [HttpGet("{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetCards(string gameName, string playerName)
        {
            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                return NotFound(new CatanResult(CatanError.NoGameWithThatName) { Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
            }
            var resources = game.GetPlayer(playerName);
            if (resources == null)
            {
                return NotFound(new CatanResult(CatanError.NoPlayerWithThatName) { Request = this.Request.Path, Description = $"{playerName} in game '{gameName}' not found" });

            }
            return Ok(resources);
        }



        [HttpPost("grant/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GrantResources([FromBody] TradeResources toAdd, string gameName, string playerName)
        {
            (Game game, PlayerResources resources, IActionResult iaResult) = InternalPurchase(toAdd, gameName, playerName, true);
            if (iaResult != null)
            {
                return iaResult;
            }
            game.TSAddLogRecord(new ResourceLog()
            {
                PlayerResources = resources,
                Action = ServiceAction.GrantResources,
                PlayerName = playerName,
                TradeResource = toAdd,
                RequestUrl = this.Request.Path,
                
                UndoRequest = new CatanRequest()
                {
                    Url = $"api/catan/resource/undo/{gameName}/{playerName}",
                    Body = toAdd,
                    BodyType = BodyType.TradeResources
                }
            }) ;
            game.TSReleaseMonitors();
            return Ok(resources);



        }
        [HttpPost("undo/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult UndoGrantResources([FromBody] TradeResources tradeResource, string gameName, string playerName)
        {
            (Game game, PlayerResources resources, IActionResult iaResult) = InternalPurchase(tradeResource, gameName, playerName, false);
            if (iaResult != null)
            {
                return iaResult;
            }


            game.TSAddLogRecord(new ResourceLog()
            {
                PlayerResources = resources,
                Action = ServiceAction.ReturnResources,
                PlayerName = playerName,
                TradeResource = tradeResource,
                RequestUrl = this.Request.Path,
                UndoRequest = new CatanRequest()  // Undo the Undo is really Replay
                {
                    Url = $"api/catan/resource/grant/{gameName}/{playerName}",
                    Body = tradeResource,
                    BodyType = BodyType.TradeResources
                }
            });

            game.TSReleaseMonitors();
            return Ok(resources);



        }
        private (Game, PlayerResources, IActionResult) InternalPurchase(TradeResources toAdd, string gameName, string playerName, bool add)
        {
            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                return (game, null, NotFound(new CatanResult(CatanError.NoGameWithThatName) { Description = $"Game '{gameName}' does not exist", Request = this.Request.Path }));
            }
            var resources = game.GetPlayer(playerName);
            if (resources == null)
            {
                return (game, null, NotFound(new CatanResult(CatanError.NoPlayerWithThatName) { Request = this.Request.Path, Description = $"{playerName} in game '{gameName}' not found" }));

            }
            if (toAdd.Wheat < 0 ||
                  toAdd.Sheep < 0 ||
                  toAdd.Ore < 0 ||
                  toAdd.Brick < 0 ||
                  toAdd.Wood < 0 ||
                  toAdd.GoldMine < 0)
            {
                return (game, null, BadRequest(new CatanResult(CatanError.BadTradeResources)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path, Body = toAdd, BodyType = BodyType.TradeResources },
                    Description = $"{playerName} in game '{gameName}' is trying to {(add ? "grant" : "refund")} a negative resource"
                }));
            }

            if (add)
            {
                resources.TSAdd(toAdd);
            }
            else
            {
                resources.TSAdd(toAdd.GetNegated());
            }


            return (game, resources, null);
        }

        [HttpPost("return/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult ReturnResources([FromBody] TradeResources toAdd, string gameName, string playerName)
        {
            (Game game, PlayerResources resources, IActionResult iaResult) = InternalPurchase(toAdd, gameName, playerName, false);
            if (iaResult != null)
            {
                return iaResult;
            }


            game.TSAddLogRecord(new ResourceLog()
            {
                PlayerResources = resources,
                Action = ServiceAction.ReturnResources,
                PlayerName = playerName,
                TradeResource = toAdd,
                RequestUrl = this.Request.Path
            });

            game.TSReleaseMonitors();
            return Ok(resources);



        }
        /// <summary>
        ///     A user trades some of their gold for resources.  this could be a trade with a proper URL set up
        ///     Chose to make this an API as the business logic is cleaner and I don't have to get 2 references
        ///     to the same ClientState...which should work, but isn't needed.
        /// </summary>
        /// <param name="trade"></param>
        /// <param name="gameName"></param>
        /// <param name="playerName"></param>
        /// <returns></returns>
        [HttpPost("tradegold/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult TradeGold([FromBody] TradeResources trade, string gameName, string playerName)
        {
            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                return NotFound(new CatanResult(CatanError.NoGameWithThatName) { Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
            }
            var resources = game.GetPlayer(playerName);
            if (resources == null)
            {
                return NotFound(new CatanResult(CatanError.NoPlayerWithThatName) { Request = this.Request.Path, Description = $"{playerName} in game '{gameName}' not found" });

            }

            if (trade.GoldMine < 0) // not worrying about a read lock here...
            {
                return BadRequest(new CatanResult(CatanError.BadTradeResources)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path, Body = trade, BodyType = BodyType.TradeResources },
                    Description = $"[Player={playerName}] [Game='{gameName}'] in tradegold, the #gold should be positiver instead of {trade.GoldMine}"
                });
            }
            int askCount = trade.Brick + trade.Wood + trade.Wheat + trade.Ore + trade.Sheep;
            if (askCount > resources.GoldMine)
            {
                return BadRequest(new CatanResult(CatanError.InsufficientResource)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path, Body = trade, BodyType = BodyType.TradeResources },
                    Description = $"[Player={playerName}] [Game='{gameName}'] Asking for {askCount} resources, only have {trade.GoldMine} Gold"
                });
            }

            trade.GoldMine = askCount;

            //
            // now lock it so that you change it in a thread safe way
            trade.GoldMine = -trade.GoldMine;
            resources.TSAdd(trade);
            game.TSAddLogRecord(new ResourceLog() { PlayerResources = resources, Action = ServiceAction.TradeGold, PlayerName = playerName, TradeResource = trade, RequestUrl = this.Request.Path });
            game.TSReleaseMonitors();
            return Ok(resources);
        }
        /// <summary>
        ///     Executes a Catan trade and logs the results
        /// </summary>
        /// <param name="trade"></param>
        /// <param name="gameName"></param>
        /// <param name="fromName"></param>
        /// <param name="toName"></param>
        /// <returns></returns>
        [HttpPost("trade/{gameName}/{fromName}/{toName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult Trade([FromBody] TradeResources[] trade, string gameName, string fromName, string toName)
        {
            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                return NotFound(new CatanResult(CatanError.NoGameWithThatName) { Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
            }
            var fromResources = game.GetPlayer(fromName);
            if (fromResources == null)
            {
                return NotFound(new CatanResult(CatanError.NoPlayerWithThatName) { Request = this.Request.Path, Description = $"{fromName} in game '{gameName}' not found" });

            }
            var toResources = game.GetPlayer(toName);
            if (toResources == null)
            {
                return NotFound(new CatanResult(CatanError.NoPlayerWithThatName) { Request = this.Request.Path, Description = $"{toName} in game '{gameName}' not found" });

            }

            var fromTrade = trade[0];
            var toTrade = trade[1];


            //
            //  validate that from has the cards needed

            if (fromResources.Brick < fromTrade.Brick ||
                fromResources.Wood < fromTrade.Wood ||
                fromResources.Sheep < fromTrade.Sheep ||
                fromResources.Ore < fromTrade.Ore ||
                fromResources.Wheat < fromTrade.Wheat)
            {
                return BadRequest(new CatanResult(CatanError.InsufficientResource)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path, Body = trade, BodyType = BodyType.TradeResourcesList },
                    Description = $"[Player={fromName}] [Game={gameName}]\n has insufficient resources for the trade."
                });
            }
            // validate that To has the cards needed
            if (toResources.Brick < toTrade.Brick ||
                toResources.Wood < toTrade.Wood ||
                toResources.Sheep < toTrade.Sheep ||
                toResources.Ore < toTrade.Ore ||
                toResources.Wheat < toTrade.Wheat)
            {
                return BadRequest(new CatanResult(CatanError.InsufficientResource)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path, Body = trade, BodyType = BodyType.TradeResourcesList },
                    Description = $"[Player={toName}] [Game={gameName}]\n has insufficient resources for the trade"
                });
            }



            // take from
            fromResources.TSAdd(fromTrade.GetNegated());
            toResources.TSAdd(fromTrade);
            fromResources.TSAdd(toTrade);
            toResources.TSAdd(toTrade.GetNegated());

            game.TSAddLogRecord(new TradeLog() { PlayerName = fromName, FromName = fromName, ToName = toName, FromTrade = fromTrade, ToTrade = toTrade, FromResources = fromResources, ToResources = toResources, RequestUrl = this.Request.Path });
            game.TSReleaseMonitors();
            return Ok(new PlayerResources[2] { fromResources, toResources });
        }

        /// <summary>
        ///     Takes one random card from 'fromName' and gives it to 'toName'
        /// </summary>
        /// <param name="gameName"></param>
        /// <param name="fromName"></param>
        /// <param name="toName"></param>
        /// <returns></returns>
        [HttpPost("take/{gameName}/{fromName}/{toName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult Take(string gameName, string fromName, string toName)
        {
            ResourceType takenResource = ResourceType.None;
            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                return NotFound(new CatanResult(CatanError.NoGameWithThatName) { Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
            }
            var fromResources = game.GetPlayer(fromName);
            if (fromResources == null)
            {
                return NotFound(new CatanResult(CatanError.NoPlayerWithThatName) { Request = this.Request.Path, Description = $"{fromName} in game '{gameName}' not found" });

            }
            var toResources = game.GetPlayer(toName);
            if (toResources == null)
            {
                return NotFound(new CatanResult(CatanError.NoPlayerWithThatName) { Request = this.Request.Path, Description = $"{toName} in game '{gameName}' not found" });

            }
            //
            //  if the from player has no resources, then we are good
            if (fromResources.TotalResources == 0)
            {
                return Ok(new PlayerResources[2] { fromResources, toResources });
            }
            try
            {
                Random rand = new Random((int)DateTime.Now.Ticks);
                int index = rand.Next(0, fromResources.TotalResources);

                var tradeResource = new TradeResources();

                if (index < fromResources.Wheat)
                {
                    tradeResource.Wheat = -1;
                    fromResources.TSAdd(tradeResource);
                    tradeResource.Wheat = 1;
                    toResources.TSAdd(tradeResource);
                    takenResource = ResourceType.Wheat;
                    return Ok((new PlayerResources[2] { fromResources, toResources }));
                }
                else
                {
                    index -= fromResources.Wheat;
                }

                if (index < fromResources.Wood)
                {
                    tradeResource.Wood = -1;
                    fromResources.TSAdd(tradeResource);
                    tradeResource.Wood = 1;
                    toResources.TSAdd(tradeResource);
                    takenResource = ResourceType.Wood;
                    return Ok(new PlayerResources[2] { fromResources, toResources });
                }
                else
                {
                    index -= fromResources.Wood;
                }

                if (index < fromResources.Brick)
                {
                    tradeResource.Brick = -1;
                    fromResources.TSAdd(tradeResource);
                    tradeResource.Brick = 1;
                    toResources.TSAdd(tradeResource);
                    takenResource = ResourceType.Brick;
                    return Ok((new PlayerResources[2] { fromResources, toResources }));
                }
                else
                {
                    index -= fromResources.Brick;
                }

                if (index < fromResources.Sheep)
                {
                    tradeResource.Sheep = -1;
                    fromResources.TSAdd(tradeResource);
                    tradeResource.Sheep = 1;
                    toResources.TSAdd(tradeResource);
                    takenResource = ResourceType.Sheep;
                    return Ok((new PlayerResources[2] { fromResources, toResources }));
                }
                else
                {
                    index -= fromResources.Sheep;
                }


                tradeResource.Ore = -1;
                fromResources.TSAdd(tradeResource);
                tradeResource.Ore = 1;
                toResources.TSAdd(tradeResource);
                takenResource = ResourceType.Ore;

                return Ok(new PlayerResources[2] { fromResources, toResources });
            }
            finally
            {
                // log it
                game.TSAddLogRecord(new TakeLog() { PlayerName = fromName, FromName = fromName, ToName = toName, Taken = takenResource, FromResources = fromResources, ToResources = toResources, Action = ServiceAction.TakeCard, RequestUrl = this.Request.Path });
                game.TSReleaseMonitors();
            }

        }

        [HttpPost("meritimetrade/{gameName}/{playerName}/{resourceToGive}/{count}/{resourceToGet}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult MeritimeTrade(string gameName, string playerName, ResourceType resourceType, int cost)
        {
            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                return NotFound(new CatanResult(CatanError.NoGameWithThatName) { CantanRequest = new CatanRequest() { Url = this.Request.Path, Body = null, BodyType = BodyType.None }, Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
            }
            var playerResources = game.GetPlayer(playerName);
            if (playerResources == null)
            {
                return NotFound(new CatanResult(CatanError.NoPlayerWithThatName) { Request = this.Request.Path, Description = $"{playerName} in game '{gameName}' not found" });

            }


            // make sure they have enough to trade

            if (playerResources.TSResourceCount(resourceType) < cost)
            {
                return BadRequest(new CatanResult(CatanError.NotEnoughResourcesToPurchase) { Request = this.Request.Path, Description = $"[Player={playerName}] [Game={gameName}]\n needs {cost} of {resourceType} only has {playerResources.TSResourceCount(resourceType)} " });
            }

            playerResources.TSAddResource(resourceType, -cost);
            playerResources.TSAddResource(resourceType, 1);
            game.TSAddLogRecord(new MeritimeTradeLog() { Cost = cost, PlayerName = playerName, Traded = resourceType, RequestUrl = this.Request.Path, Action = ServiceAction.MeritimeTrade });
            game.TSReleaseMonitors();
            return Ok(playerResources);
        }



    }
}
