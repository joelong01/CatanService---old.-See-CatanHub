using CatanService.State;
using CatanSharedModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CatanService.Controllers
{
    public static class PurchaseHelper
    {
        public static TradeResources GetCost(Entitlement entitlement)
        {

            switch (entitlement)
            {
                case Entitlement.Undefined:
                    return null;

                case Entitlement.DevCard:
                    return new TradeResources()
                    {
                        Wheat = 1,
                        Ore = 1,
                        Sheep = 1
                    };
                case Entitlement.Settlement:
                    return new TradeResources()
                    {
                        Wheat = 1,
                        Sheep = 1,
                        Wood = 1,
                        Brick = 1

                    };
                case Entitlement.City:
                    return new TradeResources()
                    {
                        Wheat = 2,
                        Ore = 3,
                    };
                case Entitlement.Road:
                    return new TradeResources()
                    {
                        Brick = 1,
                        Wood = 1
                    };
                default:
                    break;
            }

            return null;
        }



        public static bool ValidateResources(PlayerState resources, TradeResources cost)
        {
            if (cost.Wheat > resources.Wheat ||
               cost.Sheep > resources.Sheep ||
               cost.Ore > resources.Ore ||
               cost.Brick > resources.Brick ||
               cost.Wood > resources.Wood)
            {
                return false;
            }

            return true;
        }

    }
    [Route("api/catan/purchase")]
    [ApiController]
    public class PurchaseController : ControllerBase
    {
        [HttpPost("{gameName}/{playerName}/{entitlement}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
        public IActionResult Purchase(string gameName, string playerName, Entitlement entitlement)
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


            var cost = PurchaseHelper.GetCost(entitlement);
            if (cost == null)
            {
                return BadRequest(new CatanResult() { Request = this.Request.Path, Description = $"{entitlement} unknown or unset" });
            }
            bool valid = PurchaseHelper.ValidateResources(resources, cost);
            if (!valid)
            {
                Response.StatusCode = 402;
                return new JsonResult(new CatanResult() { Request = this.Request.Path, Description = $"{playerName} does not have the resources necessary to purchase {entitlement}" });
            }

            if (entitlement == Entitlement.DevCard)
            {
                DevCardType card = game.TSGetDevCard();
                if (card == DevCardType.Unknown)
                {
                    //
                    //  no more dev cards!
                    return NotFound(new CatanResult() { Request = this.Request.Path, Description = "No more dev cards available.  no charge" }); // TODO
                }

                resources.TSAddDevCard(card);
            }
            else
            {
                resources.TSAddEntitlement(entitlement);
            }

            resources.TSAdd(cost.GetNegated());
            game.TSAddLogRecord( new PurchaseLog() { Entitlement = entitlement, Action = ServiceAction.Purchased, PlayerResources = resources, PlayerName = playerName, RequestUrl = this.Request.Path });
            game.TSReleaseMonitors();
            return Ok(resources);
        }
        /// <summary>
        ///     Refunds an Entitlment purchase
        ///     Validates that the entitlement exits in the player resources
        /// </summary>
        /// <param name="gameName"></param>
        /// <param name="playerName"></param>
        /// <param name="entitlement"></param>
        /// <returns></returns>
        [HttpPost("refund/{gameName}/{playerName}/{entitlement}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult Refund(string gameName, string playerName, Entitlement entitlement)
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
            var cost = PurchaseHelper.GetCost(entitlement);
            if (cost == null)
            {
                return BadRequest(new CatanResult() { Request = this.Request.Path, Description = $"{entitlement} must be specified" });
            }

            if (resources.TSRemoveEntitlement(entitlement) == false)
            {
                return BadRequest(new CatanResult() { Request = this.Request.Path, Description = $"{playerName} does not have a {entitlement} entitlement to refund" });
            }


            resources.TSAdd(cost.GetNegated());
            game.TSAddLogRecord( new PurchaseLog() { Entitlement = entitlement, Action = ServiceAction.Refund, PlayerResources = resources, PlayerName = playerName, RequestUrl = this.Request.Path });
            game.TSReleaseMonitors();
            return Ok(resources);
        }




    }
}