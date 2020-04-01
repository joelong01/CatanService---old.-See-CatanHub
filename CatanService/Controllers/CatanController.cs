using CatanService.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Net.Http;

namespace CatanService.Controllers
{
    [ApiController]
    [Route("api/catan")]
    public class CatanController : ControllerBase
    {
        static Dictionary<UserId, UserResources> _usersToResourcesDictionary = new Dictionary<UserId, UserResources>(new UserId()); // given a game, give me a list of users


        private readonly ILogger<CatanController> _logger;

        public CatanController(ILogger<CatanController> logger)
        {
            _logger = logger;
        }

        private UserResources GetUserResources(string gameName, string userName)
        {
            var userId = new UserId
            {
                GameName = gameName.ToLower(),
                UserName = userName.ToLower()
            };
            _usersToResourcesDictionary.TryGetValue(userId, out UserResources resources);
            return resources;
        }

        [HttpPost("game/register/{gameName}/{userName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<string> RegisteAsync(string gameName, string userName)
        {
            var userId = new UserId
            {
                GameName = gameName.ToLower(),
                UserName = userName.ToLower()
            };
            bool ret = _usersToResourcesDictionary.TryGetValue(userId, out UserResources resources);
            if (!ret)
            {
                resources = new UserResources();
                _usersToResourcesDictionary[userId] = resources;
            }


            return Ok($"{userName} is playing in game {gameName}");

        }
        [HttpDelete("game/delete/{gameName}")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<string> DeleteAsync(string gameName)
        {
            foreach (var kvp in _usersToResourcesDictionary)
            {
                if (kvp.Key.GameName == gameName.ToLower())
                {
                    _usersToResourcesDictionary.Remove(kvp.Key);
                }
            }

            return Ok(gameName);


        }

        [HttpGet("game/users/{gameName}")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<string>> GetUsersAsync(string gameName)
        {
            List<string> users = new List<string>();
            foreach (var kvp in _usersToResourcesDictionary)
            {
                if (kvp.Key.GameName.ToLower() == gameName)
                {
                    users.Add(kvp.Key.UserName);
                }
            }
            return Ok(users);
        }

        [HttpGet("games")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<string> GetGamesAsync()
        {
            return Ok(JsonConvert.SerializeObject(_usersToResourcesDictionary));
        }

        [HttpGet("game/help")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<string> GetHelpAsync()
        {
            return Ok("You have landed on the Catan Service Help page!");
        }


        [HttpGet("cards/{gameName}/{userName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<string> GetCards(string gameName, string userName)
        {
            var userId = new UserId
            {
                GameName = gameName.ToLower(),
                UserName = userName.ToLower()
            };
            bool ret = _usersToResourcesDictionary.TryGetValue(userId, out UserResources resources);
            if (!ret)
            {
                return NotFound($"User {userName} in game {gameName} not found");
            }

            return Ok(JsonConvert.SerializeObject(resources));
        }

        [HttpPost("cards/add/{gameName}/{userName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<string> AddAsync([FromBody] ResourceCountClass resourceCount, string gameName, string userName)
        {

            var userResources = GetUserResources(gameName, userName);
            if (userResources == null)
            {
                return NotFound($"{userName} in game { gameName} not found");
            }
            if (resourceCount.Brick + userResources.ResourceCards[ResourceType.Brick] < 0)
            {
                return BadRequest($"{userName} in game { gameName} is trying to use more Brick than they have. Request is {resourceCount.Brick} have {userResources.ResourceCards[ResourceType.Brick]}");
            }
            if (resourceCount.Wood + userResources.ResourceCards[ResourceType.Wood] < 0)
            {
                return BadRequest($"{userName} in game { gameName} is trying to use more Wood than they have. Request is {resourceCount.Wood} have {userResources.ResourceCards[ResourceType.Wood]}");
            }
            if (resourceCount.Sheep + userResources.ResourceCards[ResourceType.Sheep] < 0)
            {
                return BadRequest($"{userName} in game { gameName} is trying to use more Sheep than they have. Request is {resourceCount.Sheep} have {userResources.ResourceCards[ResourceType.Sheep]}");
            }
            if (resourceCount.Wheat + userResources.ResourceCards[ResourceType.Wheat] < 0)
            {
                return BadRequest($"{userName} in game { gameName} is trying to use more Wheat than they have. Request is {resourceCount.Wheat} have {userResources.ResourceCards[ResourceType.Wheat]}");
            }
            if (resourceCount.Ore + userResources.ResourceCards[ResourceType.Ore] < 0)
            {
                return BadRequest($"{userName} in game { gameName} is trying to use more Ore than they have. Request is {resourceCount.Ore} have {userResources.ResourceCards[ResourceType.Ore]}");
            }
            if (resourceCount.GoldMine + userResources.ResourceCards[ResourceType.GoldMine] < 0)
            {
                return BadRequest($"{userName} in game { gameName} is trying to use more GoldMine than they have. Request is {resourceCount.GoldMine} have {userResources.ResourceCards[ResourceType.GoldMine]}");
            }

            AddResources(userResources.ResourceCards, resourceCount);
            return Ok(JsonConvert.SerializeObject(_usersToResourcesDictionary));

        }

        private void AddResources(Dictionary<ResourceType, int> dict, ResourceCountClass resourceCount)
        {
            dict[ResourceType.Brick] += resourceCount.Brick;
            dict[ResourceType.Wood] += resourceCount.Wood;
            dict[ResourceType.Wheat] += resourceCount.Wheat;
            dict[ResourceType.Ore] += resourceCount.Ore;
            dict[ResourceType.Sheep] += resourceCount.Sheep;
            dict[ResourceType.GoldMine] += resourceCount.GoldMine;
        }

        [HttpPost("cards/tradegold/{gameName}/{userName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<string> TradeGoldAsync([FromBody] ResourceCountClass resourceCount, string gameName, string userName)
        {
            var userResources = GetUserResources(gameName, userName);
            if (userResources == null)
            {
                return NotFound($"{userName} in game { gameName} not found");
            }

            int askCount = resourceCount.Brick + resourceCount.Wood + resourceCount.Wheat + resourceCount.Ore + resourceCount.Sheep;
            if (askCount != -1 * resourceCount.GoldMine)
            {
                return BadRequest($"[Player={userName}] [Game={gameName}]\n Asking for {askCount} resources, only have {resourceCount.GoldMine} Gold");
            }

            AddResources(userResources.ResourceCards, resourceCount);

            return Ok(JsonConvert.SerializeObject(_usersToResourcesDictionary));

        }

        [HttpPost("cards/trade/{gameName}/{fromName}/{toName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<string> TradeAsync([FromBody] ResourceCountClass[] resourceCount, string gameName, string fromName, string toName)
        {
            var fromResources = GetUserResources(gameName, fromName);
            if (fromResources == null)
            {
                return NotFound($"{fromName} in game { gameName} not found");
            }

            var fromTrade = resourceCount[0];
            var toTrade = resourceCount[1];

            var toResources = GetUserResources(gameName, toName);
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


            return Ok(JsonConvert.SerializeObject(_usersToResourcesDictionary));
        }

        [HttpPost("cards/take/{gameName}/{fromName}/{toName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<string> TakeAsync(string gameName, string fromName, string toName)
        {
            var fromResources = GetUserResources(gameName, fromName);
            if (fromResources == null)
            {
                return NotFound($"{fromName} in game { gameName} not found");
            }

            var toResources = GetUserResources(gameName, toName);
            if (toResources == null)
            {
                return NotFound($"{toName} in game { gameName} not found");
            }

            if (fromResources.TotalResources == 0)
            {
                return Ok(JsonConvert.SerializeObject(_usersToResourcesDictionary));
            }

            Random rand = new Random((int)DateTime.Now.Ticks);
            int index = rand.Next(0, fromResources.TotalResources);

            if (index < fromResources.ResourceCards[ResourceType.Wheat])
            {
                fromResources.ResourceCards[ResourceType.Wheat]--; // take a wheat
                toResources.ResourceCards[ResourceType.Wheat]++;  //add it
                return Ok(JsonConvert.SerializeObject(_usersToResourcesDictionary));
            }
            else
            {
                index -= fromResources.ResourceCards[ResourceType.Wheat];
            }

            if (index < fromResources.ResourceCards[ResourceType.Wood])
            {
                fromResources.ResourceCards[ResourceType.Wood]--; // take a Wood
                toResources.ResourceCards[ResourceType.Wood]++;  //add it
                return Ok(JsonConvert.SerializeObject(_usersToResourcesDictionary));
            }
            else
            {
                index -= fromResources.ResourceCards[ResourceType.Wood];
            }

            if (index < fromResources.ResourceCards[ResourceType.Brick])
            {
                fromResources.ResourceCards[ResourceType.Brick]--; // take a Brick
                toResources.ResourceCards[ResourceType.Brick]++;  //add it
                return Ok(JsonConvert.SerializeObject(_usersToResourcesDictionary));
            }
            else
            {
                index -= fromResources.ResourceCards[ResourceType.Brick];
            }

            if (index < fromResources.ResourceCards[ResourceType.Sheep])
            {
                fromResources.ResourceCards[ResourceType.Sheep]--; // take a Sheep
                toResources.ResourceCards[ResourceType.Sheep]++;  //add it
                return Ok(JsonConvert.SerializeObject(_usersToResourcesDictionary));
            }
            else
            {
                index -= fromResources.ResourceCards[ResourceType.Sheep];
            }


            fromResources.ResourceCards[ResourceType.Ore]--; // take a Ore
            toResources.ResourceCards[ResourceType.Ore]++;  //add it
            return Ok(JsonConvert.SerializeObject(_usersToResourcesDictionary));


        }

        [HttpPost("cards/meritimetrade/{gameName}/{userName}/{resourceToTrade}/{count}/{resourceToGet}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<string> TakeAsync(string gameName, string userName, ResourceType resourceToTrade, int count, ResourceType resourceToGet)
        {
            var userResources = GetUserResources(gameName, userName);
            if (userResources == null)
            {
                return NotFound($"{userName} in game { gameName} not found");
            }

            // make sure they have enough to trade

            if (userResources.ResourceCards[resourceToTrade] < count)
            {
                return BadRequest($"[Player={userName}] [Game={gameName}]\n needs {count} of {resourceToTrade} only has {userResources.ResourceCards[resourceToTrade]} ");
            }

            userResources.ResourceCards[resourceToTrade] -= count;
            userResources.ResourceCards[resourceToGet] += 1;

            return Ok(JsonConvert.SerializeObject(_usersToResourcesDictionary));
        }
    }
}
