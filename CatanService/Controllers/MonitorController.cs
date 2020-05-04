using CatanService.State;
using System.Threading.Tasks;
using Catan.Proxy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Text.Json;

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
            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                return NotFound(new CatanResult(CatanError.NoGameWithThatName) { Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
            }
            //if (!game.Started)
            //{
            //    return BadRequest(new CatanResult() { Request = this.Request.Path, Description = $"{playerName} in '{gameName}' tried to Monitor a game that hasn't started yet." });
            //}

            //
            //  need this to get the TaskCompletionSource
            var clientState = game.GetPlayer(playerName);

            if (clientState == null)
            {

                return NotFound(new CatanResult(CatanError.NoPlayerWithThatName) { Request = this.Request.Path, Description = $"{playerName} in game '{gameName}' not found" });

            }
            var logCollection = await clientState.TSWaitForLog();
            if (logCollection == null || logCollection.Count == 0)
            {
                return BadRequest(new CatanResult(CatanError.Unexpected) { Request = this.Request.Path, Description = $"Why did {playerName} release with no log entries?" });
            }
           
            return Ok(logCollection);

        }
        [HttpGet("logs/{gameName}/{playerName}/{startAt}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetAllLogs(string gameName, string playerName, int startAt)
        {
            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                return NotFound(new CatanResult(CatanError.NoGameWithThatName) { Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
            }

            var clientState = game.GetPlayer(playerName);

            if (clientState == null)
            {

                return NotFound(new CatanResult(CatanError.NoPlayerWithThatName) { Request = this.Request.Path, Description = $"{playerName} in game '{gameName}' not found" });

            }

            ServiceLogCollection response = clientState.GetLogCollection(startAt);
            return Ok(response);

        }
        [HttpPost("postclientlog/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult PostClientLog([FromBody] JsonElement body, string gameName, string playerName)
        {
            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                return NotFound(new CatanResult(CatanError.NoGameWithThatName) { Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
            }

            var clientState = game.GetPlayer(playerName);

            if (clientState == null)
            {

                return NotFound(new CatanResult(CatanError.NoPlayerWithThatName) { Request = this.Request.Path, Description = $"{playerName} in game '{gameName}' not found" });

            }

            LogHeader header = CatanProxy.DeserializeLogHeader(body.ToString());
            game.TSAddLogRecord(header);
            game.TSReleaseMonitors();
          
            return Ok(header as object);

        }

    }
}