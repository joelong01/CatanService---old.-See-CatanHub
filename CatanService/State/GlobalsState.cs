﻿using CatanService.State;
using Catan.Proxy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

namespace CatanService
{



    /// <summary>
    ///     this contains all the state assosiated with a particular player. Note that you have 1 player per client
    ///     so you should have one of these per client.  in theory only one thead at a time should be accessing this
    ///     class, but that just makes the locks cheeper.  i've made them all thread safe in case downstream requirements
    ///     make me need thread safety.
    /// </summary>
  

    /// <summary>
    ///     This class contains the state for the service.  
    ///     
    ///     TSGlobal
    ///              Games
    ///                 Game1
    ///                 Game2
    ///                 Game3
    ///                     Players
    ///                         Player1
    ///                         Player2
    ///                         Player3
    /// </summary>
    public static class TSGlobal
    {
        public static Games Games { get; } = new Games();
        public static Game GetGame(string gameName) { return Games.TSGetGame(gameName); }

        public static (Game, PlayerResources) GetGameAndPlayerInfo(string gameName, string playerName)
        {
            Game game = TSGlobal.GetGame(gameName);
            PlayerResources resources = null;
            if (game != null)
            {
                resources = game.GetPlayer(playerName);
            }

            return (game, resources);

        }

        public static void DumpToConsole()
        {

            StringBuilder sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss"));
            sb.Append(Environment.NewLine);
            sb.Append("-------------------");
            sb.Append(Environment.NewLine);

            foreach (var gameName in TSGlobal.Games.TSGetGameNames())
            {
                Game game = TSGlobal.GetGame(gameName);
                string json = CatanProxy.Serialize(game, true);
                sb.Append($"{gameName}:");
                sb.Append(Environment.NewLine);
                sb.Append(json);
                //var players = GetGame(gameName).TSGetPlayers();
                //foreach (var player in players)
                //{
                //    sb.Append("\t\t");
                //    sb.Append(player.PlayerName);
                //    sb.Append(":\t");
                //    sb.Append(player);
                //    sb.Append(Environment.NewLine);
                //}
            }
            Console.Clear();
            Console.WriteLine(sb.ToString());

        }

    }


}

