using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Catan.Proxy;
using CatanService.State;
using Microsoft.CodeAnalysis.CSharp;
using System.Threading;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Diagnostics.Contracts;

namespace CatanService.Controllers
{
    /// <summary>
    ///      Title: Catan Start Protocol
    ///   _: **1. Start**
    ///   A<--> Service: MonitorAllGames (optional)
    ///   B <--> Service: MonitorAllGames (optional)
    ///   A -> Service: Games.CreateGame
    ///   Service -> Service: Game.CopyLog
    ///   Service -> A: GameCreated (check createdBy)
    ///   Service -> B: GameCreated (createdBy, AutoJoinRequested)
    ///   B -> B: CreateGame
    ///   A -> A: CreateGame
    ///   A --> Service : MyGame.Monitor
    ///   B --> Service : MyGame.Monitor
    ///   A --> Service: Game.Monitor
    ///   B --> Service: Game.Monitor
    ///   A -> Service: Game.AddPlayer(playerA)
    ///   B -> Service: Game.AddPlayer(playerB)
    ///   Service --> A: GameMonitor: AddPlayerA
    ///   A -> A: (AddPlayerA)
    ///   Service --> B: GameMonitor: AddPlayerA
    ///   B -> B: (AddPlayerA)
    ///   Service --> A: GameMonitor: AddPlayerB
    ///   A -> A: (AddPlayerB)
    ///   Service --> B: GameMonitor: AddPlayerB
    ///   B -> B: (AddPlayerB)
    ///
    ///      https://swimlanes.io/u/YK78n-O6l
    ///
    /// </summary>

    [Route("api/catan/Game")]
    [ApiController]
    public class GameController : ControllerBase
    {
        public static Games Games { get; } = new Games();

        /// <summary>
        ///     Create a new game by the player in the URL
        /// </summary>
        /// <param name="gameInfo"></param>
        /// <param name="gameName"></param>
        /// <param name="playerName"></param>
        /// <returns></returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult CreateGame([FromBody] GameInfo gameInfo)
        {
            try
            {
                Game game = Games.GetGame(gameInfo.Id);
                if (game != default)
                {
                    var err = new CatanResult(CatanError.BadParameter)
                    {
                        CantanRequest = new CatanRequest() { Url = this.Request.Path },
                        Description = $" Game '{gameInfo.Id}' with description '{gameInfo.Name}' created by '{gameInfo.Creator}' already exists.  You can join it or delete it",
                    };
                    CatanMessage message = new CatanMessage() { Data = err, Sequence = 0, DataTypeName = typeof(CatanResult).FullName };
                    return BadRequest(message);
                }
                Games.AddGame(gameInfo.Id, new Game() { GameInfo = gameInfo });

                return GetGames();
            }
            catch (Exception e)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path },
                    Description = $"{this.Request.Path} threw an exception. {e}",
                };

                CatanMessage message = new CatanMessage() { Data = err, Sequence = 0, DataTypeName = typeof(CatanResult).FullName };
                return BadRequest(message);
            }
        }

        [HttpDelete("{gameId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult DeleteGame(string gameId)
        {
            try
            {
                bool success = Games.DeleteGame(gameId, out Game game);
                if (!success)
                {
                    var err = new CatanResult(CatanError.BadParameter)
                    {
                        CantanRequest = new CatanRequest() { Url = this.Request.Path },
                        Description = $" Game '{gameId}' does not exist",
                    };

                    CatanMessage message = new CatanMessage() { Data = err, Sequence = 0, DataTypeName = typeof(CatanResult).FullName };
                    return NotFound(message);
                }

                return GetGames();
            }
            catch (Exception e)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path },
                    Description = $"{this.Request.Path} threw an exception. {e}",
                };

                CatanMessage message = new CatanMessage() { Data = err, Sequence = 0, DataTypeName = typeof(CatanResult).FullName };
                return BadRequest(message);
            }
        }

        [HttpGet("{gameId}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetGameLogRecords(string gameId, string playerName)
        {
            Game game = Games.GetGame(gameId);

            if (game == default)
            {
                var err = new CatanResult(CatanError.NoGameWithThatName) { Description = $"Game '{gameId}' does not exist", Request = this.Request.Path };
                CatanMessage errMessage = new CatanMessage() { Data = err, Sequence = 0, DataTypeName = typeof(CatanResult).FullName };
                return NotFound(errMessage);
            }
            bool success = game.NameToPlayerDictionary.TryGetValue(playerName, out Player player);
            if (!success)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path },
                    Description = $"Player '{playerName}' is not a member of Game '{gameId}'.",
                };
                CatanMessage errMessage = new CatanMessage() { Data = err, Sequence = 0, DataTypeName = typeof(CatanResult).FullName };
                return NotFound(errMessage);
            }

            return Ok(game.GameLog);
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetGames()
        {
            return Ok(Games.GetGames());
        }

        [HttpGet("help")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetHelp()
        {
            var res = new CatanResult(CatanError.BadParameter)
            {
                CantanRequest = new CatanRequest() { Url = this.Request.Path },
                Description = $"Version=1.10",
            };
            return Ok(res);
        }

        [HttpGet("players/{gameId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetPlayersInGame(string gameId)
        {
            Game game = Games.GetGame(gameId);
            if (game == default)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path },
                    Description = $"Game '{gameId}' Not Found",
                };

                return NotFound(err);
            }

            return Ok(game.NameToPlayerDictionary.Keys);
        }

        [HttpPost("join/{gameId}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult JoinGame(string gameId, string playerName)
        {
            try
            {
                Game game = Games.GetGame(gameId);

                if (game == default)

                {
                    var err = new CatanResult(CatanError.BadParameter)
                    {
                        CantanRequest = new CatanRequest() { Url = this.Request.Path },
                        Description = $" Game '{gameId}' already exists.  You can join it or delete it",
                    };

                    CatanMessage message = new CatanMessage() { Data = err, Sequence = 0, DataTypeName = typeof(CatanResult).FullName };
                    return BadRequest(message);
                }

                //
                // try to add a player, passing in the game log
                //
                bool success = game.NameToPlayerDictionary.TryAdd(playerName, new Player(game.GameLog));
                if (!success)
                {
                    var err = new CatanResult(CatanError.BadParameter)
                    {
                        CantanRequest = new CatanRequest() { Url = this.Request.Path },
                        Description = $"Player '{playerName}' is already a member of Game '{gameId}'.",
                    };

                    CatanMessage message = new CatanMessage() { Data = err, Sequence = 0, DataTypeName = typeof(CatanResult).FullName };
                    return BadRequest(message);
                }

                return Ok(game.GameInfo); // the client is going to want to know the creator
            }
            catch (Exception e)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path },
                    Description = $"{this.Request.Path} threw an exception. {e}",
                };

                CatanMessage message = new CatanMessage() { Data = err, Sequence = 0, DataTypeName = typeof(CatanResult).FullName };
                return BadRequest(message);
            }
        }

        [HttpGet("monitor/{gameId}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Monitor(string gameId, string playerName)
        {
            Game game = Games.GetGame(gameId);

            if (game == default)
            {
                var err = new CatanResult(CatanError.NoGameWithThatName) { Description = $"Game '{gameId}' does not exist", Request = this.Request.Path };
                CatanMessage errMessage = new CatanMessage() { Data = err, Sequence = 0, DataTypeName = typeof(CatanResult).FullName };
                return NotFound(errMessage);
            }
            bool success = game.NameToPlayerDictionary.TryGetValue(playerName, out Player player);
            if (!success)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path },
                    Description = $"Player '{playerName}' is already a member of Game '{gameId}'.",
                };
                CatanMessage errMessage = new CatanMessage() { Data = err, Sequence = 0, DataTypeName = typeof(CatanResult).FullName };
                return NotFound(errMessage);
            }

            var messages = await player.WaitForLogEntries();

            if (messages == null || messages.Count == 0)
            {
                var err = new CatanResult(CatanError.Unexpected) { Request = this.Request.Path, Description = $"Why did {playerName} release with no log entries?" };
                CatanMessage errMessage = new CatanMessage() { Data = err, Sequence = 0, DataTypeName = typeof(CatanResult).FullName };
                return BadRequest(errMessage);
            }

            return Ok(messages);
        }

        [HttpPost("message/{gameId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult PostMessage([FromBody] CatanMessage message, string gameId)
        {
            try
            {
                Game game = Games.GetGame(gameId);

                if (game == default)
                {
                    var err = new CatanResult(CatanError.BadParameter)
                    {
                        CantanRequest = new CatanRequest() { Url = this.Request.Path },
                        Description = $"Game '{gameId}' does not exists",
                    };

                    CatanMessage errMessage = new CatanMessage() { Data = err, Sequence = 0, DataTypeName = typeof(CatanResult).FullName };
                    return NotFound(errMessage);
                }

                game.PostLog(message);
                game.ReleaseLogs();
                return Ok(message);
            }
            catch (Exception e)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path },
                    Description = $"{this.Request.Path} threw an exception. {e}",
                };
                CatanMessage errMessage = new CatanMessage() { Data = err, Sequence = 0, DataTypeName = typeof(CatanResult).FullName };
                return BadRequest(errMessage);
            }
        }

        /// <summary>
        ///     "Starts" the game -- you cannot join a game that has been started
        /// </summary>
        /// <param name="gameInfo"></param>
        /// <param name="gameName"></param>
        /// <param name="playerName"></param>
        /// <returns></returns>
        [HttpPost("start")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult StartGame([FromBody] GameInfo gameInfo)
        {
            try
            {
                Game game = Games.GetGame(gameInfo.Id);

                if (game == default)
                {
                    var err = new CatanResult(CatanError.BadParameter)
                    {
                        CantanRequest = new CatanRequest() { Url = this.Request.Path },
                        Description = $" Game '{gameInfo.Id}' with description '{gameInfo.Name}' created by '{gameInfo.Creator}' already exists.  You can join it or delete it",
                    };
                    CatanMessage message = new CatanMessage() { Data = err, Sequence = 0, DataTypeName = typeof(CatanResult).FullName };
                    return BadRequest(message);
                }

                game.Started = true;

                return GetGames();
            }
            catch (Exception e)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path },
                    Description = $"{this.Request.Path} threw an exception. {e}",
                };

                CatanMessage message = new CatanMessage() { Data = err, Sequence = 0, DataTypeName = typeof(CatanResult).FullName };
                return BadRequest(message);
            }
        }
    }
}
