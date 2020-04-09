using CatanService.State;
using CatanSharedModels;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

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
    }


}

