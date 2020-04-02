using CatanService.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace CatanService.Controllers
{
    [ApiController]
    [Route("api/catan")]
    public class CatanController : ControllerBase
    {

        private ReaderWriterLockSlim cacheLock = new ReaderWriterLockSlim();
        public static Dictionary<PlayerId, PlayerResources> PlayersToResourcesDictionary { get; } = new Dictionary<PlayerId, PlayerResources>(new PlayerId()); // given a game, give me a list of users

        public static TaskCompletionSource<object> ResourceMonitorTcs { get; set; }
        // public static TaskCompletionSource<object> DevCardMonitorTcs { get; set; } = new TaskCompletionSource<object>();

        private readonly ILogger<CatanController> _logger;

        public CatanController(ILogger<CatanController> logger)
        {
            _logger = logger;
        }

        private PlayerResources SafeGetPlayerResources(string gameName, string playerName)
        {
            var playerId = new PlayerId
            {
                GameName = gameName.ToLower(),
                PlayerName = playerName.ToLower()
            };

            SafeGetPlayerResources(playerId, out PlayerResources resources);
            return resources; // can be null

        }
        private bool SafeGetPlayerResources(string gameName, string playerName, out PlayerResources resources)
        {
            var playerId = new PlayerId
            {
                GameName = gameName.ToLower(),
                PlayerName = playerName.ToLower()
            };

            return SafeGetPlayerResources(playerId, out resources);
        }

        private bool SafeGetPlayerResources(PlayerId playerId, out PlayerResources resources)
        {

            cacheLock.EnterReadLock();
            try
            {
                return PlayersToResourcesDictionary.TryGetValue(playerId, out resources);

            }
            finally
            {
                cacheLock.ExitReadLock();
            }
        }
        private void SafeSetPlayerResources(string gameName, string playerName, PlayerResources resources)
        {
            var playerId = new PlayerId
            {
                GameName = gameName.ToLower(),
                PlayerName = playerName.ToLower()
            };

            SafeSetPlayerResources(playerId, resources);
        }
        private void SafeSetPlayerResources(PlayerId playerId, PlayerResources resources)
        {
            cacheLock.EnterWriteLock();
            try
            {
                PlayersToResourcesDictionary.Add(playerId, resources);
            }
            finally
            {
                cacheLock.ExitWriteLock();
            }
        }

        [HttpPost("game/register/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<string> RegisterAsync(string gameName, string playerName)
        {
            bool ret = SafeGetPlayerResources(gameName, playerName, out _);
            if (!ret)
            {
                PlayerResources resources = new PlayerResources()
                {
                    PlayerName = playerName,
                    GameName = gameName
                };

                SafeSetPlayerResources(gameName, playerName, resources);
            }

            return Ok($"{playerName} is playing in game {gameName}");

        }



        [HttpDelete("game/delete/{gameName}")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<string> DeleteAsync(string gameName)
        {
            cacheLock.EnterWriteLock();
            try
            {
                foreach (var kvp in PlayersToResourcesDictionary)
                {
                    if (kvp.Key.GameName == gameName.ToLower())
                    {
                        PlayersToResourcesDictionary.Remove(kvp.Key);
                    }
                }

                return Ok(gameName);
            }
            finally
            {
                cacheLock.ExitWriteLock();
            }

        }

        [HttpGet("game/users/{gameName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<string>> GetUsersAsync(string gameName)
        {
            cacheLock.EnterReadLock();
            try
            {
                List<string> users = new List<string>();
                foreach (var kvp in PlayersToResourcesDictionary)
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
                cacheLock.ExitReadLock();
            }
        }

        [HttpGet("games")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<string> GetGamesAsync()
        {
            cacheLock.EnterReadLock();
            try
            {
                return Ok(JsonConvert.SerializeObject(PlayersToResourcesDictionary));
            }
            finally
            {
                cacheLock.ExitReadLock();
            }
            
        }

        [HttpGet("game/help")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<string> GetHelpAsync()
        {
            return Ok("You have landed on the Catan Service Help page!");
        }


        [HttpGet("cards/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<string> GetCards(string gameName, string playerName)
        {
            bool ret = SafeGetPlayerResources(gameName, playerName, out PlayerResources resources);
            
            if (!ret)
            {
                return NotFound($"User {playerName} in game {gameName} not found");
            }

            return Ok(JsonConvert.SerializeObject(resources));
        }

        [HttpGet("game/monitor/resources/{gameName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<string>> MonitorAsync(string gameName)
        {
            if (ResourceMonitorTcs.Task.IsCompleted)
            {
                ResourceMonitorTcs = new TaskCompletionSource<object>();
            }
            await ResourceMonitorTcs.Task;

            return Ok("TODO");
        }

        [HttpGet("cards/async/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<string>> GetCardsAsync(string gameName, string playerName)
        {
            bool ret = SafeGetPlayerResources(gameName, playerName, out PlayerResources resources);
           
            if (!ret)
            {
                return NotFound($"User {playerName} in game {gameName} not found");
            }
            try
            {
                resources.TCS = new TaskCompletionSource<object>();
                await resources.TCS.Task; // this can hang for a long, long time
            }
            catch (Exception e)
            {
                return Ok($"Exception thrown {e}");
            }

            return Ok(JsonConvert.SerializeObject(resources));
        }

        [HttpPost("cards/add/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<string> AddAsync([FromBody] ResourceCountClass resourceCount, string gameName, string playerName)
        {
            bool ret = SafeGetPlayerResources(gameName, playerName, out PlayerResources playerResources);
            
            if (!ret)
            {
                return NotFound($"{playerName} in game { gameName} not found");
            }
            if (resourceCount.Brick + playerResources.ResourceCards[ResourceType.Brick] < 0)
            {
                return BadRequest($"{playerName} in game { gameName} is trying to use more Brick than they have. Request is {resourceCount.Brick} have {playerResources.ResourceCards[ResourceType.Brick]}");
            }
            if (resourceCount.Wood + playerResources.ResourceCards[ResourceType.Wood] < 0)
            {
                return BadRequest($"{playerName} in game { gameName} is trying to use more Wood than they have. Request is {resourceCount.Wood} have {playerResources.ResourceCards[ResourceType.Wood]}");
            }
            if (resourceCount.Sheep + playerResources.ResourceCards[ResourceType.Sheep] < 0)
            {
                return BadRequest($"{playerName} in game { gameName} is trying to use more Sheep than they have. Request is {resourceCount.Sheep} have {playerResources.ResourceCards[ResourceType.Sheep]}");
            }
            if (resourceCount.Wheat + playerResources.ResourceCards[ResourceType.Wheat] < 0)
            {
                return BadRequest($"{playerName} in game { gameName} is trying to use more Wheat than they have. Request is {resourceCount.Wheat} have {playerResources.ResourceCards[ResourceType.Wheat]}");
            }
            if (resourceCount.Ore + playerResources.ResourceCards[ResourceType.Ore] < 0)
            {
                return BadRequest($"{playerName} in game { gameName} is trying to use more Ore than they have. Request is {resourceCount.Ore} have {playerResources.ResourceCards[ResourceType.Ore]}");
            }
            if (resourceCount.GoldMine + playerResources.ResourceCards[ResourceType.GoldMine] < 0)
            {
                return BadRequest($"{playerName} in game { gameName} is trying to use more GoldMine than they have. Request is {resourceCount.GoldMine} have {playerResources.ResourceCards[ResourceType.GoldMine]}");
            }

            AddResources(playerResources.ResourceCards, resourceCount);
            ReleaseHangingGet(playerResources.TCS);
            
            return Ok(JsonConvert.SerializeObject(PlayersToResourcesDictionary));

        }

        private void ReleaseHangingGet(TaskCompletionSource<object> tcs)
        {
            if (tcs != null)
            {
                tcs.TrySetResult(null); // hanging gets will run
            }
            if (ResourceMonitorTcs != null)
            {
                ReleaseHangingGet(ResourceMonitorTcs);
            }
        }

        private void AddResources(Dictionary<ResourceType, int> dict, ResourceCountClass resourceCount)
        {
            //
            //  there is a race condition where the game is allocating resources and the player
            //  clicks to buy something too quickly since the service doesn't keep track of game state
            cacheLock.EnterWriteLock();
            try
            {
                dict[ResourceType.Brick] += resourceCount.Brick;
                dict[ResourceType.Wood] += resourceCount.Wood;
                dict[ResourceType.Wheat] += resourceCount.Wheat;
                dict[ResourceType.Ore] += resourceCount.Ore;
                dict[ResourceType.Sheep] += resourceCount.Sheep;
                dict[ResourceType.GoldMine] += resourceCount.GoldMine;
            }
            finally
            {
                cacheLock.ExitWriteLock();
            }
        }

        [HttpPost("cards/tradegold/{gameName}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<string> TradeGoldAsync([FromBody] ResourceCountClass resourceCount, string gameName, string playerName)
        {
            var playerResources = SafeGetPlayerResources(gameName, playerName);
            if (playerResources == null)
            {
                return NotFound($"{playerName} in game { gameName} not found");
            }

            if (resourceCount.GoldMine <= 0)
            {
                return BadRequest($"[Player={playerName}] [Game={gameName}]\n in tradegold, the #gold should be positiver instead of {resourceCount.GoldMine}");
            }

            int askCount = resourceCount.Brick + resourceCount.Wood + resourceCount.Wheat + resourceCount.Ore + resourceCount.Sheep;
            if (askCount != -1 * resourceCount.GoldMine)
            {
                return BadRequest($"[Player={playerName}] [Game={gameName}]\n Asking for {askCount} resources, only have {resourceCount.GoldMine} Gold");
            }

            AddResources(playerResources.ResourceCards, resourceCount);
            ReleaseHangingGet(playerResources.TCS);
            return Ok(JsonConvert.SerializeObject(PlayersToResourcesDictionary));

        }

        [HttpPost("cards/trade/{gameName}/{fromName}/{toName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<string> TradeAsync([FromBody] ResourceCountClass[] resourceCount, string gameName, string fromName, string toName)
        {
            var fromResources = SafeGetPlayerResources(gameName, fromName);
            if (fromResources == null)
            {
                return NotFound($"{fromName} in game { gameName} not found");
            }

            var fromTrade = resourceCount[0];
            var toTrade = resourceCount[1];

            var toResources = SafeGetPlayerResources(gameName, toName);
            if (toResources == null)
            {
                return NotFound($"{toName} in game { gameName} not found");
            }
            //
            //  validate that from has the cards needed
            if (fromResources.ResourceCards[ResourceType.Brick] < fromTrade.Brick)
            {
                return BadRequest($"[Player={fromName}] [Game={gameName}]\n Asking for {fromTrade.Brick} Brick, only has {fromResources.ResourceCards[ResourceType.Brick]}");
            }
            if (fromResources.ResourceCards[ResourceType.Wood] < fromTrade.Wood)
            {
                return BadRequest($"[Player={fromName}] [Game={gameName}]\n Asking for {fromTrade.Wood} Wood, only has {fromResources.ResourceCards[ResourceType.Wood]}");
            }
            if (fromResources.ResourceCards[ResourceType.Sheep] < fromTrade.Sheep)
            {
                return BadRequest($"[Player={fromName}] [Game={gameName}]\n Asking for {fromTrade.Sheep} Sheep, only has {fromResources.ResourceCards[ResourceType.Sheep]}");
            }
            if (fromResources.ResourceCards[ResourceType.Ore] < fromTrade.Ore)
            {
                return BadRequest($"[Player={fromName}] [Game={gameName}]\n Asking for {fromTrade.Ore} Ore, only has {fromResources.ResourceCards[ResourceType.Ore]}");
            }
            if (fromResources.ResourceCards[ResourceType.Wheat] < fromTrade.Wheat)
            {
                return BadRequest($"[Player={fromName}] [Game={gameName}]\n Asking for {fromTrade.Wheat} Wheat, only has {fromResources.ResourceCards[ResourceType.Wheat]}");
            }

            // validate that To has the cards needed
            if (toResources.ResourceCards[ResourceType.Brick] < toTrade.Brick)
            {
                return BadRequest($"[Player={toName}] [Game={gameName}]\n Asking for {toTrade.Brick} Brick, only has {toResources.ResourceCards[ResourceType.Brick]}");
            }
            if (toResources.ResourceCards[ResourceType.Wood] < toTrade.Wood)
            {
                return BadRequest($"[Player={toName}] [Game={gameName}]\n Asking for {toTrade.Wood} Wood, only has {toResources.ResourceCards[ResourceType.Wood]}");
            }
            if (toResources.ResourceCards[ResourceType.Sheep] < toTrade.Sheep)
            {
                return BadRequest($"[Player={toName}] [Game={gameName}]\n Asking for {toTrade.Sheep} Sheep, only has {toResources.ResourceCards[ResourceType.Sheep]}");
            }
            if (toResources.ResourceCards[ResourceType.Ore] < toTrade.Ore)
            {
                return BadRequest($"[Player={toName}] [Game={gameName}]\n Asking for {toTrade.Ore} Ore, only has {toResources.ResourceCards[ResourceType.Ore]}");
            }
            if (toResources.ResourceCards[ResourceType.Wheat] < toTrade.Wheat)
            {
                return BadRequest($"[Player={toName}] [Game={gameName}]\n Asking for {toTrade.Wheat} Wheat, only has {toResources.ResourceCards[ResourceType.Wheat]}");
            }

            // give to to 
            AddResources(toResources.ResourceCards, fromTrade);
            // give to from
            AddResources(fromResources.ResourceCards, toTrade);

            // negate
            toTrade.Negate();
            fromTrade.Negate();

            //subract the resources
            AddResources(fromResources.ResourceCards, fromTrade);
            AddResources(toResources.ResourceCards, toTrade);
            ReleaseHangingGet(fromResources.TCS);
            ReleaseHangingGet(toResources.TCS);

            return Ok(JsonConvert.SerializeObject(PlayersToResourcesDictionary));
        }

        [HttpPost("cards/take/{gameName}/{fromName}/{toName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<string> TakeAsync(string gameName, string fromName, string toName)
        {
            var fromResources = SafeGetPlayerResources(gameName, fromName);
            if (fromResources == null)
            {
                return NotFound($"{fromName} in game { gameName} not found");
            }

            var toResources = SafeGetPlayerResources(gameName, toName);
            if (toResources == null)
            {
                return NotFound($"{toName} in game { gameName} not found");
            }

            if (fromResources.TotalResources == 0)
            {
                return Ok(JsonConvert.SerializeObject(PlayersToResourcesDictionary));
            }

            Random rand = new Random((int)DateTime.Now.Ticks);
            int index = rand.Next(0, fromResources.TotalResources);

            if (index < fromResources.ResourceCards[ResourceType.Wheat])
            {
                fromResources.ResourceCards[ResourceType.Wheat]--; // take a wheat
                toResources.ResourceCards[ResourceType.Wheat]++;  //add it
                return Ok(JsonConvert.SerializeObject(PlayersToResourcesDictionary));
            }
            else
            {
                index -= fromResources.ResourceCards[ResourceType.Wheat];
            }

            if (index < fromResources.ResourceCards[ResourceType.Wood])
            {
                fromResources.ResourceCards[ResourceType.Wood]--; // take a Wood
                toResources.ResourceCards[ResourceType.Wood]++;  //add it
                return Ok(JsonConvert.SerializeObject(PlayersToResourcesDictionary));
            }
            else
            {
                index -= fromResources.ResourceCards[ResourceType.Wood];
            }

            if (index < fromResources.ResourceCards[ResourceType.Brick])
            {
                fromResources.ResourceCards[ResourceType.Brick]--; // take a Brick
                toResources.ResourceCards[ResourceType.Brick]++;  //add it
                return Ok(JsonConvert.SerializeObject(PlayersToResourcesDictionary));
            }
            else
            {
                index -= fromResources.ResourceCards[ResourceType.Brick];
            }

            if (index < fromResources.ResourceCards[ResourceType.Sheep])
            {
                fromResources.ResourceCards[ResourceType.Sheep]--; // take a Sheep
                toResources.ResourceCards[ResourceType.Sheep]++;  //add it
                return Ok(JsonConvert.SerializeObject(PlayersToResourcesDictionary));
            }
            else
            {
                index -= fromResources.ResourceCards[ResourceType.Sheep];
            }


            fromResources.ResourceCards[ResourceType.Ore]--; // take a Ore
            toResources.ResourceCards[ResourceType.Ore]++;  //add it
            ReleaseHangingGet(fromResources.TCS);
            ReleaseHangingGet(toResources.TCS);
            return Ok(JsonConvert.SerializeObject(PlayersToResourcesDictionary));


        }

        [HttpPost("cards/meritimetrade/{gameName}/{playerName}/{resourceToGive}/{count}/{resourceToGet}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<string> TakeAsync(string gameName, string playerName, ResourceType resourceToGive, int count, ResourceType resourceToGet)
        {
            var playerResources = SafeGetPlayerResources(gameName, playerName);
            if (playerResources == null)
            {
                return NotFound($"{playerName} in game { gameName} not found");
            }

            // make sure they have enough to trade

            if (playerResources.ResourceCards[resourceToGive] < count)
            {
                return BadRequest($"[Player={playerName}] [Game={gameName}]\n needs {count} of {resourceToGive} only has {playerResources.ResourceCards[resourceToGive]} ");
            }

            playerResources.ResourceCards[resourceToGive] -= count;
            playerResources.ResourceCards[resourceToGet] += 1;
            ReleaseHangingGet(playerResources.TCS);

            return Ok(JsonConvert.SerializeObject(PlayersToResourcesDictionary));
        }
    }
}
