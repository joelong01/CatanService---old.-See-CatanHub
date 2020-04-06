using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CatanSharedModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CatanService.Controllers
{
    [Route("api/catan/purchase")]
    [ApiController]
    public class PurchaseController : ControllerBase
    {
        [HttpPost("{gameName}/{playerName}/{entitlement}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<JsonResult> Purchase(string gameName, string playerName, Entitlement entitlement)
        {

            bool ret = TSGlobal.GlobalState.TSGetPlayerResources(gameName, playerName, out ClientState resources);
            if (!ret)
            {
                return NotFound($"{playerName} in game { gameName} not found");

            }


            var cost = GetCost(entitlement);
            bool valid = ValidateResources(resources, GetCost(entitlement));
            if (!valid)
            {
                return BadRequest($"{playerName} does not have the resources necessary to purchase {entitlement}");
            }
            resources.TSAdd(cost.GetNegated());
            TSGlobal.GlobalState.TSAddLogEntry(new PurchaseLog() { Entitlement = entitlement, Action = ServiceAction.Purchased, PlayerResources = resources, PlayerName = playerName });
            TSGlobal.GlobalState.TSReleaseMonitors(gameName);
            return Ok(resources.TSSerialize());
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
        public ActionResult<JsonResult> Refund(string gameName, string playerName, Entitlement entitlement)
        {

            bool ret = TSGlobal.GlobalState.TSGetPlayerResources(gameName, playerName, out ClientState resources);
            if (!ret)
            {
                return NotFound($"{playerName} in game { gameName} not found");

            }
            var cost = GetCost(entitlement);
            if (cost == null)
            {
                return BadRequest($"{entitlement} must be specified");
            }

            if (resources.TSRemoveEntitlement(entitlement) == false)
            {
                return BadRequest($"{playerName} does not have a {entitlement} entitlement to refund");
            }


            resources.TSAdd(cost.GetNegated());
            TSGlobal.GlobalState.TSAddLogEntry(new PurchaseLog() { Entitlement = entitlement, Action = ServiceAction.Refund, PlayerResources = resources, PlayerName = playerName });
            TSGlobal.GlobalState.TSReleaseMonitors(gameName);
            return Ok(resources.TSSerialize());
        }

        private TradeResources GetCost(Entitlement entitlement)
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
                default:
                    break;
            }

            return null;
        }



        private bool ValidateResources(ClientState resources, TradeResources cost)
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
}