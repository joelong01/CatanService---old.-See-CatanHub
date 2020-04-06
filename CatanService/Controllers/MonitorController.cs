using System;
using System.Collections.Generic;
using System.Text.Json;
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
        public async Task<ActionResult<string>> MonitorResources(string gameName, string playerName)
        {
            //
            //  need this to get the TaskCompletionSource
            bool ret = TSGlobal.GlobalState.TSGetPlayerResources(gameName, playerName, out ClientState resources);
            if (!ret)
            {
                return NotFound($"{playerName} in game { gameName} not found");

            }
            List<ServiceLogEntry> list = await resources.TSWaitForLog();
            return Ok(TSGlobal.Serialize<List<ServiceLogEntry>>(list));
        }

    }
}