using Microsoft.VisualStudio.TestTools.UnitTesting;

using PRoCon.Core;

using PRoConEvents;
using System;
using System.Linq;
using System.Threading;

namespace AdKatsLoggerTest
{
    [TestClass]
    public class AdKatsLoggerTest
    {
        [TestMethod("Test if every PRoCon's variable update is working")]
        [TestCategory("IPRoConPluginInterface")]
        public void TestSetPluginVariable()
        {
            var plugin = new AdKatsLogger();
            var pluginVariables = plugin.GetDisplayPluginVariables();

            Func<string, AdKatsLogger, CPluginVariable> queryFunc = (varName, adKatsLogger) =>
            {
                return adKatsLogger.GetPluginVariables()
                                   .Where(p => varName.Contains(p.Name))
                                   .FirstOrDefault();
            };

            foreach (var pluginVariable in pluginVariables)
            {
                var values = new string[] { "FOO", "BAR" };

                if (pluginVariable.Type.Equals("yesno")) values = new string[] { "No", "Yes" };
                else if (pluginVariable.Type.Equals("int")) values = new string[] { "5", "9" };
                else if (pluginVariable.Type.Equals("double")) values = new string[] { "1", "3" };

                foreach (var value in values)
                {
                    plugin.SetPluginVariable(pluginVariable.Name, value);
                    var newPluginVariable = queryFunc(pluginVariable.Name, plugin);

                    Assert.IsNotNull(newPluginVariable, $"Var = {pluginVariable.Name}");
                    Assert.AreEqual(value, newPluginVariable.Value, $"Var = {pluginVariable.Name}");
                }
            }
        }

        [TestMethod("Test if every plugin's variable exists in PRoCons variable")]
        [TestCategory("IPRoConPluginInterface")]
        public void TestGetDisplayPluginVariables()
        {
            var plugin = new AdKatsLogger();
            var pluginVariables = plugin.GetPluginVariables();
            var displayPluginVariables = plugin.GetDisplayPluginVariables();

            Func<string, AdKatsLogger, CPluginVariable> queryFunc = (varName, adKatsLogger) =>
            {
                return adKatsLogger.GetPluginVariables()
                                   .Where(p => p.Name.Contains(varName))
                                   .FirstOrDefault();
            };

            foreach (var pluginVariable in pluginVariables)
            {
                var displayPluginVariable = queryFunc(pluginVariable.Name, plugin);

                Assert.IsNotNull(displayPluginVariable);
                Assert.AreEqual(displayPluginVariable.ReadOnly, pluginVariable.ReadOnly);
                Assert.AreEqual(displayPluginVariable.Value, pluginVariable.Value);
                Assert.AreEqual(displayPluginVariable.Type, pluginVariable.Type);
            }
        }

        [TestMethod("Test if when player joins, create new session and add into stats tracker with ranking by score")]
        [TestCategory("PRoConPluginAPI")]
        public void TestOnPlayerJoinRankingByScore()
        {
            var plugin = new AdKatsLogger();
            TestHelper.ConfigureDatabase(plugin);
            TestHelper.SetupGame(plugin);
            TestHelper.SetupServer(plugin);

            plugin.SetPluginVariable("Spam Protection|Enabled?", "Yes");
            plugin.SetPluginVariable("Stats Logging|Enabled?", "Yes");
            plugin.SetPluginVariable("Enable in-game commands?", "Yes");
            plugin.SetPluginVariable("Enable Ranking by score?", "Yes");

            var players = new string[] { Faker.Internet.UserName(), Faker.Internet.UserName() };
            var ranks = new int[] { 1, 2 };

            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                var expectedRank = ranks[i];

                TestHelper.InsertPlayer(plugin, soldierName: player, rankScore: expectedRank, eaGuid: $"{player}-EA-GUID");
                plugin.OnPlayerJoin(player);

                // Wait until thread finishes
                Thread.Sleep(100);
                var rank = plugin.__GetCurrentRankFromPlayer__(player);

                Assert.IsTrue(plugin.__StatsTracker__.ContainsKey(player));
                Assert.IsTrue(plugin.__WelcomeStatsDictionary__.ContainsKey(player));
                Assert.IsNotNull(rank);
                Assert.AreEqual(expectedRank, rank);
            }
        }

        [TestMethod("Test if when player joins, create new session and add into stats tracker with ranking by kills")]
        [TestCategory("PRoConPluginAPI")]
        public void TestOnPlayerJoinOverallRanking()
        {
            var plugin = new AdKatsLogger();
            TestHelper.ConfigureDatabase(plugin);
            TestHelper.SetupGame(plugin);
            TestHelper.SetupServer(plugin);

            plugin.SetPluginVariable("Spam Protection|Enabled?", "Yes");
            plugin.SetPluginVariable("Stats Logging|Enabled?", "Yes");
            plugin.SetPluginVariable("Enable in-game commands?", "Yes");
            plugin.SetPluginVariable("Enable Ranking by score?", "No");

            var players = new string[] { Faker.Internet.UserName(), Faker.Internet.UserName() };
            var ranks = new int[] { 50, 20 };

            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                var expectedRank = ranks[i];

                TestHelper.InsertPlayer(plugin, soldierName: player, rankKills: expectedRank, eaGuid: $"{player}-EA-GUID");
                plugin.OnPlayerJoin(player);

                // Wait until thread finishes
                Thread.Sleep(100);
                var rank = plugin.__GetCurrentRankFromPlayer__(player);

                Assert.IsTrue(plugin.__StatsTracker__.ContainsKey(player));
                Assert.IsTrue(plugin.__WelcomeStatsDictionary__.ContainsKey(player));
                Assert.IsNotNull(rank);
                Assert.AreEqual(expectedRank, rank);
            }
        }

        [TestMethod("Test if when player authenticates, add players into stats tracker with PB's GUID")]
        [TestCategory("PRoConPluginAPI")]
        public void TestOnPlayerAuthenticated()
        {
            var plugin = new AdKatsLogger(); 
            TestHelper.ConfigureDatabase(plugin);
            TestHelper.SetupGame(plugin);
            TestHelper.SetupServer(plugin);

            plugin.SetPluginVariable("Stats Logging|Enabled?", "Yes");

            var soldierName = Faker.Internet.UserName();
            TestHelper.InsertPlayer(plugin, soldierName: soldierName);

            var guid = Guid.NewGuid().ToString();

            plugin.OnPlayerJoin(soldierName);
            plugin.OnPlayerAuthenticated(soldierName, guid);

            Assert.IsTrue(plugin.__StatsTracker__.ContainsKey(soldierName));
            Assert.IsTrue(plugin.__WelcomeStatsDictionary__.ContainsKey(soldierName));
            Assert.AreEqual(guid, plugin.__StatsTracker__[soldierName].Guid);
        }

        [TestMethod("Test if when player sends a global message, it persists into database")]
        [TestCategory("PRoConPluginAPI")]
        public void TestOnGlobalChat()
        {
            var plugin = new AdKatsLogger();
            TestHelper.ConfigureDatabase(plugin);
            TestHelper.SetupGame(plugin);
            TestHelper.SetupServer(plugin);

            plugin.SetPluginVariable("Chat Logging|Enabled?", "Yes");
            plugin.SetPluginVariable("Chat Logging|Instant logging of chat messages?", "Yes");

            var soldierName = Faker.Internet.UserName();
            TestHelper.InsertPlayer(plugin, soldierName: soldierName);

            plugin.OnGlobalChat(soldierName, "Sending to global");

            // Wait until thread finishes
            Thread.Sleep(300);

            var chatLog = plugin.__GetChatLogFromPlayer__(soldierName);

            Assert.IsNotNull(chatLog);
            Assert.AreEqual(1, chatLog.Count);
        }

        [TestMethod("Test if when player sends a team message, it persists into database")]
        [TestCategory("PRoConPluginAPI")]
        public void TestOnTeamChat()
        {
            var plugin = new AdKatsLogger();
            TestHelper.ConfigureDatabase(plugin);
            TestHelper.SetupGame(plugin);
            TestHelper.SetupServer(plugin);

            plugin.SetPluginVariable("Chat Logging|Enabled?", "Yes");
            plugin.SetPluginVariable("Chat Logging|Instant logging of chat messages?", "Yes");

            var soldierName = Faker.Internet.UserName();
            TestHelper.InsertPlayer(plugin, soldierName: soldierName);

            plugin.OnTeamChat(soldierName, "Sending to team", 1);

            // Wait until thread finishes
            Thread.Sleep(300);

            var chatLog = plugin.__GetChatLogFromPlayer__(soldierName);

            Assert.IsNotNull(chatLog);
            Assert.AreEqual(1, chatLog.Count);
        }

        [TestMethod("Test if when player sends a squad message, it persists into database")]
        [TestCategory("PRoConPluginAPI")]
        public void TestOnSquadChat()
        {
            var plugin = new AdKatsLogger();
            TestHelper.ConfigureDatabase(plugin);
            TestHelper.SetupGame(plugin);
            TestHelper.SetupServer(plugin);

            plugin.SetPluginVariable("Chat Logging|Enabled?", "Yes");
            plugin.SetPluginVariable("Chat Logging|Instant logging of chat messages?", "Yes");

            var soldierName = Faker.Internet.UserName();
            TestHelper.InsertPlayer(plugin, soldierName: soldierName);

            plugin.OnSquadChat(soldierName, "Sending to squad", 1, 1);

            // Wait until thread finishes
            Thread.Sleep(300);

            var chatLog = plugin.__GetChatLogFromPlayer__(soldierName);

            Assert.IsNotNull(chatLog);
            Assert.AreEqual(1, chatLog.Count);
        }
    }
}
