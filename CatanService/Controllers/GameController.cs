using CatanService.State;
using CatanSharedModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;


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
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult Register([FromBody] GameInfo gameInfo, string gameName, string playerName)
        {
            var game = TSGlobal.Games.TSFindOrCreateGame(gameName, gameInfo);
            if (game.GameInfo != gameInfo)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path, Body = gameInfo, BodyType = BodyType.GameInfo },
                    Description = $"{playerName} in Game '{gameName}' attempted to register a game with different gameInfo.",                                        
                };
                err.ExtendedInformation.Add(new KeyValuePair<string, object>("ExistingGameInfo", game));
                return BadRequest(err);
            
            }

            PlayerState clientState = game.GetPlayer(playerName);
            if (clientState != null)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path, Body = gameInfo, BodyType = BodyType.GameInfo },
                    Description = $"{playerName} in Game '{gameName}' is already registered.",
                  
                };
                err.ExtendedInformation.Add(new KeyValuePair<string, object>("ExistingGameInfo", clientState));
                return BadRequest(err);
            }

            if (game.Started)
            {
                var err = new CatanResult(CatanError.GameAlreadStarted)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path, Body = gameInfo, BodyType = BodyType.GameInfo },
                    Description = $"{playerName} attempting to join '{gameName}' that has already started",                    
                };
                err.ExtendedInformation.Add(new KeyValuePair<string, object>("ExistingGameInfo", clientState));
                return BadRequest(err);
            }

            clientState = new PlayerState()
            {
                PlayerName = playerName,
                GameName = gameName,
                ResourcesLeft = new GameInfo(game.GameInfo) 
            };

            game.TSSetPlayerResources( playerName, clientState);
           
            //
            // do not add a Log record -- the client gets the list of players when one of them calls Start


            return Ok(clientState);

        }
        [HttpPost("start/{gameName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult Start(string gameName)
        {
            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                var err = new CatanResult(CatanError.NoGameWithThatName)
                {
                    Description = $"Game '{gameName}' does not exist",
                    Request = this.Request.Path
                };

                return NotFound(err);
            }
            game.Started = true;
            game.TSAddLogRecord(new GameLog() { Players = game.Players, PlayerName = "", Action = ServiceAction.GameStarted, RequestUrl = this.Request.Path });

            game.TSReleaseMonitors();
            return Ok();
        }

        [HttpPost("turn/{gameName}/{oldPlayer}/{newPlayer}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult Turn(string gameName, string oldPlayer, string newPlayer)
        {
            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                return NotFound(new CatanResult(CatanError.NoGameWithThatName) { Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
            }
            if (game.GetPlayer(oldPlayer) == null)
            {
                return NotFound(new CatanResult(CatanError.NoPlayerWithThatName) { Request = this.Request.Path, Description = $"{oldPlayer} in game {gameName} not found" });

            }            
            if (game.GetPlayer(newPlayer) == null)
            {
                return NotFound(new CatanResult(CatanError.NoPlayerWithThatName) { Request = this.Request.Path, Description = $"{oldPlayer} in game {gameName} not found" });

            }
            game.TSAddLogRecord(new TurnLog() { NewPlayer = newPlayer, PlayerName = oldPlayer, RequestUrl = this.Request.Path });
            game.TSReleaseMonitors();
            return Ok();
        }

        [HttpPost("turnorder/{gameName}/{oldPlayer}/{newPlayer}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult TurnOrder(string gameName, List<string> players)
        {
            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                return NotFound(new CatanResult(CatanError.NoGameWithThatName) { Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
            }

            game.TSSetPlayerOrder(players);
           
            game.TSReleaseMonitors();
            return Ok();
        }


        [HttpDelete("{gameName}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult DeleteAsync(string gameName)
        {
            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                return NotFound(new CatanResult(CatanError.NoGameWithThatName) { Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
            }
                       
            TSGlobal.Games.TSDeleteGame(gameName);

            game.TSAddLogRecord(new GameLog() { Players = game.Players, Action = ServiceAction.GameDeleted, RequestUrl = this.Request.Path });
            game.TSReleaseMonitors();
            return Ok(new CatanResult(CatanError.NoError)
            {
                Request = this.Request.Path, Description = $"{gameName} deleted"
            });
        }



        [HttpGet("users/{gameName}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetUsers(string gameName)
        {
            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                return NotFound(new CatanResult(CatanError.NoGameWithThatName) { Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
            }
            
            return Ok(game.Players);
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetGames()
        {
            var games = TSGlobal.Games.TSGetGames();
            return Ok(games);

        }
        [HttpGet("gameInfo/{gameName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetGameInfo(string gameName)
        {

            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                return NotFound(new CatanResult(CatanError.NoGameWithThatName) { Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
            }

            
            return Ok(game.GameInfo);

        }

        [HttpGet("help")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetHelp()
        {
            return Ok(new CatanResult(CatanError.NoError) { Request = this.Request.Path, Description = "You have landed on the Catan Service Help page!" });
        }


    }
}
