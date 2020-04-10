using CatanSharedModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
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
        private CancellationTokenSource _cts = new CancellationTokenSource();
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

            return Post<PlayerResources>(url, CatanProxy.Serialize<GameInfo>(info));

        }

        public Task<PlayerResources> BuyEntitlement(string gameName, string playerName, Entitlement entitlement)
        {
            if(String.IsNullOrEmpty(gameName) || String.IsNullOrEmpty(playerName))
            {
                throw new Exception("names can't be null or empty");
            }
            string url = $"{HostName}/api/catan/purchase/{gameName}/{playerName}/{entitlement}";

            return Post<PlayerResources>(url, null);
        }

        public Task<PlayerResources> GetResources(string game, string player)
        {
            if (String.IsNullOrEmpty(game) || String.IsNullOrEmpty(player))
            {
                throw new Exception("names can't be null or empty");
            }
            string url = $"{HostName}/api/catan/resource/{game}/{player}";
            return Get<PlayerResources>(url);
        }

        public Task<PlayerResources> DevCardPurchase(string game, string player)
        {
            if (String.IsNullOrEmpty(game) || String.IsNullOrEmpty(player))
            {
                throw new Exception("names can't be null or empty");
            }
            string url = $"{HostName}/api/catan/devcard/{game}/{player}";

            return Post<PlayerResources>(url, null);
        }
        public Task<PlayerResources> PlayYearOfPlenty(string gameName, string player, TradeResources tr)
        {

            if (String.IsNullOrEmpty(gameName) || String.IsNullOrEmpty(player))
            {
                throw new Exception("names can't be null or empty");
            }
            string url = $"{HostName}/api/catan/devcard/play/yearofplenty/{gameName}/{player}";

            return Post<PlayerResources>(url, Serialize(tr));
        }
        public Task<PlayerResources> PlayRoadBuilding(string game, string player)
        {
            if (String.IsNullOrEmpty(game) || String.IsNullOrEmpty(player))
            {
                throw new Exception("names can't be null or empty");
            }
            string url = $"{HostName}/api/catan/devcard/play/roadbuilding/{game}/{player}";

            return Post<PlayerResources>(url, null);
        }
        public Task<PlayerResources> PlayMonopoly(string game, string player, ResourceType resourceType)
        {
            if (String.IsNullOrEmpty(game) || String.IsNullOrEmpty(player))
            {
                throw new Exception("names can't be null or empty");
            }
            string url = $"{HostName}/api/catan/devcard/play/monopoly/{game}/{player}/{resourceType}";

            return Post<PlayerResources>(url, null);
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

            ServiceLogCollection serviceLogCollection = CatanProxy.Deserialize<ServiceLogCollection>(json);
            List<ServiceLogRecord> records = new List<ServiceLogRecord>();
            foreach (var rec in serviceLogCollection.LogRecords)
            {
                //
                //  we have to Deserialize to the Header to find out what kind of object we have - this means we double Deserialize the object
                //  we could avoid this by serializing a list of Actions to matach 
                ServiceLogRecord logEntry = CatanProxy.Deserialize<ServiceLogRecord>(rec.ToString());
                switch (logEntry.Action)
                {
                    case ServiceAction.Undefined:
                        break;
                    case ServiceAction.Purchased:

                        break;
                    case ServiceAction.PlayerAdded:
                        GameLog gameLog = CatanProxy.Deserialize<GameLog>(rec.ToString());
                        records.Add(gameLog);
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
                        ResourceLog resourceLog = CatanProxy.Deserialize<ResourceLog>(rec.ToString());
                        records.Add(resourceLog);
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
            //Debug.WriteLine($"[Game={game}] [Player={player}] [LogCount={logList.Count}]");
            return records;
        }

       

        /// <summary>
        ///     Takes resources (Ore, Wheat, etc.) from global pool and assigns to player
        /// </summary>
        /// <param name="game"></param>
        /// <param name="player"></param>
        /// <param name="resources"></param>
        /// <returns></returns>

        public Task<PlayerResources> GrantResources(string game, string player, TradeResources resources)
        {
            if (String.IsNullOrEmpty(game) || String.IsNullOrEmpty(player))
            {
                throw new Exception("names can't be null or empty");
            }
            string url = $"{HostName}/api/catan/resource/grant/{game}/{player}";
            var body = CatanProxy.Serialize<TradeResources>(resources);
            return Post<PlayerResources>(url, body);
        }

        public Task<List<PlayerResources>> Trade(string game, string fromPlayer, TradeResources from, string toPlayer, TradeResources to)
        {
            if (String.IsNullOrEmpty(fromPlayer) || String.IsNullOrEmpty(toPlayer) || String.IsNullOrEmpty(game))
            {
                throw new Exception("names can't be null or empty");
            }
            string url = $"{HostName}/api/catan/resource/trade/{game}/{fromPlayer}/{toPlayer}";
            var body = CatanProxy.Serialize<TradeResources[]>(new TradeResources[] { from, to });
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
                var response = await Client.GetAsync(url, _cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    json = await response.Content.ReadAsStringAsync();

                    if (typeof(T) == typeof(string))
                    {
                        T workaround = (T)(object)json;
                        return workaround;
                    }
                    T obj = CatanProxy.Deserialize<T>(json);
                    return obj;
                }
                else
                {
                    Debug.WriteLine($"Error grom GetAsync: {response} {Environment.NewLine} {response.ReasonPhrase}");
                }


            }
            catch (HttpRequestException)
            {
                // see if there is a Catan Exception

                LastErrorString = json;
                try
                {
                    LastError = CatanProxy.Deserialize<CatanResult>(json);
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

                var response = await Client.DeleteAsync(url, _cts.Token);
                var json = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    T obj = CatanProxy.Deserialize<T>(json);
                    return obj;
                }
                else
                {
                    LastErrorString = await response.Content.ReadAsStringAsync();
                    try
                    {
                        LastError = CatanProxy.Deserialize<CatanResult>(LastErrorString);
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
                    response = await Client.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"), _cts.Token);
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
                    T obj = CatanProxy.Deserialize<T>(json);
                    return obj;
                }
                else
                {
                    LastErrorString = await response.Content.ReadAsStringAsync();
                    try
                    {
                        LastError = CatanProxy.Deserialize<CatanResult>(LastErrorString);
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
        public static JsonSerializerOptions GetJsonOptions(bool indented = false)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = indented

            };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }
        static public string Serialize<T>(T obj, bool indented = false)
        {
            return JsonSerializer.Serialize<T>(obj, GetJsonOptions(indented));
        }
        static public T Deserialize<T>(string json)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return JsonSerializer.Deserialize<T>(json, options);
        }
    }
}
