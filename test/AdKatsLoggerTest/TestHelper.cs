using Microsoft.VisualStudio.TestTools.UnitTesting;

using PRoConEvents;

using System;
using System.Collections;
using System.Collections.Generic;

namespace AdKatsLoggerTest
{
    internal static class TestHelper
    {
        public static void ConfigureDatabase(AdKatsLogger plugin)
        {
            plugin.SetPluginVariable("Host", "localhost");
            plugin.SetPluginVariable("Port", "3306");
            plugin.SetPluginVariable("Username", "adkatslogger");
            plugin.SetPluginVariable("Password", "adkatslogger");
            plugin.SetPluginVariable("Database Name", "adkatslogger");

            plugin.__CleanDatabase__();
        }

        public static void SetupGame(AdKatsLogger plugin, int gameID = 1)
        {
            var game = new Dictionary<string, object>();

            game.Add("GameID", gameID);
            game.Add("Name", "BF4");

            plugin.__InsertData__("tbl_games", game);
        }

        public static void SetupServer(AdKatsLogger plugin, int gameID = 1, string hostName = "127.0.0.1:47400", int serverGroup = 0)
        {
            var server = new Dictionary<string, object>();

            server.Add("ServerGroup", serverGroup);
            server.Add("IP_Address", hostName);
            server.Add("ServerName", "Sample server");
            server.Add("GameID", gameID);
            server.Add("UsedSlots", 0);
            server.Add("MaxSlots", 64);
            server.Add("MapName", "MP_Prison");
            server.Add("GameMode", "TeamDeathMatch0");
            server.Add("ConnectionState", "on");

            var result = plugin.__InsertData__("tbl_server", server);
            if (result != null) plugin.__SetServerID__(Convert.ToInt32(result.Value));
        }

        public static void InsertPlayer(
            AdKatsLogger plugin,
            string soldierName = "Foo",
            int gameID = 1,
            string hostName = "",
            int serverGroup = 0,
            int globalRank = 1,
            string pbGuid = "",
            string eaGuid = "",
            int score = 0,
            int kills = 0,
            int rankScore = 0,
            int rankKills = 0
        )
        {
            var maybeServerID = plugin.__GetServerID__(hostName);
            if (!maybeServerID.HasValue) throw new NullReferenceException("AdKatsLogger._serverID is null");
            var serverID = maybeServerID.Value;

            var playerData = new Dictionary<string, object>();

            playerData.Add("GameID", gameID);
            playerData.Add("ClanTag", null);
            playerData.Add("SoldierName", soldierName);
            playerData.Add("GlobalRank", globalRank);
            playerData.Add("PBGUID", pbGuid);
            playerData.Add("EAGUID", eaGuid);
            playerData.Add("IP_Address", "127.0.0.1");

            var playerID = GetLastID(plugin, "tbl_playerdata", playerData);

            var serverPlayer = new Dictionary<string, object>();

            serverPlayer.Add("ServerID", serverID);
            serverPlayer.Add("PlayerID", playerID);

            var statsID = GetLastID(plugin, "tbl_server_player", serverPlayer);

            var playerStats = new Dictionary<string, object>();

            playerStats.Add("StatsID", statsID);
            playerStats.Add("Score", score);
            playerStats.Add("Kills", kills);

            plugin.__InsertData__("tbl_playerstats", playerStats);

            var playerRank = new Dictionary<string, object>();

            playerRank.Add("PlayerID", statsID);
            playerRank.Add("ServerGroup", serverGroup);
            playerRank.Add("rankScore", rankScore);
            playerRank.Add("rankKills", rankKills);

            plugin.__InsertData__("tbl_playerrank", playerRank);
        }

        private static int GetLastID(AdKatsLogger plugin, string table, Dictionary<string, object> data)
        {
            var maybeID = plugin.__InsertData__(table, data);
            if (!maybeID.HasValue) Assert.Fail($"Expected Auto Increment key to be valid, received null from table {table}");

            return Convert.ToInt32(maybeID.Value);
        }
    }
}
