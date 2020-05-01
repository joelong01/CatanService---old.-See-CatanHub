using Catan.Proxy;
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
                return NotFound(new CatanResult(CatanError.NoGameWithThatName) { Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
            }
            var resources = game.GetPlayer(playerName);
            if (resources == null)
            {
                return NotFound(new CatanResult(CatanError.NoPlayerWithThatName) { CantanRequest = new CatanRequest() { Url = this.Request.Path, Body = null, BodyType = BodyType.None }, Description = $"{playerName} in game '{gameName}' not found" });

            }


            var cost = PurchaseHelper.GetCost(Entitlement.DevCard);
            bool valid = PurchaseHelper.ValidateResources(resources, cost);
            if (!valid)
            {
                return BadRequest(new CatanResult(CatanError.DevCardsSoldOut) { Request = this.Request.Path, Description = $"{playerName} does not have the resources necessary to purchase a DevCard" });
            }


            DevCardType card = game.TSGetDevCard();
            if (card == DevCardType.Unknown)
            {
                //
                //  no more dev cards!
                return NotFound(new CatanResult(CatanError.DevCardsSoldOut) { Request = this.Request.Path, Description = "No more dev cards available.  no charge" });
            }

            resources.TSAddDevCard(card);
            resources.TSAdd(cost.GetNegated());
            game.TSAddLogRecord(new PurchaseLog() { Entitlement = Entitlement.DevCard, Action = CatanAction.Purchased, PlayerResources = resources, PlayerName = playerName, RequestUrl = this.Request.Path });
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
        [HttpPost("play/yearofplenty/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult PlayYearOfPlenty([FromBody] TradeResources tr, string gameName, string playerName)
        {
            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                 return NotFound(new CatanResult(CatanError.NoGameWithThatName){ Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
                

            }
            var resources = game.GetPlayer(playerName);
            if (resources == null)
            {
                return NotFound(new CatanResult(CatanError.NoPlayerWithThatName) { Request = this.Request.Path, Description = $"{playerName} in game '{gameName}' not found" });

            }
            if (tr == null)
            {
                return BadRequest(new CatanResult(CatanError.MissingData)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path, Body = tr, BodyType = BodyType.TradeResources },
                    Description = $"Year Of Plenty requires a TradeResource in the Body of the request"
                });
            }
            int total = tr.Brick + tr.Wheat + tr.Wood + tr.Ore + tr.Sheep;
            if (total != 2)
            {
                return BadRequest(new CatanResult(CatanError.BadTradeResources)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path, Body = tr, BodyType = BodyType.TradeResources },
                    Description = $"Year Of Plenty requires a TradeResource to have a total of two resources specified instead of {total}"
                });  
            }

            bool ret = resources.TSPlayDevCard(DevCardType.YearOfPlenty);
            if (!ret)
            {
                return NotFound(new CatanResult(CatanError.NoResource) 
                { 
                    CantanRequest = new CatanRequest() { Url = this.Request.Path, Body = tr, BodyType = BodyType.TradeResources }, 
                    Description = $"{playerName} in game {gameName} does not have a Year Of Plenty to play." 
                });

            }

            resources.TSAdd(tr);
            game.TSAddLogRecord(new PlayedPlayedYearOfPlentyLog() { PlayerName = playerName, Acquired = tr, RequestUrl = this.Request.Path });
            return Ok(resources);

        }

        [HttpPost("play/monopoly/{gameName}/{playerName}/{resourceType}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult PlayMonopoly(string gameName, string playerName, ResourceType resourceType)
        {
            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                return NotFound(new CatanResult(CatanError.NoGameWithThatName){CantanRequest = new CatanRequest() { Url = this.Request.Path, Body = null, BodyType = BodyType.None },Description = $"Game '{gameName}' does not exist",Request = this.Request.Path});
            }
            var resources = game.GetPlayer(playerName);
            if (resources == null)
            {
                return NotFound(new CatanResult(CatanError.NoPlayerWithThatName) { CantanRequest = new CatanRequest() { Url = this.Request.Path, Body = null, BodyType = BodyType.None }, Description = $"{playerName} in game '{gameName}' not found" });

            }
            bool set = resources.TSPlayDevCard(DevCardType.Monopoly);
            if (!set)
            {
                return NotFound(new CatanResult(CatanError.NoResource) { CantanRequest= new CatanRequest() { Url = this.Request.Path, Body = null, BodyType = BodyType.None }, Description = $"{playerName} in game {gameName} does not have a Monopoly to play." });
            }

            var ret = game.TSTakeAll(this.Request.Path, resourceType);


            resources.TSAdd(ret.total, resourceType);
            ret.impactedPlayers[playerName] = ret.total;
            game.TSAddLogRecord(new PlayedMonopoly() { PlayerName = playerName, ResourceType = resourceType,  RequestUrl = this.Request.Path }); 
            game.TSReleaseMonitors();

            return Ok(resources);
        }
        [HttpPost("play/roadbuilding/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult PlayRoadBuilding(string gameName, string playerName)
        {
            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                return NotFound(new CatanResult(CatanError.NoGameWithThatName){CantanRequest = new CatanRequest() { Url = this.Request.Path, Body = null, BodyType = BodyType.None },Description = $"Game '{gameName}' does not exist",Request = this.Request.Path});
            }
            var resources = game.GetPlayer(playerName);
            if (resources == null)
            {
                return NotFound(new CatanResult(CatanError.NoPlayerWithThatName) {CantanRequest = new CatanRequest() { Url = this.Request.Path, Body = null, BodyType = BodyType.None }, Description = $"{playerName} in game '{gameName}' not found" });

            }
            bool set = resources.TSPlayDevCard(DevCardType.RoadBuilding);
            if (!set)
            {
                return NotFound(new CatanResult(CatanError.NoMoreResource) { CantanRequest = new CatanRequest() { Url = this.Request.Path, Body = null, BodyType = BodyType.None }, Description = $"{playerName} in game {gameName} does not have a Road Building card to play." });
            }



            resources.TSAddEntitlement(Entitlement.Road);
            resources.TSAddEntitlement(Entitlement.Road);


            game.TSAddLogRecord(new PlayedDevCardModel() { Action = CatanAction.PlayedDevCard, DevCard = DevCardType.RoadBuilding, PlayerName = playerName, RequestUrl = this.Request.Path });
            game.TSReleaseMonitors();

            return Ok(resources);
        }
        
        /// <summary>
        ///     The originator (playerName) has already done this action in their game by the time this is called.
        ///     all we do is put the log in the queue to be notified back to the rest of the clients.
        /// </summary>
        /// <param name="knightLog"></param>
        /// <param name="gameName"></param>
        /// <param name="playerName"></param>
        /// <returns></returns>
        
        [HttpPost("play/knight/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult PlayKnight([FromBody] KnightPlayedLog knightLog, string gameName, string playerName)
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
            bool set = resources.TSPlayDevCard(DevCardType.Knight);
            if (!set)
            {
                return NotFound(new CatanResult(CatanError.InsufficientResource) { Request = this.Request.Path, Description = $"{playerName} in game {gameName} does not have a Knight card to play." });
            }
            
            game.TSAddLogRecord(knightLog); 
            game.TSReleaseMonitors();

            return Ok(resources);
        }
    }
}