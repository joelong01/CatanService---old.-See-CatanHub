using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CatanSharedModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CatanService.Controllers
{
    [Route("api/monitor")]
    [ApiController]
    public class MonitorController : ControllerBase
    {
        [HttpGet("resources/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<string>> MonitorResources(string gameName, string playerName)
        {
            //
            //  need this to get the TaskCompletionSource
            bool ret = Globals.SafeGetPlayerResources(gameName, playerName, out PlayerResources resources);

            if (!ret)
            {
                return NotFound($"User {playerName} in game {gameName} not found");
            }
            try
            {
                if (resources.ResourceUpdateTCS == null)
                {
                    //
                    //  TODO: bug race condition 
                    resources.ResourceUpdateTCS = new TaskCompletionSource<object>();
                }

                if (resources.ResourceUpdateTCS.Task.IsCompleted)
                {
                    resources.ResourceUpdateTCS = new TaskCompletionSource<object>();
                }

                await resources.ResourceUpdateTCS.Task; // this can hang for a long, long time
            }
            catch (Exception e)
            {
                return Ok($"Exception thrown {e}");
            }
            //
            //  need to get the latest resources
            ret = Globals.SafeGetPlayerResources(gameName, playerName, out resources);
            return Ok(JsonSerializer.Serialize<PlayerResources>(resources));
        }
        [HttpGet("resources/all")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<string>> MonitorAll(string gameName, string playerName)
        {

            if (Globals.ChangeLogTCS == null)
            {
                Globals.ChangeLogTCS = new TaskCompletionSource<object>();
            }

            await Globals.ChangeLogTCS.Task;
            Globals.ChangeLogTCS = null;
            var resources = Globals.CopyAndClearChangeLog();
            return Ok(JsonSerializer.Serialize<List<PlayerResources>>(resources));

        }
    }
}