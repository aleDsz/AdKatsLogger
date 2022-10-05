using PRoConEvents;
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

        public static void SetupServer(AdKatsLogger plugin, int gameID = 1, int serverID = 1, int serverGroup = 0)
        {
            var game = new Dictionary<string, object>();

            game.Add("GameID", gameID);
            game.Add("Name", "BF4");

            plugin.__InsertData__("tbl_games", game);

            var server = new Dictionary<string, object>();

            server.Add("ServerID", serverID);
            server.Add("ServerGroup", serverGroup);
            server.Add("IP_Address", "127.0.0.1:47400");
            server.Add("ServerName", "Sample server");
            server.Add("GameID", gameID);
            server.Add("usedSlots", 0);
            server.Add("maxSlots", 64);
            server.Add("mapName", "MP_Prison");
            server.Add("Gamemode", "TeamDeathMatch0");
            server.Add("ConnectionState", "on");

            plugin.__InsertData__("tbl_server", server);
        }

        public static void InsertPlayer(
            AdKatsLogger plugin,
            string soldierName = "Foo",
            int gameID = 1,
            int serverID = 1,
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
            var playerData = new Dictionary<string, object>();

            playerData.Add("GameID", gameID);
            playerData.Add("ClanTag", null);
            playerData.Add("SoldierName", soldierName);
            playerData.Add("GlobalRank", globalRank);
            playerData.Add("PBGUID", pbGuid);
            playerData.Add("EAGUID", eaGuid);
            playerData.Add("IP_Address", "127.0.0.1");

            plugin.__InsertData__("tbl_playerdata", playerData);
            var playerID = plugin.__GetPlayerIDFromPlayer__(soldierName, gameID);

            var serverPlayer = new Dictionary<string, object>();

            serverPlayer.Add("ServerID", serverID);
            serverPlayer.Add("PlayerID", playerID);

            plugin.__InsertData__("tbl_server_player", serverPlayer);
            var statsID = plugin.__GetStatsIDFromPlayerID__(serverID, playerID);

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
    }
}
