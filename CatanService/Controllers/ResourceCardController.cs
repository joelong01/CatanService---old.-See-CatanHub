using CatanSharedModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace CatanService.Controllers
{
    [Route("api/catan/resourcecards")]
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
        public ActionResult<string> GetCards(string gameName, string playerName)
        {
            bool ret = Globals.SafeGetPlayerResources(gameName, playerName, out PlayerResources resources);

            if (!ret)
            {
                return NotFound($"User {playerName} in game {gameName} not found");
            }

            return Ok(JsonSerializer.Serialize<PlayerResources>(resources));
        }

        [HttpPost("add/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<string> AddAsync([FromBody] PlayerResources toAdd, string gameName, string playerName)
        {
            bool ret = Globals.SafeGetPlayerResources(gameName, playerName, out PlayerResources playerResources);

            if (!ret)
            {
                return NotFound($"{playerName} in game { gameName} not found");
            }
            if (toAdd.Brick + playerResources.Brick < 0)
            {
                return BadRequest($"{playerName} in game { gameName} is trying to use more Brick than they have. Request is {toAdd.Brick} have {playerResources.Brick}");
            }
            if (toAdd.Wood + playerResources.Wood < 0)
            {
                return BadRequest($"{playerName} in game { gameName} is trying to use more Wood than they have. Request is {toAdd.Wood} have {playerResources.Wood}");
            }
            if (toAdd.Sheep + playerResources.Sheep < 0)
            {
                return BadRequest($"{playerName} in game { gameName} is trying to use more Sheep than they have. Request is {toAdd.Sheep} have {playerResources.Sheep}");
            }
            if (toAdd.Wheat + playerResources.Wheat < 0)
            {
                return BadRequest($"{playerName} in game { gameName} is trying to use more Wheat than they have. Request is {toAdd.Wheat} have {playerResources.Wheat}");
            }
            if (toAdd.Ore + playerResources.Ore < 0)
            {
                return BadRequest($"{playerName} in game { gameName} is trying to use more Ore than they have. Request is {toAdd.Ore} have {playerResources.Ore}");
            }
            if (toAdd.GoldMine + playerResources.GoldMine < 0)
            {
                return BadRequest($"{playerName} in game { gameName} is trying to use more GoldMine than they have. Request is {toAdd.GoldMine} have {playerResources.GoldMine}");
            }

            AddResources(playerResources, toAdd);
            Globals.AddChange(playerResources);
            Globals.ReleaseHangingGet(playerResources.ResourceUpdateTCS);
            Globals.ReleaseGlobalMonitor();
            return Ok(JsonSerializer.Serialize<PlayerResources>(playerResources));

        }
        [HttpPost("tradegold/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<string> TradeGoldAsync([FromBody] PlayerResources trade, string gameName, string playerName)
        {
            _ = Globals.SafeGetPlayerResources(gameName, playerName, out PlayerResources playerResources);
            if (playerResources == null)
            {
                return NotFound($"{playerName} in game { gameName} not found");
            }

            if (trade.GoldMine <= 0)
            {
                return BadRequest($"[Player={playerName}] [Game={gameName}]\n in tradegold, the #gold should be positiver instead of {trade.GoldMine}");
            }

            int askCount = trade.Brick + trade.Wood + trade.Wheat + trade.Ore + trade.Sheep;
            if (askCount != -1 * trade.GoldMine)
            {
                return BadRequest($"[Player={playerName}] [Game={gameName}]\n Asking for {askCount} resources, only have {trade.GoldMine} Gold");
            }
            Globals.AddChange(trade);
            AddResources(playerResources, trade);
            Globals.ReleaseHangingGet(playerResources.ResourceUpdateTCS);
            Globals.ReleaseGlobalMonitor();
            return Ok(JsonSerializer.Serialize<PlayerResources>(playerResources));

        }
        [HttpPost("trade/{gameName}/{fromName}/{toName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<string> TradeAsync([FromBody] PlayerResources[] trade, string gameName, string fromName, string toName)
        {
            _=  Globals.SafeGetPlayerResources(gameName, fromName, out PlayerResources fromResources);
            if (fromResources == null)
            {
                return NotFound($"{fromName} in game { gameName} not found");
            }

            var fromTrade = trade[0];
            var toTrade = trade[1];

            _ = Globals.SafeGetPlayerResources(gameName, toName, out PlayerResources toResources);
            if (toResources == null)
            {
                return NotFound($"{toName} in game { gameName} not found");
            }
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

            // give to to 
            AddResources(toResources, fromTrade);
            // give to from
            AddResources(fromResources, toTrade);

            // negate
            toTrade.Negate();
            fromTrade.Negate();

            //subract the resources
            AddResources(fromResources, fromTrade);
            AddResources(toResources, toTrade);
            Globals.AddChange(fromResources);
            Globals.AddChange(toResources);
            Globals.ReleaseHangingGet(fromResources.ResourceUpdateTCS);
            Globals.ReleaseHangingGet(toResources.ResourceUpdateTCS);
            Globals.ReleaseGlobalMonitor();
            return Ok(JsonSerializer.Serialize<PlayerResources[]>(new PlayerResources[2] { fromResources, toResources }));
        }

        [HttpPost("take/{gameName}/{fromName}/{toName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<string> TakeAsync(string gameName, string fromName, string toName)
        {
            _ = Globals.SafeGetPlayerResources(gameName, fromName, out PlayerResources fromResources);
            if (fromResources == null)
            {
                return NotFound($"{fromName} in game { gameName} not found");
            }

            _ = Globals.SafeGetPlayerResources(gameName, toName, out PlayerResources toResources);
            if (toResources == null)
            {
                return NotFound($"{toName} in game { gameName} not found");
            }

            if (fromResources.TotalResources == 0)
            {
                return Ok(JsonSerializer.Serialize<PlayerResources[]>(new PlayerResources[2] { fromResources, toResources }));
            }

            Random rand = new Random((int)DateTime.Now.Ticks);
            int index = rand.Next(0, fromResources.TotalResources);

            if (index < fromResources.Wheat)
            {
                fromResources.Wheat--; // take a wheat
                toResources.Wheat++;  //add it
                return Ok(JsonSerializer.Serialize<PlayerResources[]>(new PlayerResources[2] { fromResources, toResources }));
            }
            else
            {
                index -= fromResources.Wheat;
            }

            if (index < fromResources.Wood)
            {
                fromResources.Wood--; // take a Wood
                toResources.Wood++;  //add it
                return Ok(JsonSerializer.Serialize<PlayerResources[]>(new PlayerResources[2] { fromResources, toResources }));
            }
            else
            {
                index -= fromResources.Wood;
            }

            if (index < fromResources.Brick)
            {
                fromResources.Brick--; // take a Brick
                toResources.Brick++;  //add it
                return Ok(JsonSerializer.Serialize<PlayerResources[]>(new PlayerResources[2] { fromResources, toResources }));
            }
            else
            {
                index -= fromResources.Brick;
            }

            if (index < fromResources.Sheep)
            {
                fromResources.Sheep--; // take a Sheep
                toResources.Sheep++;  //add it
                return Ok(JsonSerializer.Serialize<PlayerResources[]>(new PlayerResources[2] { fromResources, toResources }));
            }
            else
            {
                index -= fromResources.Sheep;
            }


            fromResources.Ore--; // take a Ore
            toResources.Ore++;  //add it
            Globals.ReleaseHangingGet(fromResources.ResourceUpdateTCS);
            Globals.ReleaseHangingGet(toResources.ResourceUpdateTCS);
            Globals.ReleaseGlobalMonitor();
            return Ok(JsonSerializer.Serialize<PlayerResources[]>(new PlayerResources[2] { fromResources, toResources }));


        }

        [HttpPost("meritimetrade/{gameName}/{playerName}/{resourceToGive}/{count}/{resourceToGet}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<string> TakeAsync(string gameName, string playerName, ResourceType resourceToGive, int count, ResourceType resourceToGet)
        {
            _ = Globals.SafeGetPlayerResources(gameName, playerName, out PlayerResources playerResources);
            if (playerResources == null)
            {
                return NotFound($"{playerName} in game { gameName} not found");
            }

            // make sure they have enough to trade

            if (playerResources.ResourceCount(resourceToGive) < count)
            {
                return BadRequest($"[Player={playerName}] [Game={gameName}]\n needs {count} of {resourceToGive} only has {playerResources.ResourceCount(resourceToGive)} ");
            }

            playerResources.AddResource(resourceToGive, -count);
            playerResources.AddResource(resourceToGet, 1);
            Globals.AddChange(playerResources);
            Globals.ReleaseGlobalMonitor();
            Globals.ReleaseHangingGet(playerResources.ResourceUpdateTCS);
            Globals.ReleaseGlobalMonitor();

            return Ok(JsonSerializer.Serialize<PlayerResources>(playerResources));
        }
        private void AddResources(PlayerResources playerResources, PlayerResources resourceCount)
        {
            //
            //  there is a race condition where the game is allocating resources and the player
            //  clicks to buy something too quickly since the service doesn't keep track of game state
            Globals.rwLock.EnterWriteLock();
            try
            {
                playerResources.Brick += resourceCount.Brick;
                playerResources.Wood += resourceCount.Wood;
                playerResources.Wheat += resourceCount.Wheat;
                playerResources.Ore += resourceCount.Ore;
                playerResources.Sheep += resourceCount.Sheep;
                playerResources.GoldMine += resourceCount.GoldMine;
            }
            finally
            {
                Globals.rwLock.ExitWriteLock();
            }
        }

    }
}
