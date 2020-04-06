using CatanSharedModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Text.Json;

namespace CatanService.Controllers
{
    [ApiController]
    [Route("api/catan/game")]
    public class GameController : ControllerBase
    {
        
        private readonly ILogger<GameController> _logger;

        public GameController(ILogger<GameController> logger)
        {
            _logger = logger;
        }



        [HttpPost("register/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<string> RegisterAsync(string gameName, string playerName)
        {
            bool ret = TSGlobal.GlobalState.TSGetPlayerResources(gameName, playerName, out ClientState resources);
            if (!ret)
            {
                resources = new ClientState()
                {
                    PlayerName = playerName,
                    GameName = gameName
                };

                TSGlobal.GlobalState.TSSetPlayerResources(gameName, playerName, resources);
                TSGlobal.GlobalState.TSAddLogEntry(new GameLog() { Players = TSGlobal.GlobalState.TSGetPlayers(gameName), PlayerName = playerName, Action = ServiceAction.PlayerAdded });

            }
            
            TSGlobal.GlobalState.TSReleaseMonitors(gameName); // this will cause the client monitoring changes to get a list of players from the GameUpdateLog
            return Ok(resources.TSSerialize());

        }




        [HttpDelete("delete/{gameName}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<string> DeleteAsync(string gameName)
        {
            var ret = TSGlobal.GlobalState.TSDeleteGame(gameName);
            if (!ret)
            {
                return NotFound($"{gameName} not found");


            }
            TSGlobal.GlobalState.TSAddLogEntry(new GameLog() { Players = TSGlobal.GlobalState.TSGetPlayers(gameName), Action = ServiceAction.GameDeleted });
            TSGlobal.GlobalState.TSReleaseMonitors(gameName); // this will cause the client monitoring changes to get a list of players from the GameUpdateLog
            return Ok($"{gameName} deleted");


        }

        [HttpGet("users/{gameName}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<string> GetUsersAsync(string gameName)
        {
            var players = TSGlobal.GlobalState.TSGetPlayers(gameName);
            if (players.Count == 0) return Ok($"No Players in game {gameName}");

            return Ok(TSGlobal.Serialize<List<string>>(players));
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<string> GetGamesAsync()
        {
            var games = TSGlobal.GlobalState.TSGetGames();
            if (games.Count == 0) return Ok($"No games currently being played");

            return Ok(TSGlobal.Serialize<List<string>>(games));

        }

        [HttpGet("help")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<string> GetHelpAsync()
        {
            return Ok("You have landed on the Catan Service Help page!");
        }


    }
}
