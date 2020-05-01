using CatanService.State;
using Catan.Proxy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System;

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

        /// <summary>
        ///     Create a new game by the player in the URL
        /// </summary>
        /// <param name="gameInfo"></param>
        /// <param name="gameName"></param>
        /// <param name="playerName"></param>
        /// <returns></returns>
        [HttpPost("create/{gameName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult CreateGame([FromBody] GameInfo gameInfo, string gameName)
        {
            try
            {
                var game = TSGlobal.Games.TSGetGame(gameName);
                if (game != null)
                {
                    var err = new CatanResult(CatanError.BadParameter)
                    {
                        CantanRequest = new CatanRequest() { Url = this.Request.Path, Body = gameInfo, BodyType = BodyType.GameInfo },
                        Description = $" Game '{gameName}' already exists.  You can join it or delete it",
                    };

                    return BadRequest(err);
                }
                game = TSGlobal.Games.TSCreateGame(gameName, gameInfo);
                TSGlobal.DumpToConsole();
                return GetGames();
            }
            catch(Exception e)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path },
                    Description = $"{this.Request.Path} threw an exception. {e}",
                };

                return BadRequest(err);
            }

        }

        /// <summary>
        ///     Create a new game by the player in the URL
        /// </summary>
        /// <param name="gameInfo"></param>
        /// <param name="gameName"></param>
        /// <param name="playerName"></param>
        /// <returns></returns>
        [HttpPost("state/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult SetState([FromBody] LogStateTranstion logStateTransition, string gameName, string playerName)
        {
            try
            {
                var game = TSGlobal.Games.TSGetGame(gameName);
                if (game == null)
                {
                    var err = new CatanResult(CatanError.BadParameter)
                    {
                        CantanRequest = new CatanRequest() { Url = this.Request.Path },
                        Description = $" Game '{gameName}' does not exist.  Create it first",
                    };

                    return BadRequest(err);

                }

                PlayerState clientState = game.GetPlayer(playerName);
                if (clientState == null)
                {
                    var err = new CatanResult(CatanError.BadParameter)
                    {
                        CantanRequest = new CatanRequest() { Url = this.Request.Path },
                        Description = $"{playerName} in Game '{gameName}' is not registered.",

                    };
                    err.ExtendedInformation.Add(new KeyValuePair<string, object>("ExistingGameInfo", clientState));
                    return BadRequest(err);
                }



                //
                //   add a log record so that all clients get players
                var log = new StateChangeLog() { PlayerName = playerName, Action = CatanAction.ChangedState, RequestUrl = this.Request.Path, LogStateTranstion = logStateTransition };
                game.TSAddLogRecord(log);

                game.TSReleaseMonitors();
                TSGlobal.DumpToConsole();
                return Ok(log);
            }
            catch (Exception e)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path },
                    Description = $"{this.Request.Path} threw an exception. {e}",
                };

                return BadRequest(err);
            }

        }

        /// <summary>
        ///     joing an existing game
        ///     return the GameInfo
        /// </summary>
        /// <param name="gameName"></param>
        /// <param name="playerName"></param>
        /// <returns></returns>
        [HttpPost("joingame/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult JoinGame(string gameName, string playerName)
        {
            try
            {
                var game = TSGlobal.Games.TSGetGame(gameName);
                if (game == null)
                {
                    var err = new CatanResult(CatanError.BadParameter)
                    {
                        CantanRequest = new CatanRequest() { Url = this.Request.Path },
                        Description = $" Game '{gameName}' does not exist.  Create it first",
                    };

                    return BadRequest(err);

                }

                PlayerState clientState = game.GetPlayer(playerName);
                if (clientState != null)
                {
                    var err = new CatanResult(CatanError.BadParameter)
                    {
                        CantanRequest = new CatanRequest() { Url = this.Request.Path },
                        Description = $"{playerName} in Game '{gameName}' is already registered.",

                    };
                    err.ExtendedInformation.Add(new KeyValuePair<string, object>("ExistingGameInfo", clientState));
                    return BadRequest(err);
                }

                if (game.Started)
                {
                    var err = new CatanResult(CatanError.GameAlreadStarted)
                    {
                        CantanRequest = new CatanRequest() { Url = this.Request.Path },
                        Description = $"{playerName} attempting to join '{gameName}' that has already started",
                    };
                    err.ExtendedInformation.Add(new KeyValuePair<string, object>("ExistingGameInfo", clientState));
                   // Console.WriteLine($"{playerName} joined GameName={gameName} ");
                    return BadRequest(err);
                }

                clientState = new PlayerState()
                {
                    PlayerName = playerName,
                    GameName = gameName,
                    ResourcesLeft = new GameInfo(game.GameInfo)
                };
                //Console.WriteLine($"{playerName} joined game {gameName}");
                game.TSSetPlayerResources(playerName, clientState);

                //
                //   add a log record so that all clients get players
                var gameLog = new GameLog() { Players = game.Players, PlayerName = playerName, Action = CatanAction.AddPlayer, RequestUrl = this.Request.Path, GameInfo=game.GameInfo };
                game.TSAddLogRecord(gameLog);

                game.TSReleaseMonitors();
                TSGlobal.DumpToConsole();
                return Ok(gameLog);
            }
            catch (Exception e)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path },
                    Description = $"{this.Request.Path} threw an exception. {e}",
                };

                return BadRequest(err);
            }

        }
        [HttpPost("start/{gameName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult Start(string gameName)
        {
            try
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
                var gameLog = new GameLog() { Players = game.Players, PlayerName = "", Action = CatanAction.Started, RequestUrl = this.Request.Path, GameInfo = game.GameInfo };
                game.TSAddLogRecord(gameLog);

                game.TSReleaseMonitors();
                TSGlobal.DumpToConsole();
                return Ok(gameLog);
            }
            catch (Exception e)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path },
                    Description = $"{this.Request.Path} threw an exception. {e}",
                };

                return BadRequest(err);
            }

        }

        [HttpPost("turn/{gameName}/{oldPlayer}/{newPlayer}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult Turn(string gameName, string oldPlayer, string newPlayer)
        {
            try
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
            catch (Exception e)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path },
                    Description = $"{this.Request.Path} threw an exception. {e}",
                };

                return BadRequest(err);
            }
        }

        [HttpPost("turnorder/{gameName}/{oldPlayer}/{newPlayer}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult TurnOrder(string gameName, List<string> players)
        {
            try
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
            catch (Exception e)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path },
                    Description = $"{this.Request.Path} threw an exception. {e}",
                };

                return BadRequest(err);
            }
        }


        [HttpDelete("{gameName}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult DeleteAsync(string gameName)
        {
            try
            {
                var game = TSGlobal.GetGame(gameName);
                if (game == null)
                {
                    return NotFound(new CatanResult(CatanError.NoGameWithThatName) { Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
                }

                TSGlobal.Games.TSDeleteGame(gameName);

                game.TSAddLogRecord(new GameLog() { Players = game.Players, Action = CatanAction.GameDeleted, RequestUrl = this.Request.Path, GameInfo = game.GameInfo });
                game.TSReleaseMonitors();
                //Console.WriteLine($"Deleted game {gameName}");
                TSGlobal.DumpToConsole();
                return Ok(new CatanResult(CatanError.NoError)
                {
                    Request = this.Request.Path,
                    Description = $"{gameName} deleted"
                });
            }
            catch (Exception e)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path },
                    Description = $"{this.Request.Path} threw an exception. {e}",
                };

                return BadRequest(err);
            }
        }

        [HttpDelete("alltestgames")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult DeleteAll()
        {
            try
            {
                // don't modify the collection in the loop over the collection...
                List<string> gamesToRemove = new List<string>();                
                foreach (var game in TSGlobal.Games.TSGetGames())
                {
                    if (game.GameInfo.GameType == GameType.Test)
                    {
                        gamesToRemove.Add(game.Name);
                    }
                }
                foreach (var game in gamesToRemove)
                {
                    TSGlobal.Games.TSDeleteGame(game);
                }
                
                TSGlobal.DumpToConsole();
                return Ok(new CatanResult(CatanError.NoError)
                {
                    Request = this.Request.Path,
                    Description = $"this games deleted: {CatanProxy.Serialize(gamesToRemove)}"
                });
            }
            catch (Exception e)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path },
                    Description = $"{this.Request.Path} threw an exception. {e}",
                };

                return BadRequest(err);
            }
        }

        [HttpGet("users/{gameName}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetUsers(string gameName)
        {
            try
            {
                var game = TSGlobal.GetGame(gameName);
                if (game == null)
                {
                    return NotFound(new CatanResult(CatanError.NoGameWithThatName) { Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
                }

                return Ok(game.Players);
            }
            catch (Exception e)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path },
                    Description = $"{this.Request.Path} threw an exception. {e}",
                };

                return BadRequest(err);
            }
        }

        [HttpGet("gamedata/{gameName}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetGameData(string gameName)
        {
            try
            {
                var game = TSGlobal.GetGame(gameName);
                if (game == null)
                {
                    return NotFound(new CatanResult(CatanError.NoGameWithThatName) { Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
                }

                return Ok(CatanProxy.Serialize(game, true));
            }
            catch (Exception e)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path },
                    Description = $"{this.Request.Path} threw an exception. {e}",
                };

                return BadRequest(err);
            }
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetGames()
        {
            try
            {
                var games = TSGlobal.Games.TSGetGameNames();
                return Ok(games);
            }
            catch (Exception e)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path },
                    Description = $"{this.Request.Path} threw an exception. {e}",
                };

                return BadRequest(err);
            }

        }
        [HttpGet("gameInfo/{gameName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetGameInfo(string gameName)
        {
            try
            {
                var game = TSGlobal.GetGame(gameName);
                if (game == null)
                {
                    return NotFound(new CatanResult(CatanError.NoGameWithThatName) { Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
                }
                return Ok(game.GameInfo);
            }
            catch (Exception e)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path },
                    Description = $"{this.Request.Path} threw an exception. {e}",
                };

                return BadRequest(err);
            }


            

        }
        /// <summary>
        ///     a client (playerName) wants to post new board (boardLog) for everybody in gameName to look at
        /// </summary>
        /// <param name="gameName"></param>
        /// <param name="playerName"></param>
        /// <returns></returns>
        [HttpPost("board/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult PostBoard([FromBody] RandomBoardLog logEntry, string gameName, string playerName)
        {

            var game = TSGlobal.GetGame(gameName);
            if (game == null)
            {
                return NotFound(new CatanResult(CatanError.NoGameWithThatName) { CantanRequest = new CatanRequest() { Url = this.Request.Path, Body = null, BodyType = BodyType.None }, Description = $"Game '{gameName}' does not exist", Request = this.Request.Path });
            }
            var resources = game.GetPlayer(playerName);
            if (resources == null)
            {
                return NotFound(new CatanResult(CatanError.NoPlayerWithThatName) { CantanRequest = new CatanRequest() { Url = this.Request.Path, Body = null, BodyType = BodyType.None }, Description = $"{playerName} in game '{gameName}' not found" });

            }
            if (logEntry == null)
            {
                return BadRequest(new CatanResult(CatanError.BadParameter) { CantanRequest = new CatanRequest() { Url = this.Request.Path, Body = null, BodyType = BodyType.None }, Description = $"RandomBoardSetting cannot be null" });
            }

            game.GameInfo.BoardSettings = CatanProxy.Deserialize<RandomBoardSettings>(logEntry.RandomBoardSettings.ToString());

           
            game.TSAddLogRecord(logEntry);
            game.TSReleaseMonitors();
            return Ok(logEntry);

        }

        [HttpGet("help")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetHelp()
        {
           return Ok(new CatanResult(CatanError.NoError) { Request = Request.Path, Description = $"You have landed on the Catan Service Help page!" });
        }


    }
}
