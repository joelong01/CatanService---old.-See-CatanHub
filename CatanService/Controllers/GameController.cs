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
using Microsoft.Azure.SignalR.Protocol;

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

        [HttpDelete("{id}/{by}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult DeleteGame(Guid id, string by)
        {
            try
            {
                bool success = Games.DeleteGame(id, out Game game);
                if (!success)
                {
                    var err = new CatanResult(CatanError.BadParameter)
                    {
                        CantanRequest = new CatanRequest() { Url = this.Request.Path },
                        Description = $" Game '{id}' does not exist",
                    };

                    return NotFound(err);
                }
                CatanServiceMessage msg = new CatanServiceMessage() { GameInfo = game.GameInfo, PlayerName = by };

                CatanMessage message = new CatanMessage()
                {
                    ActionType = ActionType.Normal,
                    Data = (object)msg,
                    DataTypeName = typeof(GameInfo).FullName,
                    From = by,
                    Sequence = game.GetNextSequenceNumber(),
                    MessageType = MessageType.DeleteGame
                };
                game.PostLog(message);
                game.ReleaseLogs();

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

        [HttpGet("{id}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetGameLogRecords(Guid id, string playerName)
        {

            Game game = Games.GetGame(id);

            if (game == default)
            {
                var err = new CatanResult(CatanError.NoGameWithThatName) { Description = $"Game '{id}' does not exist", Request = this.Request.Path };
                CatanMessage errMessage = new CatanMessage() { Data = err, Sequence = 0, DataTypeName = typeof(CatanResult).FullName };
                return NotFound(errMessage);
            }
            bool success = game.NameToPlayerDictionary.TryGetValue(playerName, out Player player);
            if (!success)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path },
                    Description = $"Player '{playerName}' is not a member of Game with id = '{id}'.",
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
                Description = $"Version=2.1",
            };
            return Ok(res);
        }

        [HttpGet("players/{gameId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetPlayersInGame(Guid gameId)
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

        [HttpPost("join/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult JoinGame([FromBody] GameInfo gameInfo, string playerName)
        {
            try
            {

                Game game = Games.GetGame(gameInfo.Id);

                if (game == null)

                {
                    var err = new CatanResult(CatanError.BadParameter)
                    {
                        CantanRequest = new CatanRequest() { Url = this.Request.Path },
                        Description = $" Game '{gameInfo.Name}' with id={gameInfo.Id} does not exist.  Call Monitor() before calling Join()",
                    };

                    CatanMessage message = new CatanMessage() { Data = err, Sequence = 0, DataTypeName = typeof(CatanResult).FullName };
                    return BadRequest(message);
                }

                //
                //  should already be in here since you shoudl have called Monitor()
                bool success = game.NameToPlayerDictionary.TryGetValue(playerName, out Player player);


                if (!success)
                {
                    var err = new CatanResult(CatanError.BadParameter)
                    {
                        CantanRequest = new CatanRequest() { Url = this.Request.Path },
                        Description = $"Player '{playerName}' not found for game  '{gameInfo.Name}'. Did you call Monitor() before Join()?",
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



        [HttpGet("monitor/{id}/{gameName}/{playerName}/{requestAutoJoin}/{delete}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Monitor(Guid id, string gameName, string playerName, bool requestAutoJoin, bool delete)
        {
            Game game = null;
            bool success;
            List<CatanMessage> messages = new List<CatanMessage>();
            if (game != null && game.GameInfo.Creator == playerName && delete)
            {
                success = Games.DeleteGame(id, out game);
                if (success)
                {
                    CatanServiceMessage msg = new CatanServiceMessage() { GameInfo = game.GameInfo, PlayerName = playerName };

                    CatanMessage message = new CatanMessage()
                    {
                        ActionType = ActionType.Normal,
                        Data = (object)msg,
                        DataTypeName = typeof(GameInfo).FullName,
                        From = playerName,
                        Sequence = game.GetNextSequenceNumber(),
                        MessageType = MessageType.DeleteGame
                    };

                    messages.Add(message);
                }
            }
            else
            {
                game = Games.GetGame(id);
            }
            
            if (game == null)
            {
                GameInfo gameInfo = new GameInfo()
                {
                    Id = id,
                    Name = gameName,
                    Creator = playerName,
                    RequestAutoJoin = requestAutoJoin,
                    Started = false
                };


                game = new Game() { GameInfo = gameInfo };

                Games.AddGame(gameInfo.Id, game);

                CatanServiceMessage msg = new CatanServiceMessage() { GameInfo = gameInfo, PlayerName = playerName };

                CatanMessage message = new CatanMessage()
                {
                    ActionType = ActionType.Normal,
                    Data = (object)msg,
                    DataTypeName = typeof(GameInfo).FullName,
                    From = playerName,
                    Sequence = game.GetNextSequenceNumber(),
                    MessageType = MessageType.CreateGame
                };

                messages.Add(message);

            }

            success = game.NameToPlayerDictionary.TryGetValue(playerName, out Player player);
            if (!success)
            {
                // add the player

                game.NameToPlayerDictionary.TryAdd(playerName, new Player(game.GameLog));
                //
                //  return the player Joined message
                CatanServiceMessage msg = new CatanServiceMessage() { GameInfo = game.GameInfo, PlayerName = playerName };

                CatanMessage message = new CatanMessage()
                {
                    ActionType = ActionType.Normal,
                    Data = (object)msg,
                    DataTypeName = typeof(GameInfo).FullName,
                    From = playerName,
                    Sequence = game.GetNextSequenceNumber(),
                    MessageType = MessageType.JoinGame
                };

                messages.Add(message);
            }
            //
            //  this will contain the boot strap messages the first time we are called
            if (messages.Count > 0)
            {
                return Ok(messages);
            }

            //
            //  if you got here the game has been created and the game has been joined

            messages = await player.WaitForLogEntries();

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
        public IActionResult BroadcastMessage([FromBody] CatanMessage message, Guid gameId)
        {
            try
            {

                Game game = Games.GetGame(gameId);

                if (game == null)
                {
                    var err = new CatanResult(CatanError.BadParameter)
                    {
                        CantanRequest = new CatanRequest() { Url = this.Request.Path },
                        Description = $"Game '{gameId}' does not exists",
                    };

                    CatanMessage errMessage = new CatanMessage() { Data = err, Sequence = 0, DataTypeName = typeof(CatanResult).FullName };
                    return NotFound(errMessage);
                }
                message.MessageType = MessageType.BroadcastMessage;
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

        [HttpPost("privatemessage/{gameId}/{to}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult SendPrivateMessage([FromBody] CatanMessage message, Guid gameId, string to)
        {
            try
            {

                Game game = Games.GetGame(gameId);

                if (game == null)
                {
                    var err = new CatanResult(CatanError.BadParameter)
                    {
                        CantanRequest = new CatanRequest() { Url = this.Request.Path },
                        Description = $"Game '{gameId}' does not exists",
                    };

                    CatanMessage errMessage = new CatanMessage() { Data = err, Sequence = 0, DataTypeName = typeof(CatanResult).FullName };
                    return NotFound(errMessage);
                }


                //
                //  should already be in here since you shoudl have called Monitor()
                bool success = game.NameToPlayerDictionary.TryGetValue(to, out Player player);


                if (!success)
                {
                    var err = new CatanResult(CatanError.BadParameter)
                    {
                        CantanRequest = new CatanRequest() { Url = this.Request.Path },
                        Description = $"Player '{to}' not found for game  '{gameId}'. Did you call Monitor() before Join()?",
                    };

                    return BadRequest(err);
                }
                message.MessageType = MessageType.PrivateMessage;
                player.PlayerLog.Enqueue(message);
                player.ReleaseLog();
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
