using CatanSharedModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace CatanService.Controllers
{
    [Route("api/catan/resource")]
    public class ResourceCardController : Controller
    {

        private readonly ILogger<GameController> _logger;

        public ResourceCardController(ILogger<GameController> logger)
        {
            _logger = logger;
        }
        [HttpGet("{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetCards(string gameName, string playerName)
        {
            bool ret = TSGlobal.PlayerState.TSGetPlayerResources(gameName, playerName, out ClientState resources);
            if (!ret)
            {
                return NotFound($"{playerName} in game { gameName} not found");

            }
            return Ok(resources);
        }



        [HttpPost("grant/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GrantResources([FromBody] TradeResources toAdd, string gameName, string playerName)
        {
            bool ret = TSGlobal.PlayerState.TSGetPlayerResources(gameName, playerName, out ClientState resources);
            if (!ret)
            {
                return NotFound($"{playerName} in game { gameName} not found");

            }
            if (toAdd.Wheat < 0 ||
                  toAdd.Sheep < 0 ||
                  toAdd.Ore < 0 ||
                  toAdd.Brick < 0 ||
                  toAdd.Wood < 0)
            {
                return BadRequest($"{playerName} in game { gameName} is trying to grant a negative resource");
            }


            resources.TSAdd(toAdd);


            TSGlobal.PlayerState.TSAddLogEntry(new ResourceLog() { PlayerResources = resources, Action = ServiceAction.GrantResources, PlayerName = playerName, TradeResource=toAdd });
            TSGlobal.PlayerState.TSReleaseMonitors(gameName);
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
            bool ret = TSGlobal.PlayerState.TSGetPlayerResources(gameName, playerName, out ClientState resources);
            if (!ret)
            {
                return NotFound($"{playerName} in game { gameName} not found");

            }

            if (trade.GoldMine < 0) // not worrying about a read lock here...
            {
                return BadRequest($"[Player={playerName}] [Game={gameName}]\n in tradegold, the #gold should be positiver instead of {trade.GoldMine}");
            }
            int askCount = trade.Brick + trade.Wood + trade.Wheat + trade.Ore + trade.Sheep;
            if (askCount > resources.GoldMine)
            {
                return BadRequest($"[Player={playerName}] [Game={gameName}]\n Asking for {askCount} resources, only have {trade.GoldMine} Gold");
            }

            trade.GoldMine = askCount;

            //
            // now lock it so that you change it in a thread safe way
            trade.GoldMine = -trade.GoldMine;
            resources.TSAdd(trade);
            TSGlobal.PlayerState.TSAddLogEntry(new ResourceLog() { PlayerResources = resources, Action = ServiceAction.TradeGold, PlayerName = playerName, TradeResource=trade });
            TSGlobal.PlayerState.TSReleaseMonitors(gameName);
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
            bool ret = TSGlobal.PlayerState.TSGetPlayerResources(gameName, fromName, out ClientState fromResources);
            if (!ret)
            {
                return NotFound($"{fromName} in game { gameName} not found");

            }
            ret = TSGlobal.PlayerState.TSGetPlayerResources(gameName, toName, out ClientState toResources);
            if (toResources == null)
            {
                return NotFound($"{toName} in game { gameName} not found");
            }

            var fromTrade = trade[0];
            var toTrade = trade[1];


            //
            //  validate that from has the cards needed
            if (fromResources.Brick < fromTrade.Brick)
            {
                return BadRequest($"[Player={fromName}] [Game={gameName}]\n Asking for {fromTrade.Brick} Brick, only has {fromResources.Brick}");
            }
            if (fromResources.Wood < fromTrade.Wood)
            {
                return BadRequest($"[Player={fromName}] [Game={gameName}]\n Asking for {fromTrade.Wood} Wood, only has {fromResources.Wood}");
            }
            if (fromResources.Sheep < fromTrade.Sheep)
            {
                return BadRequest($"[Player={fromName}] [Game={gameName}]\n Asking for {fromTrade.Sheep} Sheep, only has {fromResources.Sheep}");
            }
            if (fromResources.Ore < fromTrade.Ore)
            {
                return BadRequest($"[Player={fromName}] [Game={gameName}]\n Asking for {fromTrade.Ore} Ore, only has {fromResources.Ore}");
            }
            if (fromResources.Wheat < fromTrade.Wheat)
            {
                return BadRequest($"[Player={fromName}] [Game={gameName}]\n Asking for {fromTrade.Wheat} Wheat, only has {fromResources.Wheat}");
            }

            // validate that To has the cards needed
            if (toResources.Brick < toTrade.Brick)
            {
                return BadRequest($"[Player={toName}] [Game={gameName}]\n Asking for {toTrade.Brick} Brick, only has {toResources.Brick}");
            }
            if (toResources.Wood < toTrade.Wood)
            {
                return BadRequest($"[Player={toName}] [Game={gameName}]\n Asking for {toTrade.Wood} Wood, only has {toResources.Wood}");
            }
            if (toResources.Sheep < toTrade.Sheep)
            {
                return BadRequest($"[Player={toName}] [Game={gameName}]\n Asking for {toTrade.Sheep} Sheep, only has {toResources.Sheep}");
            }
            if (toResources.Ore < toTrade.Ore)
            {
                return BadRequest($"[Player={toName}] [Game={gameName}]\n Asking for {toTrade.Ore} Ore, only has {toResources.Ore}");
            }
            if (toResources.Wheat < toTrade.Wheat)
            {
                return BadRequest($"[Player={toName}] [Game={gameName}]\n Asking for {toTrade.Wheat} Wheat, only has {toResources.Wheat}");
            }

            // take from
            fromResources.TSAdd(fromTrade.GetNegated());
            toResources.TSAdd(fromTrade);
            fromResources.TSAdd(toTrade);
            toResources.TSAdd(toTrade.GetNegated());

            TSGlobal.PlayerState.TSAddLogEntry(new TradeLog() { PlayerName = fromName, FromName = fromName, ToName = toName, FromTrade = fromTrade, ToTrade = toTrade, FromResources = fromResources, ToResources = toResources });
            TSGlobal.PlayerState.TSReleaseMonitors(gameName);
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
            bool ret = TSGlobal.PlayerState.TSGetPlayerResources(gameName, fromName, out ClientState fromResources);
            ResourceType takenResource = ResourceType.None; ;
            if (!ret)
            {
                return NotFound($"{fromName} in game { gameName} not found");

            }

            ret = TSGlobal.PlayerState.TSGetPlayerResources(gameName, toName, out ClientState toResources);
            if (!ret)
            {
                return NotFound($"{fromName} in game { gameName} not found");

            }
            //
            //  if the from player has no resources, then we are good
            if (fromResources.TotalResources == 0)
            {
                return Ok(TSGlobal.Serialize<PlayerResources[]>(new PlayerResources[2] { fromResources, toResources }));
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
                    return Ok(TSGlobal.Serialize<PlayerResources[]>(new PlayerResources[2] { fromResources, toResources }));
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
                    return Ok(TSGlobal.Serialize<PlayerResources[]>(new PlayerResources[2] { fromResources, toResources }));
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
                    return Ok(TSGlobal.Serialize<PlayerResources[]>(new PlayerResources[2] { fromResources, toResources }));
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
                    return Ok(TSGlobal.Serialize<PlayerResources[]>(new PlayerResources[2] { fromResources, toResources }));
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
                TSGlobal.PlayerState.TSAddLogEntry(new TakeLog() { PlayerName = fromName, FromName = fromName, ToName = toName, Taken = takenResource, FromResources = fromResources, ToResources = toResources, Action = ServiceAction.TakeCard });
                TSGlobal.PlayerState.TSReleaseMonitors(gameName);
            }

        }

        [HttpPost("meritimetrade/{gameName}/{playerName}/{resourceToGive}/{count}/{resourceToGet}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult MeritimeTrade(string gameName, string playerName, ResourceType resourceType, int cost)
        {
            bool ret = TSGlobal.PlayerState.TSGetPlayerResources(gameName, playerName, out ClientState playerResources);

            if (!ret)
            {
                return NotFound($"{playerName} in game { gameName} not found");

            }

            // make sure they have enough to trade

            if (playerResources.TSResourceCount(resourceType) < cost)
            {
                return BadRequest($"[Player={playerName}] [Game={gameName}]\n needs {cost} of {resourceType} only has {playerResources.TSResourceCount(resourceType)} ");
            }

            playerResources.TSAddResource(resourceType, -cost);
            playerResources.TSAddResource(resourceType, 1);
            TSGlobal.PlayerState.TSAddLogEntry(new MeritimeTradeLog() { Cost = cost, PlayerName = playerName, Traded = resourceType });
            TSGlobal.PlayerState.TSReleaseMonitors(gameName);

            return Ok(playerResources);
        }


        [HttpPost("devcard/play/yearofplenty/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult PlayYearOfPlenty([FromBody] TradeResources tr, string gameName, string playerName)
        {
            bool ret = TSGlobal.PlayerState.TSGetPlayerResources(gameName, playerName, out ClientState playerResources);

            if (!ret)
            {
                return NotFound($"{playerName} in game {gameName} not found");

            }

            if (tr == null)
            {
                return BadRequest($"Year Of Plenty requires a TradeResource in the Body of the request");
            }
            int total = tr.Brick + tr.Wheat + tr.Wood + tr.Ore + tr.Sheep;
            if (total != 2)
            {
                return BadRequest($"Year Of Plenty requires a TradeResource to have a total of two resources specified instead of {total}");
            }

            ret = playerResources.TSPlayDevCard(DevCardType.YearOfPlenty);
            if (!ret)
            {
                return NotFound($"{playerName} in game {gameName} does not have a Year Of Plenty to play.");

            }

            playerResources.TSAdd(tr);
            TSGlobal.PlayerState.TSAddLogEntry(new ResourceLog() { PlayerResources = playerResources, Action = ServiceAction.PlayedYearOfPlenty, PlayerName = playerName, TradeResource = tr });
            return Ok(playerResources);

        }

        [HttpPost("devcard/play/monopoly/{gameName}/{playerName}/{resourceType}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult PlayMonopoly(string gameName, string playerName, ResourceType resourceType)
        {
            bool ret = TSGlobal.PlayerState.TSGetPlayerResources(gameName, playerName, out ClientState playerResources);

            if (!ret)
            {
                return NotFound($"{playerName} in game {gameName} not found");

            }
            bool set = playerResources.TSPlayDevCard(DevCardType.Monopoly);
            if (!set)
            {
                return NotFound($"{playerName} in game {gameName} does not have a Monopoly to play.");
            }


            int count = 0;
            foreach (var name in TSGlobal.PlayerState.TSGetPlayers(gameName))
            {
                if (name == playerName) continue;
                TSGlobal.PlayerState.TSGetPlayerResources(gameName, playerName, out ClientState victim); 
                count += victim.TSTakeAll(resourceType);// this logs the loss of cards
            }

            playerResources.TSAdd(count, resourceType);
            TSGlobal.PlayerState.TSAddLogEntry(new MonopolyLog() { PlayerResources = playerResources, Action = ServiceAction.PlayedMonopoly, PlayerName = playerName, ResourceType=resourceType, Count=count }); //logs the gain of cards
            TSGlobal.PlayerState.TSReleaseMonitors(gameName);

            return Ok(playerResources);
        }
        [HttpPost("devcard/play/roadbuilding/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult PlayRoadBuilding(string gameName, string playerName)
        {
            bool ret = TSGlobal.PlayerState.TSGetPlayerResources(gameName, playerName, out ClientState playerResources);

            if (!ret)
            {
                return NotFound($"{playerName} in game {gameName} not found");

            }
            bool set = playerResources.TSPlayDevCard(DevCardType.RoadBuilding);
            if (!set)
            {
                return NotFound($"{playerName} in game {gameName} does not have a Road Building card to play.");
            }

            

            playerResources.TSAddEntitlement(Entitlement.Road);
            playerResources.TSAddEntitlement(Entitlement.Road);


            TSGlobal.PlayerState.TSAddLogEntry(new ServiceLogEntry() { Action = ServiceAction.PlayedRoadBuilding, PlayerName = playerName}); 
            TSGlobal.PlayerState.TSReleaseMonitors(gameName);

            return Ok(playerResources.TSSerialize());
        }
        [HttpPost("devcard/play/knight/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult PlayKnight(string gameName, string playerName)
        {
            bool ret = TSGlobal.PlayerState.TSGetPlayerResources(gameName, playerName, out ClientState playerResources);

            if (!ret)
            {
                return NotFound($"{playerName} in game {gameName} not found");

            }
            bool set = playerResources.TSPlayDevCard(DevCardType.Knight);
            if (!set)
            {
                return NotFound($"{playerName} in game {gameName} does not have a Knight card to play.");
            }


            TSGlobal.PlayerState.TSAddLogEntry(new ServiceLogEntry() { Action = ServiceAction.PlayedKnight, PlayerName = playerName });
            TSGlobal.PlayerState.TSReleaseMonitors(gameName);

            return Ok(playerResources);
        }
    }
}
