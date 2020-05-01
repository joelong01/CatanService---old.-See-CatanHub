﻿using CatanService.State;
using Catan.Proxy;
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
                return NotFound(new CatanResult(CatanError.NoGameWithThatName) { CantanRequest = new CatanRequest() { Url = this.Request.Path, Body = null, BodyType = BodyType.None }, Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
            }
            var playerState = game.GetPlayer(playerName);
            if (playerState == null)
            {
                return NotFound(new CatanResult(CatanError.NoPlayerWithThatName) { Request = this.Request.Path, Description = $"{playerName} in game {gameName} not found" });

            }


            var cost = PurchaseHelper.GetCost(entitlement);
            if (cost == null)
            {
                return BadRequest(new CatanResult(CatanError.BadEntitlement) { Request = this.Request.Path, Description = $"{entitlement} unknown or unset" });
            }
            bool valid = PurchaseHelper.ValidateResources(playerState, cost);
            if (!valid)
            {
                Response.StatusCode = 402;
                return new JsonResult(new CatanResult(CatanError.NotEnoughResourcesToPurchase) { Request = this.Request.Path, Description = $"{playerName} does not have the resources necessary to purchase {entitlement}" });
            }

            if (entitlement == Entitlement.DevCard)
            {
                return BadRequest(new CatanResult(CatanError.BadEntitlement) { Request = this.Request.Path, Description = $"{entitlement} is bought through the api/catan/devcard path" });
            }
            bool available = playerState.TSAllocateEntitlement(entitlement); // are there entitlements of this type available?
            if (!available)
            {
                return NotFound(new CatanResult(CatanError.LimitExceeded) { Request = this.Request.Path, Description = $"{playerName} has the maximum allowd of {entitlement} " });
            }

            playerState.TSAddEntitlement(entitlement);
            playerState.TSAdd(cost.GetNegated());
            game.TSAddLogRecord(new PurchaseLog()
            {
                Entitlement = entitlement,
                Action = CatanAction.Purchased,
                PlayerResources = playerState,
                PlayerName = playerName,
                RequestUrl = this.Request.Path,
                UndoRequest = new CatanRequest()
                {
                    Url = $"api/catan/purchase/refund/{gameName}/{playerName}/{entitlement}",
                    Body = null,
                    BodyType = BodyType.None

                }
            });

            game.TSReleaseMonitors();
            return Ok(playerState);
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
            if (entitlement == Entitlement.Undefined)
            {
                return BadRequest(new CatanResult(CatanError.BadParameter) { Request = this.Request.Path, Description = $"{entitlement} must be specified" });
            }

            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                return NotFound(new CatanResult(CatanError.NoGameWithThatName) { Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
            }
            var playerState = game.GetPlayer(playerName);
            if (playerState == null)
            {
                return NotFound(new CatanResult(CatanError.NoPlayerWithThatName) { Request = this.Request.Path, Description = $"{playerName} in game '{gameName}' not found" });

            }
            var cost = PurchaseHelper.GetCost(entitlement);
            if (cost == null)
            {
                return BadRequest(new CatanResult(CatanError.BadLogRecord) { Request = this.Request.Path, Description = $"{entitlement} must be specified" });
            }

            if (playerState.TSRemoveEntitlement(entitlement) == false)
            {
                return BadRequest(new CatanResult(CatanError.BadEntitlement) { Request = this.Request.Path, Description = $"{playerName} does not have a {entitlement} entitlement to refund" });
            }

            if (playerState.TSFreeEntitlement(entitlement) == false)
            {
                return BadRequest(new CatanResult(CatanError.BadEntitlement) { Request = this.Request.Path, Description = $"Refunding a {entitlement} to  {playerName} would give them mroe than the max allowed."});
            }
            

            playerState.TSAdd(cost);
            game.TSAddLogRecord(new PurchaseLog()
            {
                LogType = LogType.Undo,
                Entitlement = entitlement,
                Action = CatanAction.Purchased,
                PlayerResources = playerState,
                PlayerName = playerName,
                RequestUrl = Request.Path,
                UndoRequest = new CatanRequest()
                {
                    Url = $"api/catan/purchase/{gameName}/{playerName}/{entitlement}",
                    Body = null,
                    BodyType = BodyType.None

                }
            });
            game.TSReleaseMonitors();
            return Ok(playerState);
        }




    }
}