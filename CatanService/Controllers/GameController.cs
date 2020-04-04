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
        public ActionResult<string> RegisterAsync(string gameName, string playerName)
        {
            bool ret = Globals.SafeGetPlayerResources(gameName, playerName, out PlayerResources resources);
            if (!ret)
            {
                resources = new PlayerResources()
                {
                    PlayerName = playerName,
                    GameName = gameName
                };

                Globals.SafeSetPlayerResources(gameName, playerName, resources);
            }

            return Ok(Globals.AddPlayersAndSerialize(resources));

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
                        if (kvp.Value.ResourceUpdateTCS != null)
                        {
                            Globals.ReleaseHangingGet(kvp.Value.ResourceUpdateTCS);
                        }
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

                return Ok(PlayerResources.Serialize<List<string>>(games));
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
