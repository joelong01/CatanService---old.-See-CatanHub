using CatanSharedModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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
            bool ret = Globals.SafeGetPlayerResources(gameName, playerName, out _);
            if (!ret)
            {
                PlayerResources resources = new PlayerResources()
                {
                    PlayerName = playerName,
                    GameName = gameName
                };

                Globals.SafeSetPlayerResources(gameName, playerName, resources);
            }

            return Ok($"{playerName} is playing in game {gameName}");

        }




        [HttpDelete("delete/{gameName}")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<string> DeleteAsync(string gameName)
        {
            Globals.rwLock.EnterWriteLock();
            try
            {
                foreach (var kvp in Globals.PlayersToResourcesDictionary)
                {
                    if (kvp.Key.GameName == gameName.ToLower())
                    {
                        Globals.PlayersToResourcesDictionary.Remove(kvp.Key);
                    }
                }

                return Ok(gameName);
            }
            finally
            {
                Globals.rwLock.ExitWriteLock();
            }

        }

        [HttpGet("users/{gameName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<string>> GetUsersAsync(string gameName)
        {
            Globals.rwLock.EnterReadLock();
            try
            {
                List<string> users = new List<string>();
                foreach (var kvp in Globals.PlayersToResourcesDictionary)
                {
                    if (kvp.Key.GameName == gameName.ToLower())
                    {
                        users.Add(kvp.Key.PlayerName);
                    }
                }
                return Ok(users);
            }
            finally
            {
                Globals.rwLock.ExitReadLock();
            }
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<string> GetGamesAsync()
        {
            Globals.rwLock.EnterReadLock();
            try
            {
                List<string> games = new List<string>();
                foreach (var kvp in Globals.PlayersToResourcesDictionary)
                {
                    if (!games.Contains(kvp.Key.GameName))
                    {
                        games.Add(kvp.Key.GameName);
                    }
                    
                }

                return Ok(JsonSerializer.Serialize<List<string>>(games));
            }
            finally
            {
                Globals.rwLock.ExitReadLock();
            }
            
        }

        [HttpGet("help")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<string> GetHelpAsync()
        {
            return Ok("You have landed on the Catan Service Help page!");
        }

            
    }
}
