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
            TestHelper.SetupServer(plugin);

            plugin.SetPluginVariable("Spam Protection|Enabled?", "Yes");
            plugin.SetPluginVariable("Stats Logging|Enabled?", "Yes");
            plugin.SetPluginVariable("Enable in-game commands?", "Yes");
            plugin.SetPluginVariable("Enable Ranking by score?", "Yes");

            TestHelper.InsertPlayer(plugin, soldierName: "aleDsz", rankScore: 1, eaGuid: "aleDsz-EA-GUID");
            TestHelper.InsertPlayer(plugin, soldierName: "Foo", rankScore: 2, eaGuid: "Foo-EA-GUID");

            plugin.OnPlayerJoin("aleDsz");
            plugin.OnPlayerJoin("Foo");

            Assert.IsTrue(plugin.__StatsTracker__.ContainsKey("aleDsz"));
            Assert.IsTrue(plugin.__StatsTracker__.ContainsKey("Foo"));
            Assert.IsTrue(plugin.__WelcomeStatsDictionary__.ContainsKey("aleDsz"));
            Assert.IsTrue(plugin.__WelcomeStatsDictionary__.ContainsKey("Foo"));

            Assert.AreEqual(1, plugin.__GetCurrentRankFromPlayer__("aleDsz"));
            Assert.AreEqual(2, plugin.__GetCurrentRankFromPlayer__("Foo"));
        }

        [TestMethod("Test if when player joins, create new session and add into stats tracker with ranking by kills")]
        [TestCategory("PRoConPluginAPI")]
        public void TestOnPlayerJoinOverallRanking()
        {
            var plugin = new AdKatsLogger();
            TestHelper.ConfigureDatabase(plugin);
            TestHelper.SetupServer(plugin);

            plugin.SetPluginVariable("Spam Protection|Enabled?", "Yes");
            plugin.SetPluginVariable("Stats Logging|Enabled?", "Yes");
            plugin.SetPluginVariable("Enable in-game commands?", "Yes");
            plugin.SetPluginVariable("Enable Ranking by score?", "No");

            TestHelper.InsertPlayer(plugin, soldierName: "aleDsz", rankKills: 50, eaGuid: "aleDsz-EA-GUID");
            TestHelper.InsertPlayer(plugin, soldierName: "Foo", rankKills: 20, eaGuid: "Foo-EA-GUID");

            plugin.OnPlayerJoin("aleDsz");
            plugin.OnPlayerJoin("Foo");

            Assert.IsTrue(plugin.__StatsTracker__.ContainsKey("aleDsz"));
            Assert.IsTrue(plugin.__StatsTracker__.ContainsKey("Foo"));
            Assert.IsTrue(plugin.__WelcomeStatsDictionary__.ContainsKey("aleDsz"));
            Assert.IsTrue(plugin.__WelcomeStatsDictionary__.ContainsKey("Foo"));

            Assert.AreEqual(50, plugin.__GetCurrentRankFromPlayer__("aleDsz"));
            Assert.AreEqual(20, plugin.__GetCurrentRankFromPlayer__("Foo"));
        }

        [TestMethod("Test if when player authenticates, add players into stats tracker with PB's GUID")]
        [TestCategory("PRoConPluginAPI")]
        public void TestOnPlayerAuthenticated()
        {
            var plugin = new AdKatsLogger();
            TestHelper.ConfigureDatabase(plugin);
            TestHelper.SetupServer(plugin);

            plugin.SetPluginVariable("Stats Logging|Enabled?", "Yes");
            TestHelper.InsertPlayer(plugin, soldierName: "aleDsz");

            var guid = Guid.NewGuid().ToString();

            plugin.OnPlayerJoin("aleDsz");
            plugin.OnPlayerAuthenticated("aleDsz", guid);

            Assert.IsTrue(plugin.__StatsTracker__.ContainsKey("aleDsz"));
            Assert.IsTrue(plugin.__WelcomeStatsDictionary__.ContainsKey("aleDsz"));
            Assert.AreEqual(guid, plugin.__StatsTracker__["aleDsz"].Guid);
        }

        [TestMethod("Test if when player sends a message, it persists into database")]
        [TestCategory("PRoConPluginAPI")]
        public void TestOnChat()
        {
            var plugin = new AdKatsLogger();
            TestHelper.ConfigureDatabase(plugin);
            TestHelper.SetupServer(plugin);

            plugin.SetPluginVariable("Chat Logging|Enabled?", "Yes");
            plugin.SetPluginVariable("Chat Logging|Instant logging of chat messages?", "Yes");
            TestHelper.InsertPlayer(plugin, soldierName: "aleDsz");

            plugin.OnGlobalChat("aleDsz", "Sending to global");
            plugin.OnTeamChat("aleDsz", "Sending to team", 1);
            plugin.OnSquadChat("aleDsz", "Sending to squad", 1, 1);

            var chatLog = plugin.__GetChatLogFromPlayer__("aleDsz");

            Assert.IsNotNull(chatLog);
            Assert.AreEqual(3, chatLog.Count);
        }
    }
}
