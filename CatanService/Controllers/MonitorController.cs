using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CatanSharedModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CatanService.Controllers
{

    [Route("api/catan/monitor")]
    [ApiController]
    public class MonitorController : ControllerBase
    {
        [HttpGet("{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> MonitorResources(string gameName, string playerName)
        {
            //
            //  need this to get the TaskCompletionSource
            bool ret = TSGlobal.PlayerState.TSGetPlayerResources(gameName, playerName, out ClientState resources);
            if (!ret)
            {
                return NotFound($"{playerName} in game { gameName} not found");

            }
            List<object> list = await resources.TSWaitForLog();
            if (list.Count == 0)
            {
                return BadRequest($"A client is already registered for this player {playerName}");
            }
            return Ok(list);

        }

    }
}