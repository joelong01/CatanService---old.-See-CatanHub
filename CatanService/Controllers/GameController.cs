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
            bool ret = TSGlobal.PlayerState.TSGetPlayerResources(gameName, playerName, out ClientState resources);
            if (!ret)
            {
                resources = new ClientState()
                {
                    PlayerName = playerName,
                    GameName = gameName
                };

                TSGlobal.PlayerState.TSSetPlayerResources(gameName, playerName, resources);
                TSGlobal.PlayerState.TSAddLogEntry(new GameLog() { Players = TSGlobal.PlayerState.TSGetPlayers(gameName), PlayerName = playerName, Action = ServiceAction.PlayerAdded });

            }
            
            TSGlobal.PlayerState.TSReleaseMonitors(gameName); // this will cause the client monitoring changes to get a list of players from the GameUpdateLog
            return Ok(resources.TSSerialize());

        }

        [HttpPost("turn/{gameName}/{oldPlayer}/{newPlayer}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<string> Turn(string gameName, string oldPlayer, string newPlayer)
        {
            bool ret = TSGlobal.PlayerState.TSGetPlayerResources(gameName, oldPlayer, out ClientState _);
            if (!ret)
            {
                return NotFound($"{oldPlayer} in game {gameName} not found");

            }
            ret = TSGlobal.PlayerState.TSGetPlayerResources(gameName, newPlayer, out ClientState _);
            if (!ret)
            {
                return NotFound($"{oldPlayer} in game {gameName} not found");

            }
            TSGlobal.PlayerState.TSAddLogEntry(new TurnLog() { NewPlayer = newPlayer, PlayerName = oldPlayer });

            TSGlobal.PlayerState.TSReleaseMonitors(gameName); 
            return Ok();
        }


        [HttpDelete("delete/{gameName}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<string> DeleteAsync(string gameName)
        {
            var ret = TSGlobal.PlayerState.TSDeleteGame(gameName);
            if (!ret)
            {
                return NotFound($"{gameName} not found");


            }
            TSGlobal.PlayerState.TSAddLogEntry(new GameLog() { Players = TSGlobal.PlayerState.TSGetPlayers(gameName), Action = ServiceAction.GameDeleted });
            TSGlobal.PlayerState.TSReleaseMonitors(gameName); // this will cause the client monitoring changes to get a list of players from the GameUpdateLog
            return Ok($"{gameName} deleted");
        }



        [HttpGet("users/{gameName}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<string> GetUsersAsync(string gameName)
        {
            var players = TSGlobal.PlayerState.TSGetPlayers(gameName);
            if (players.Count == 0) return Ok($"No Players in game {gameName}");

            return Ok(TSGlobal.Serialize<List<string>>(players));
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<string> GetGamesAsync()
        {
            var games = TSGlobal.PlayerState.TSGetGames();
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
