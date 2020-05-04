using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Catan.Proxy;
using System.Threading;
using System.Text.Json.Serialization;

namespace CatanService.Controllers
{
   
    public class Player
    {

        [JsonIgnore]
        public ConcurrentQueue<CatanMessage> PlayerLog { get; } = new ConcurrentQueue<CatanMessage>();
        [JsonIgnore]
        public TaskCompletionSource<object> TCS { get; private set; } = null;


        
        /// <summary>
        ///     in a threadsafe way, return the list of all of the log entries since the last time the API was called.
        /// </summary>
        /// <returns></returns>
        public List<CatanMessage> GetLogEntries()
        {
            var list = new List<CatanMessage>();
            while (PlayerLog.IsEmpty == false)
            {
                if (PlayerLog.TryDequeue(out CatanMessage message))
                {
                    list.Add(message);
                }
            }
            return list;
        }

        public async Task<List<CatanMessage>> WaitForLogEntries()
        {
            var list = GetLogEntries();
            if (list.Count != 0)
            {
                return list;
            }
            TCS = new TaskCompletionSource<object>();
            await TCS.Task;
            TCS = null;
            return  GetLogEntries();
            
        }

        internal void ReleaseLog()
        {
            if (TCS != null && !TCS.Task.IsCompleted)
            {
                TCS.SetResult(true);
            }
        }
    }


    public class Session
    {
        
        private int GlobalSequnceNumber = 0;
        public string Description { get; set; } = "";
        /// <summary>
        ///     All the logs for the entire session
        /// </summary>
        [JsonIgnore]
        public ConcurrentQueue<CatanMessage> GameLog { get; } = new ConcurrentQueue<CatanMessage>();
        /// <summary>
        ///     Given a playerName (CASE SENSItiVE), get the PlayerObject
        /// </summary>
        
        public ConcurrentDictionary<string, Player> NameToPlayerDictionary { get; } = new ConcurrentDictionary<string, Player>();
        
        public bool PostLog(CatanMessage message)
        {
            message.Sequence = Interlocked.Increment(ref GlobalSequnceNumber);
            GameLog.Enqueue(message);
            foreach (var player in NameToPlayerDictionary.Values)
            {
                player.PlayerLog.Enqueue(message);
            }
            return true;
        }

        internal void ReleaseLogs()
        {
            foreach (var player in NameToPlayerDictionary.Values)
            {
                player.ReleaseLog();
            }
        }
    }

    
 
    [Route("api/catan/session")]
    [ApiController]
    public class SessionController : ControllerBase
    {
        /// <summary>
        ///     a map of SessionIds (guids as strings) to Session objects
        /// </summary>
        private static ConcurrentDictionary<string, Session> SessionDictionary { get; } = new ConcurrentDictionary<string, Session>();
        

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetSessions()
        {
            return Ok(SessionDictionary.Keys);
        }

        [HttpGet("players/{sessionId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetPlayersInSession(string sessionId)
        {
            bool success = SessionDictionary.TryGetValue(sessionId, out Session session);
            if (!success)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path, BodyType = BodyType.None },
                    Description = $"Session '{sessionId}' Not Found",
                };

                return NotFound(err);
            }

            return Ok(session.NameToPlayerDictionary.Keys);
        }

        /// <summary>
        ///     Create a new game by the player in the URL
        /// </summary>
        /// <param name="gameInfo"></param>
        /// <param name="gameName"></param>
        /// <param name="playerName"></param>
        /// <returns></returns>
        [HttpPost("{sessionId}/{description}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult CreateSession(string sessionId, string description)
        {
            try
            {
                bool success = SessionDictionary.TryGetValue(sessionId, out Session session);
                if (success)
                {
                    var err = new CatanResult(CatanError.BadParameter)
                    {
                        CantanRequest = new CatanRequest() { Url = this.Request.Path, BodyType = BodyType.None },
                        Description = $" Session '{sessionId}' already exists.  You can join it or delete it",
                    };

                    return BadRequest(err);
                }

                SessionDictionary[sessionId] = new Session() { Description = description };
                return Ok(sessionId);
                
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
        [HttpPost("join/{sessionId}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult JoinSession(string sessionId, string playerName)
        {
            try
            {
                bool success = SessionDictionary.TryGetValue(sessionId, out Session session);
                if (!success)

                {
                    var err = new CatanResult(CatanError.BadParameter)
                    {
                        CantanRequest = new CatanRequest() { Url = this.Request.Path, BodyType = BodyType.None },
                        Description = $" Session '{sessionId}' already exists.  You can join it or delete it",
                    };

                    return BadRequest(err);
                }

                success = session.NameToPlayerDictionary.TryGetValue(playerName, out Player player);
                if (success)
                {
                    var err = new CatanResult(CatanError.BadParameter)
                    {
                        CantanRequest = new CatanRequest() { Url = this.Request.Path, BodyType = BodyType.None },
                        Description = $"Player '{playerName}' is already a member of Session '{sessionId}'.",
                    };

                    return BadRequest(err);
                }

                session.NameToPlayerDictionary[playerName] = new Player();




                return Ok(session.NameToPlayerDictionary.Keys); // this will return all the plays in the session when you joing it

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

        [HttpPost("message/{sessionId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult PostMessage([FromBody] CatanMessage message, string sessionId)
        {
            try
            {
                bool success = SessionDictionary.TryGetValue(sessionId, out Session session);
                if (!success)
                {
                    var err = new CatanResult(CatanError.BadParameter)
                    {
                        CantanRequest = new CatanRequest() { Url = this.Request.Path, BodyType = BodyType.None },
                        Description = $"Session '{sessionId}' does not exists",
                    };

                    return NotFound(err);
                }

                session.PostLog(message);
                session.ReleaseLogs();
                return Ok(message); 

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

        [HttpGet("monitor/{sessionId}/{playerName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> MonitorResources(string sessionId, string playerName)
        {
            bool success = SessionDictionary.TryGetValue(sessionId, out Session session);
            if (!success)               
            {
                return NotFound(new CatanResult(CatanError.NoGameWithThatName) { Description = $"Session '{sessionId}' does not exist", Request = this.Request.Path });
            }
            success = session.NameToPlayerDictionary.TryGetValue(playerName, out Player player);
            if (!success)
            {
                var err = new CatanResult(CatanError.BadParameter)
                {
                    CantanRequest = new CatanRequest() { Url = this.Request.Path, BodyType = BodyType.None },
                    Description = $"Player '{playerName}' is already a memore of Session '{sessionId}'.",
                };

                return NotFound(err);
            }


            var messages = await player.WaitForLogEntries();

            if (messages == null || messages.Count == 0)
            {
                return BadRequest(new CatanResult(CatanError.Unexpected) { Request = this.Request.Path, Description = $"Why did {playerName} release with no log entries?" });
            }

            return Ok(messages);

        }

    }
}