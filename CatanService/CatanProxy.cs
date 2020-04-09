using CatanSharedModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Catan.Proxy
{
    public class ProxyResult<T>
    {
        public T Result { get; set; }
        public string RawJson { get; set; }
        public int ErrorCode { get; set; }

    }

    public class CatanProxy : IDisposable
    {


        public HttpClient Client { get; set; } = new HttpClient();

        public string HostName { get; set; } // "http://localhost:50919";
        public CatanResult LastError { get; set; } = null;
        public string LastErrorString { get; set; } = "";
        public CatanProxy()
        {
            Client.Timeout = TimeSpan.FromHours(5);
        }
        public Task<PlayerResources> Register(GameInfo info, string gameName, string playerName)
        {

            if (String.IsNullOrEmpty(gameName) || String.IsNullOrEmpty(playerName))
            {
                throw new Exception("names can't be null or empty");
            }
            string url = $"{HostName}/api/catan/game/register/{gameName}/{playerName}";

            return Post<PlayerResources>(url, CatanSerializer.Serialize<GameInfo>(info));

        }
        public Task<List<string>> GetGames()
        {
            string url = $"{HostName}/api/catan/game";

            return Get<List<string>>(url);

        }
        public Task<List<string>> GetUsers(string game)
        {
            if (String.IsNullOrEmpty(game))
            {
                throw new Exception("names can't be null or empty");
            }

            string url = $"{HostName}/api/catan/game/users/{game}";

            return Get<List<string>>(url);

        }
        public Task<GameInfo> GetGameInfo(string game)
        {
            if (String.IsNullOrEmpty(game))
            {
                throw new Exception("names can't be null or empty");
            }

            string url = $"{HostName}/api/catan/game/gameInfo/{game}";

            return Get<GameInfo>(url);

        }
        public Task<CatanResult> DeleteGame(string gameName)
        {

            if (String.IsNullOrEmpty(gameName))
            {
                throw new Exception("names can't be null or empty");
            }
            string url = $"{HostName}/api/catan/game/{gameName}";

            return Delete<CatanResult>(url);


        }

        public Task StartGame(string game)
        {
            if (String.IsNullOrEmpty(game))
            {
                throw new Exception("names can't be null or empty");
            }
            string url = $"{HostName}/api/catan/game/start/{game}";
            return Post<string>(url, null);
        }

        public async Task<List<ServiceLogRecord>> Monitor(string game, string player)
        {
            if (String.IsNullOrEmpty(game))
            {
                throw new Exception("names can't be null or empty");
            }
            string url = $"{HostName}/api/catan/monitor/{game}/{player}";
            string json = await Get<string>(url);
            var options = new JsonDocumentOptions()
            {
                AllowTrailingCommas = true
            };

            //  this.TraceMessage($"{json}");
            List<ServiceLogRecord> logList = new List<ServiceLogRecord>();
            using (JsonDocument document = JsonDocument.Parse(Encoding.UTF8.GetBytes(json), options))
            {

                foreach (JsonElement element in document.RootElement.EnumerateArray())
                {
                    //  this.TraceMessage($"{element}");
                    ServiceLogRecord logEntry = CatanSerializer.Deserialize<ServiceLogRecord>(element.GetRawText());
                    Debug.WriteLine($"Log Received.  [ID={logEntry.LogId}] [Player={logEntry.PlayerName}]");
                    switch (logEntry.Action)
                    {
                        case ServiceAction.Undefined:
                            break;
                        case ServiceAction.Purchased:
                            PurchaseLog purchaseLog = CatanSerializer.Deserialize<PurchaseLog>(element.GetRawText());
                            logList.Add(purchaseLog);
                            break;
                        case ServiceAction.PlayerAdded:
                            GameLog gameLog = CatanSerializer.Deserialize<GameLog>(element.GetRawText());
                            logList.Add(gameLog);
                            break;
                        case ServiceAction.UserRemoved:
                            break;
                        case ServiceAction.GameCreated:
                            break;
                        case ServiceAction.GameDeleted:
                            break;
                        case ServiceAction.TradeGold:
                            break;
                        case ServiceAction.GrantResources:
                            ResourceLog resourLog = CatanSerializer.Deserialize<ResourceLog>(element.GetRawText());
                            logList.Add(resourLog);
                            break;
                        case ServiceAction.TradeResources:
                            break;
                        case ServiceAction.TakeCard:
                            break;
                        case ServiceAction.Refund:
                            break;
                        case ServiceAction.MeritimeTrade:
                            break;
                        case ServiceAction.UpdatedTurn:
                            break;
                        case ServiceAction.LostToMonopoly:
                            break;
                        case ServiceAction.PlayedMonopoly:
                            break;
                        case ServiceAction.PlayedRoadBuilding:
                            break;
                        case ServiceAction.PlayedKnight:
                            break;
                        case ServiceAction.PlayedYearOfPlenty:
                            break;
                        default:
                            break;
                    }
                }


            }
            Debug.WriteLine($"[Game={game}] [Player={player}] [LogCount={logList.Count}]");
            return logList;
        }

        //_ = await client.PostAsync($"{_hostName}/api/catan/resource/grant/{GameName}/{player.PlayerResources.PlayerName}", new StringContent(CatanSerializer.Serialize<PlayerResources>(resources), Encoding.UTF8, "application/json"));

        public Task<PlayerResources> GrantResources(string game, string player, TradeResources resources)
        {
            if (String.IsNullOrEmpty(game) || String.IsNullOrEmpty(player))
            {
                throw new Exception("names can't be null or empty");
            }
            string url = $"{HostName}/api/catan/resource/grant/{game}/{player}";
            var body = CatanSerializer.Serialize<TradeResources>(resources);
            return Post<PlayerResources>(url, body);
        }

        public Task<List<PlayerResources>> Trade(string game, string fromPlayer, TradeResources from, string toPlayer, TradeResources to)
        {
            if (String.IsNullOrEmpty(fromPlayer) || String.IsNullOrEmpty(toPlayer) || String.IsNullOrEmpty(game))
            {
                throw new Exception("names can't be null or empty");
            }
            string url = $"{HostName}/api/catan/resource/trade/{game}/{fromPlayer}/{toPlayer}";
            var body = CatanSerializer.Serialize<TradeResources[]>(new TradeResources[] { from, to });
            return Post<List<PlayerResources>>(url, body);
        }


        private async Task<T> Get<T>(string url)
        {


            if (String.IsNullOrEmpty(url))
            {
                throw new Exception("the URL can't be null or empty");
            }



            LastError = null;
            LastErrorString = "";
            string json = "";
            try
            {

                json = await Client.GetStringAsync(url);
                if (typeof(T) == typeof(string))
                {
                    T workaround = (T)(object)json;
                    return workaround;
                }
                T obj = CatanSerializer.Deserialize<T>(json);
                return obj;


            }
            catch (HttpRequestException)
            {
                // see if there is a Catan Exception

                LastErrorString = json;
                try
                {
                    LastError = CatanSerializer.Deserialize<CatanResult>(json);
                }
                catch
                {
                    return default;
                }

            }
            catch (Exception e)
            {
                LastErrorString = json + e.ToString();
                return default;
            }
            return default;
        }

        private async Task<T> Delete<T>(string url)
        {

            if (String.IsNullOrEmpty(url))
            {
                throw new Exception("the URL can't be null or empty");
            }



            LastError = null;
            LastErrorString = "";

            try
            {

                var response = await Client.DeleteAsync(url);
                var json = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    T obj = CatanSerializer.Deserialize<T>(json);
                    return obj;
                }
                else
                {
                    LastErrorString = await response.Content.ReadAsStringAsync();
                    try
                    {
                        LastError = CatanSerializer.Deserialize<CatanResult>(LastErrorString);
                        return default;
                    }
                    catch
                    {
                        return default;
                    }

                }
            }
            catch (Exception e)
            {
                // see if there is a Catan Exception
                LastErrorString = e.ToString();
                return default;
            }
        }

        private async Task<T> Post<T>(string url, string body)
        {

            if (String.IsNullOrEmpty(url))
            {
                throw new Exception("the URL can't be null or empty");
            }



            LastError = null;
            LastErrorString = "";

            try
            {
                HttpResponseMessage response;
                if (body != null)
                {
                    response = await Client.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
                }
                else
                {
                    response = await Client.PostAsync(url, new StringContent("", Encoding.UTF8, "application/json"));
                }

                string json = await response.Content.ReadAsStringAsync();
                if (typeof(T) == typeof(string))
                {
                    T workaround = (T)(object)json;
                    return workaround;
                }
                if (response.IsSuccessStatusCode)
                {
                    T obj = CatanSerializer.Deserialize<T>(json);
                    return obj;
                }
                else
                {
                    LastErrorString = await response.Content.ReadAsStringAsync();
                    try
                    {
                        LastError = CatanSerializer.Deserialize<CatanResult>(LastErrorString);
                        return default;
                    }
                    catch
                    {
                        return default;
                    }

                }
            }
            catch (Exception e)
            {
                // see if there is a Catan Exception
                LastErrorString = e.ToString();
                return default;
            }
        }

        public void Dispose()
        {
            Client.Dispose();
        }
    }
}
