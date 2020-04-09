using CatanService.State;
using CatanSharedModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CatanService.Controllers
{
    [Route("api/catan/devcard")]
    [ApiController]
    public class DevCardController : ControllerBase
    {
        [HttpPost("{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult Purchase(string gameName, string playerName)
        {
            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                return NotFound(new CatanResult() { Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
            }
            var resources = game.GetPlayer(playerName);
            if (resources == null)
            {
                return NotFound(new CatanResult() { Request = this.Request.Path, Description = $"{playerName} in game '{gameName}' not found" });

            }


            var cost = PurchaseHelper.GetCost(Entitlement.DevCard);
            bool valid = PurchaseHelper.ValidateResources(resources, cost);
            if (!valid)
            {
                return BadRequest(new CatanResult() { Request = this.Request.Path, Description = $"{playerName} does not have the resources necessary to purchase a DevCard" });
            }


            DevCardType card = game.TSGetDevCard();
            if (card == DevCardType.Unknown)
            {
                //
                //  no more dev cards!
                return NotFound(new CatanResult() { Request = this.Request.Path, Description = "No more dev cards available.  no charge" });
            }

            resources.TSAddDevCard(card);
            resources.TSAdd(cost.GetNegated());
            game.TSAddLogRecord(new PurchaseLog() { Entitlement = Entitlement.DevCard, Action = ServiceAction.Purchased, PlayerResources = resources, PlayerName = playerName, RequestUrl = this.Request.Path });
            game.TSReleaseMonitors();
            return Ok(resources);
        }
        /// <summary>
        ///     Play YearOfPlenty
        ///     Validate
        ///         1) Entitlement Exists
        ///         2) input parameters (in particular that the TradeResources are configured correctly)
        ///         
        ///     Note that each dev card has a different data shape, so we have a consume API per dev card.
        /// </summary>
        /// <param name="tr"></param>
        /// <param name="gameName"></param>
        /// <param name="playerName"></param>
        /// <returns></returns>
        [HttpPost("devcard/play/yearofplenty/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult PlayYearOfPlenty([FromBody] TradeResources tr, string gameName, string playerName)
        {
            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                return NotFound(new CatanResult() { Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
            }
            var playerResources = game.GetPlayer(playerName);
            if (playerResources == null)
            {
                return NotFound(new CatanResult() { Request = this.Request.Path, Description = $"{playerName} in game '{gameName}' not found" });

            }

            if (tr == null)
            {
                return BadRequest(new CatanResultWithBody<TradeResources>(tr) { Request = this.Request.Path, Description = $"Year Of Plenty requires a TradeResource in the Body of the request" });
            }
            int total = tr.Brick + tr.Wheat + tr.Wood + tr.Ore + tr.Sheep;
            if (total != 2)
            {
                return BadRequest(new CatanResultWithBody<TradeResources>(tr) { Request = this.Request.Path, Description = $"Year Of Plenty requires a TradeResource to have a total of two resources specified instead of {total}" });
            }

            bool ret = playerResources.TSPlayDevCard(DevCardType.YearOfPlenty);
            if (!ret)
            {
                return NotFound(new CatanResultWithBody<TradeResources>(tr) { Request = this.Request.Path, Description = $"{playerName} in game {gameName} does not have a Year Of Plenty to play." });

            }

            playerResources.TSAdd(tr);
            game.TSAddLogRecord(new ResourceLog() { PlayerResources = playerResources, Action = ServiceAction.PlayedYearOfPlenty, PlayerName = playerName, TradeResource = tr, RequestUrl = this.Request.Path });
            return Ok(playerResources);

        }

        [HttpPost("devcard/play/monopoly/{gameName}/{playerName}/{resourceType}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult PlayMonopoly(string gameName, string playerName, ResourceType resourceType)
        {
            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                return NotFound(new CatanResult() { Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
            }
            var playerResources = game.GetPlayer(playerName);
            if (playerResources == null)
            {
                return NotFound(new CatanResult() { Request = this.Request.Path, Description = $"{playerName} in game '{gameName}' not found" });

            }
            bool set = playerResources.TSPlayDevCard(DevCardType.Monopoly);
            if (!set)
            {
                return NotFound(new CatanResult() { Request = this.Request.Path, Description = $"{playerName} in game {gameName} does not have a Monopoly to play." });
            }

            gameName = gameName.ToLower();
            playerName = playerName.ToLower();
            int count = game.TSTakeAll(this.Request.Path, resourceType);

            playerResources.TSAdd(count, resourceType);
            game.TSAddLogRecord(new MonopolyLog() { PlayerResources = playerResources, Action = ServiceAction.PlayedMonopoly, PlayerName = playerName, ResourceType = resourceType, Count = count, RequestUrl = this.Request.Path }); //logs the gain of cards
            game.TSReleaseMonitors();

            return Ok(playerResources);
        }
        [HttpPost("devcard/play/roadbuilding/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult PlayRoadBuilding(string gameName, string playerName)
        {
            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                return NotFound(new CatanResult() { Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
            }
            var playerResources = game.GetPlayer(playerName);
            if (playerResources == null)
            {
                return NotFound(new CatanResult() { Request = this.Request.Path, Description = $"{playerName} in game '{gameName}' not found" });

            }
            bool set = playerResources.TSPlayDevCard(DevCardType.RoadBuilding);
            if (!set)
            {
                return NotFound(new CatanResult() { Request = this.Request.Path, Description = $"{playerName} in game {gameName} does not have a Road Building card to play." });
            }



            playerResources.TSAddEntitlement(Entitlement.Road);
            playerResources.TSAddEntitlement(Entitlement.Road);


            game.TSAddLogRecord(new ServiceLogRecord() { Action = ServiceAction.PlayedRoadBuilding, PlayerName = playerName, RequestUrl = this.Request.Path });
            game.TSReleaseMonitors();

            return Ok(playerResources);
        }
        [HttpPost("devcard/play/knight/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult PlayKnight(string gameName, string playerName)
        {
            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                return NotFound(new CatanResult() { Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
            }
            var playerResources = game.GetPlayer(playerName);
            if (playerResources == null)
            {
                return NotFound(new CatanResult() { Request = this.Request.Path, Description = $"{playerName} in game '{gameName}' not found" });

            }
            bool set = playerResources.TSPlayDevCard(DevCardType.Knight);
            if (!set)
            {
                return NotFound(new CatanResult() { Request = this.Request.Path, Description = $"{playerName} in game {gameName} does not have a Knight card to play." });
            }


            game.TSAddLogRecord(new ServiceLogRecord() { Action = ServiceAction.PlayedKnight, PlayerName = playerName, RequestUrl = this.Request.Path });
            game.TSReleaseMonitors();

            return Ok(playerResources);
        }
    }
}