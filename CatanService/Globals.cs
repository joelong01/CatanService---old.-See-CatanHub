using CatanSharedModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CatanService
{

    public static class Globals
    {

        static public ReaderWriterLockSlim rwLock { get; } = new ReaderWriterLockSlim();
        public static Dictionary<PlayerId, PlayerResources> PlayersToResourcesDictionary { get; } = new Dictionary<PlayerId, PlayerResources>(new PlayerId()); // given a game, give me a list of users

        private static List<PlayerResources> _changedResources = new List<PlayerResources>();
        private static object _logLock = new object();
        public static TaskCompletionSource<object> ChangeLogTCS { get; set; } = null; // assumes only one app monitors everything...

        public static void AddChange(PlayerResources resource)
        {
            lock (_logLock)
            {
                _changedResources.Add(resource);
                //
                //  don't clear this as we can aggregate changes to send back to the client
            }

        }
        public static List<PlayerResources> CopyAndClearChangeLog()
        {
            lock (_logLock)
            {
                var ret = new List<PlayerResources>(_changedResources);
                _changedResources.Clear();
                return ret;
            }
        }

        public static void ReleaseGlobalMonitor()
        {
            if (ChangeLogTCS != null)
            {
                ChangeLogTCS.TrySetResult(null);
            }
        }

        public static bool SafeGetPlayerResources(string gameName, string playerName, out PlayerResources resources)
        {
            var playerId = new PlayerId
            {
                GameName = gameName.ToLower(),
                PlayerName = playerName.ToLower()
            };

            rwLock.EnterReadLock();
            try
            {
                return PlayersToResourcesDictionary.TryGetValue(playerId, out resources);

            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }
        public static void SafeSetPlayerResources(string gameName, string playerName, PlayerResources resources)
        {
            var playerId = new PlayerId
            {
                GameName = gameName.ToLower(),
                PlayerName = playerName.ToLower()
            };

            SafeSetPlayerResources(playerId, resources);
        }
        public static void SafeSetPlayerResources(PlayerId playerId, PlayerResources resources)
        {
            rwLock.EnterWriteLock();
            try
            {
                PlayersToResourcesDictionary.Add(playerId, resources);
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }

        public static string AddPlayersAndSerialize(PlayerResources resources)
        {
            rwLock.EnterReadLock();
            try
            {
                resources.Players.Clear();
                foreach (var kvp in Globals.PlayersToResourcesDictionary)
                {
                    if (resources.GameName.ToLower() == kvp.Key.GameName)
                    {
                        resources.Players.Add(kvp.Key.PlayerName);
                    }
                    
                }

                return PlayerResources.Serialize<PlayerResources>(resources);
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }

        public static void ReleaseHangingGet(TaskCompletionSource<object> tcs)
        {
            if (tcs != null)
            {
                tcs.TrySetResult(null); // hanging gets will run
            }
        }
    }
}

