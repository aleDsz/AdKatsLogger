/*  Copyright 2013 [GWC]XpKillerhx

	Edited by aleDsz.

    This plugin file is part of PRoCon Frostbite.

    This plugin is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This plugin is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with PRoCon Frostbite.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Configuration;
using System.ComponentModel;
using System.Threading;

//MySQL native includes
using MySql.Data.MySqlClient;

//Procon includes
using PRoCon.Core;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;
using PRoCon.Core.Players;
using PRoCon.Core.Players.Items;

namespace PRoConEvents
{
    public class AdKatsLogger : PRoConPluginAPI, IPRoConPluginInterface
    {
        #region Properties
        private MatchCommand _loggerStatusCommand;

		// Database
		private string _dbHost;
		private string _dbPort;
		private string _dbName;
		private string _dbUsername;
		private string _dbPassword;
		private MySqlTransaction _transaction;
		private MySqlConnection _conn;
		private MySqlConnectionStringBuilder _connStrBuilder;
		private int _transactionRetryCount;

		// PRoCon
		private string _serverHostName;
		private string _serverPort;
		private string _serverPRoConVersion;

		// Locks
		private readonly object _tableBuilderLock;
		private readonly object _chatLogLock;
		private readonly object _sqlQueryLock;
		private readonly object _sessionLock;
		private readonly object _streamLock;
		private readonly object _connectionStringBuilderLock;
		private readonly object _registerAllCommandsLock;
		private readonly object _commandsSetupLock;

		// Date Offset
		private double _offset;

		// Logging
		private string _loggerLevel;
		private List<string> _allowedLoggerLevel;

		// Chat logging
		private List<string> _chatStringFilterRuleList;
		private List<Regex> _chatRegexFilterRuleList;

		// Trackers
		private List<ChatLog> _chatLogTracker;
		private Dictionary<string, Stats> _statsTracker;
		private Dictionary<string, Stats> _sessionTracker;
		private Dictionary<string, CPunkbusterInfo> _punkbusterInfoTracker;
		private Dictionary<Kill, int> _dogtagTracker;

		// Stats
		private List<string> _playerStatsMessageFormat;
		private List<string> _playerOfTheDayStatsMessageFormat;
		private List<string> _weaponStatsMessageFormat;
		private List<string> _serverStatsMessageFormat;
		private List<string> _welcomeMessageFormat;
		private List<string> _newPlayerMessageFormat;
		private int _welcomeLoggingDelay;
		private string _top10HeaderFormat;
		private string _top10RowFormat;
		private string _top10ForPeriodHeaderFormat;
		private int _intervalTop10ForPeriod;
		private string _weaponTop10HeaderFormat;
		private string _weaponTop10RowFormat;

		// Tables
		private Dictionary<string, DateTime> _welcomeStatsDictionary;
		private Dictionary<string, Dictionary<string, UsedWeapon>> _usedWeaponDictionary;

		// Session
		private bool _didRoundStart;
		private MapStats _mapStats;
		private MapStats _nextMapInfo;
		private List<Stats> _passedSessionsList;
		private List<string> _sessionMessageFormat;

		// Spam protection
		private int _numberOfAllowedRequests;
		private SpamProtection _spamProtection;

		// Commands
		private string _statsInGameCommand;
		private string _serverStatsInGameCommand;
		private string _sessionInGameCommand;
		private string _dogtagsInGameCommand;
		private string _top10InGameCommand;
		private string _playerOfTheDayInGameCommand;
		private string _top10ForPeriodInGameCommand;
		private Dictionary<string, IngameCommand> _ingameCommands;

		// Server data
		private int _serverID;
		private int _gameID;
		private int _serverGroup;
		private int _serverInfoDelay;
		private int _roundStartCount;
		private int _roundRestartCount;
		private string _game;
		private string _serverName;
		private bool _isPluginEnabled;
		private bool _isStreaming;
		private bool _isPluginReady;
		private bool _isDatabaseReady;
		private DateTime? _lastCommandExecutedAt;
		private DateTime? _lastServerUpdatedAt;

		// Toggles
		// Chat Logging
		private enumBoolYesNo _enumChatLogging;
		private enumBoolYesNo _enumChatLoggingServerSpam;
		private enumBoolYesNo _enumInstantChatLogging;
		private enumBoolYesNo _enumChatLoggingFilter;

		// Stat Logging
		private enumBoolYesNo _enumStatsLogging;
		private enumBoolYesNo _enumStatsWeaponLogging;
		private enumBoolYesNo _enumStatsRankingByScore;
		private enumBoolYesNo _enumStatsIngameCommands;
		private enumBoolYesNo _enumStatsOverallRanking;
		private enumBoolYesNo _enumStatsSendToAllPlayers;
		private enumBoolYesNo _enumStatsKdrCorrection;
		private enumBoolYesNo _enumStatsRealTimeScoreboard;
		private enumBoolYesNo _enumStatsWelcomeLogging;
		private enumBoolYesNo _enumStatsTop10;

		// Session
		private enumBoolYesNo _enumSession;
		private enumBoolYesNo _enumPersistSession;

		// Spam Protection
		private enumBoolYesNo _enumSpamProtection;

		private enumBoolYesNo _enumMapLogging;
		private enumBoolYesNo _enumGuidLogging;
        #endregion

        public AdKatsLogger()
        {
			this._loggerStatusCommand = new MatchCommand(
				"AdKatsLogger",
				"GetStatus",
				new List<string>(),
				"AdKatsLogger_Status",
				new List<MatchArgumentFormat>(),
				new ExecutionRequirements(ExecutionScope.None),
				"Useable by other plugins to determine the current status of this plugin."
			);

			this._offset = 0;
			this._serverGroup = 0;
			this._serverID = 0;
			this._gameID = 0;
			this._transactionRetryCount = 3;
			this._numberOfAllowedRequests = 10;
			this._welcomeLoggingDelay = 5;
			this._intervalTop10ForPeriod = 7;
			this._serverInfoDelay = 30;
			this._roundStartCount = 2;
			this._roundRestartCount = 1;

			this._chatLogTracker = new List<ChatLog>();
			this._statsTracker = new Dictionary<string, Stats>();
			this._sessionTracker = new Dictionary<string, Stats>();
			this._punkbusterInfoTracker = new Dictionary<string, CPunkbusterInfo>();
			this._dogtagTracker = new Dictionary<Kill, int>();
			this._passedSessionsList = new List<Stats>();
			this._usedWeaponDictionary = new Dictionary<string, Dictionary<string, UsedWeapon>>();

			this._tableBuilderLock = new object();
			this._chatLogLock = new object();
			this._sqlQueryLock = new object();
			this._sessionLock = new object();
			this._streamLock = new object();
			this._connectionStringBuilderLock = new object();
			this._registerAllCommandsLock = new object();
			this._commandsSetupLock = new object();
			
			this._connStrBuilder = new MySqlConnectionStringBuilder();

			this._serverName = "";
			this._game = "";
			this._isStreaming = true;
			this._isPluginEnabled = false;
			this._isPluginReady = false;
			this._isDatabaseReady = false;

			this._spamProtection = new SpamProtection(this._numberOfAllowedRequests);

			this._chatStringFilterRuleList = new List<string>();
			this._chatRegexFilterRuleList = new List<Regex>();

			this._mapStats = new MapStats(new DateTimeWithOffset(this._offset).Now, "START", 0, 0, this._offset);
			this._didRoundStart = false;

			this._loggerLevel = "Error";
			this.BuildAllowedLoggerLevel();

			this._statsInGameCommand = "stats,rank";
			this._serverStatsInGameCommand = "serverstats";
			this._sessionInGameCommand = "session";
			this._dogtagsInGameCommand = "dogtags";
			this._top10InGameCommand = "top10";
			this._playerOfTheDayInGameCommand = "playeroftheday,potd";
			this._top10ForPeriodInGameCommand = "weektop10,wtop10";
			this._ingameCommands = new Dictionary<string, IngameCommand>();

			this._welcomeStatsDictionary = new Dictionary<string, DateTime>();

			this._playerStatsMessageFormat = new List<string>();
			this._playerStatsMessageFormat.Add("Serverstats for %playerName%:");
			this._playerStatsMessageFormat.Add("Score: %playerScore% %playerKills% Kills %playerHeadshots% HS %playerDeaths% Deaths K/D: %playerKDR%");
			this._playerStatsMessageFormat.Add("Your Server Rank is: %playerRank% of %allRanks%");

			this._playerOfTheDayStatsMessageFormat = new List<string>();
			this._playerOfTheDayStatsMessageFormat.Add("%playerName% is the Player of the day");
			this._playerOfTheDayStatsMessageFormat.Add("Score: %playerScore% %playerKills% Kills %playerHeadshots% HS %playerDeaths% Deaths K/D: %playerKDR%");
			this._playerOfTheDayStatsMessageFormat.Add("His Server Rank is: %playerRank% of %allRanks%");
			this._playerOfTheDayStatsMessageFormat.Add("Overall playtime for today: %playerPlaytime%");

			this._weaponStatsMessageFormat = new List<string>();
			this._weaponStatsMessageFormat.Add("%playerName%'s Stats for %Weapon%:");
			this._weaponStatsMessageFormat.Add("%playerKills% Kills %playerHeadshots% Headshots  Headshotrate: %playerKHR%%");
			this._weaponStatsMessageFormat.Add("Your Weapon Rank is: %playerRank% of %allRanks%");

			this._serverStatsMessageFormat = new List<string>();
			this._weaponStatsMessageFormat.Add("Server statistics for server %serverName%");
			this._weaponStatsMessageFormat.Add("Unique Players: %countPlayer%");
			this._weaponStatsMessageFormat.Add("Total playtime: %sumPlaytime%");
			this._weaponStatsMessageFormat.Add("Total score: %sumScore% Avg. Score: %avgScore% Avg. SPM: %avgSPM%");
			this._weaponStatsMessageFormat.Add("Total kills: %sumKills% Avg. Kills: %avgKills% Avg. KPM: %avgKPM%");

			this._welcomeMessageFormat = new List<string>();
			this._welcomeMessageFormat.Add("Nice to see you on our Server again, %playerName%");
			this._welcomeMessageFormat.Add("Server Stats for %playerName%:");
			this._welcomeMessageFormat.Add("Score: %playerScore% %playerKills% Kills %playerHeadshots% HS %playerDeaths% Deaths KDR: %playerKDR%");
			this._welcomeMessageFormat.Add("Your Server Rank is: %playerRank% of %allRanks%");

			this._newPlayerMessageFormat = new List<string>();
			this._newPlayerMessageFormat.Add("Welcome to the %serverName% Server, %playerName%");

			this._sessionMessageFormat = new List<string>();
			this._sessionMessageFormat.Add("%playerName%'s Session Data Session started %SessionStarted%");
			this._sessionMessageFormat.Add("Score: %playerScore% %playerKills% Kills %playerHeadshots% HS %playerDeaths% Deaths KDR: %playerKDR%");
			this._sessionMessageFormat.Add("Your Rank: %playerRank% (%RankDif%)");
			this._sessionMessageFormat.Add("Session length: %SessionDuration% Minutes");

			this._top10HeaderFormat = "Top 10 Player of the %serverName% Server";
			this._top10RowFormat = "%Rank%. %playerName% Score: %playerScore% %playerKills% Kills %playerHeadshots% Headshots %playerDeaths% Deaths KDR: %playerKDR%";
			this._weaponTop10HeaderFormat = "Top 10 Player with %Weapon% of the %serverName%";
			this._weaponTop10RowFormat = "%Rank%. %playerName% %playerKills% Kills %playerHeadshots% Headshots %playerDeaths% Deaths HSKR: %playerKHR%%";
			this._top10ForPeriodHeaderFormat = "Top 10 Player of the %serverName% Server over the last %intervaldays% days";
	} 

		// IPRoConPluginInterface methods 

		public string GetPluginName() => "AdKats Logger";

        public string GetPluginAuthor() => "aleDsz";

        public string GetPluginVersion() => "1.0.0.0";

        public string GetPluginWebsite() => "https://github.com/aleDsz/AdKatsLogger";

        public string GetPluginDescription() => "pruu Custom Plugin for AdKats Logging (Stats, Weapon, Server and Chat)";

        public List<CPluginVariable> GetDisplayPluginVariables()
        {
			var pluginsList = new List<CPluginVariable>();

			pluginsList.Add(new CPluginVariable("Server Details|Host", typeof(string), this._dbHost));
			pluginsList.Add(new CPluginVariable("Server Details|Port", typeof(string), this._dbPort));
			pluginsList.Add(new CPluginVariable("Server Details|Database Name", typeof(string), this._dbName));
			pluginsList.Add(new CPluginVariable("Server Details|Username", typeof(string), this._dbUsername));
			pluginsList.Add(new CPluginVariable("Server Details|Password", typeof(string), this._dbPassword));

			pluginsList.Add(new CPluginVariable("Query Details|Failed Transaction retry attempts", typeof(int), this._transactionRetryCount));
			pluginsList.Add(new CPluginVariable("Query Details|Minimum seconds between ServerInfo Updates", typeof(int), this._serverInfoDelay));

			pluginsList.Add(new CPluginVariable("Chat Logging|Enabled?", typeof(enumBoolYesNo), this._enumChatLogging));

			if (this._enumChatLogging == enumBoolYesNo.Yes)
			{
				pluginsList.Add(new CPluginVariable("Chat Logging|Log server spam?", typeof(enumBoolYesNo), this._enumChatLoggingServerSpam));
				pluginsList.Add(new CPluginVariable("Chat Logging|Instant logging of chat messages?", typeof(enumBoolYesNo), this._enumInstantChatLogging));
				pluginsList.Add(new CPluginVariable("Chat Logging|Enable filter?", typeof(enumBoolYesNo), this._enumChatLoggingFilter));

				if (this._enumChatLoggingFilter == enumBoolYesNo.Yes)
				{
					var replacedList = ReplaceListStrings(this._chatStringFilterRuleList, "&#124", "|");
					replacedList = ReplaceListStrings(replacedList, "&#43", "+");
					this._chatStringFilterRuleList = replacedList;

					pluginsList.Add(new CPluginVariable("Chat Logging|Regex", typeof(string[]), this._chatStringFilterRuleList.ToArray()));
				}
			}

			pluginsList.Add(new CPluginVariable("Stats Logging|Enabled?", typeof(enumBoolYesNo), this._enumStatsLogging));

			if (this._enumStatsLogging == enumBoolYesNo.Yes)
			{
				pluginsList.Add(new CPluginVariable("Stats Logging|Enable weapon stats?", typeof(enumBoolYesNo), this._enumStatsWeaponLogging));
				pluginsList.Add(new CPluginVariable("Stats Logging|Enable Ranking by score?", typeof(enumBoolYesNo), this._enumStatsRankingByScore));
				pluginsList.Add(new CPluginVariable("Stats Logging|Enable in-game commands?", typeof(enumBoolYesNo), this._enumStatsIngameCommands));
				pluginsList.Add(new CPluginVariable("Stats Logging|Enable Overall ranking?", typeof(enumBoolYesNo), this._enumStatsOverallRanking));
				pluginsList.Add(new CPluginVariable("Stats Logging|Send Stats to all Players?", typeof(enumBoolYesNo), this._enumStatsSendToAllPlayers));
				pluginsList.Add(new CPluginVariable("Stats Logging|Enable KDR correction?", typeof(enumBoolYesNo), this._enumStatsKdrCorrection));
				pluginsList.Add(new CPluginVariable("Stats Logging|Enable real-time scoreboard?", typeof(enumBoolYesNo), this._enumStatsRealTimeScoreboard));
				pluginsList.Add(new CPluginVariable("Stats Logging|Enable welcome logging?", typeof(enumBoolYesNo), this._enumStatsWelcomeLogging));
				pluginsList.Add(new CPluginVariable("Stats Logging|Enable Top 10 commands?", typeof(enumBoolYesNo), this._enumStatsTop10));

				pluginsList.Add(new CPluginVariable("Stats Logging|Server group (0 - 128)", typeof(int), this._serverGroup));

				pluginsList.Add(new CPluginVariable("Stats Logging|Player message format", typeof(string[]), this._playerStatsMessageFormat.ToArray()));
				pluginsList.Add(new CPluginVariable("Stats Logging|Player of The Day message format", typeof(string[]), this._playerOfTheDayStatsMessageFormat.ToArray()));
				pluginsList.Add(new CPluginVariable("Stats Logging|Weapon Stats message format", typeof(string[]), this._weaponStatsMessageFormat.ToArray()));
				pluginsList.Add(new CPluginVariable("Stats Logging|Server Stats message format", typeof(string[]), this._serverStatsMessageFormat.ToArray()));
			}

			if (this._enumStatsWelcomeLogging == enumBoolYesNo.Yes)
			{
				pluginsList.Add(new CPluginVariable("Stats Welcome Logging|Welcome message format", typeof(string[]), this._welcomeMessageFormat.ToArray()));
				pluginsList.Add(new CPluginVariable("Stats Welcome Logging|New player message format", typeof(string[]), this._newPlayerMessageFormat.ToArray()));
				pluginsList.Add(new CPluginVariable("Stats Welcome Logging|Delay", typeof(int), this._welcomeLoggingDelay));
			}

			if (this._enumStatsTop10 == enumBoolYesNo.Yes)
			{
				pluginsList.Add(new CPluginVariable("Stats Logging|Top10 header line", typeof(string), this._top10HeaderFormat));
				pluginsList.Add(new CPluginVariable("Stats Logging|Top10 row format", typeof(string), this._top10RowFormat));

				pluginsList.Add(new CPluginVariable("Stats Logging|Top10 for period header line", typeof(string), this._top10ForPeriodHeaderFormat));
				pluginsList.Add(new CPluginVariable("Stats Logging|Top10 for period interval days", typeof(int), this._intervalTop10ForPeriod));

				pluginsList.Add(new CPluginVariable("Stats Logging|WeaponTop10 header line", typeof(string), this._weaponTop10HeaderFormat));
				pluginsList.Add(new CPluginVariable("Stats Logging|WeaponTop10 row format", typeof(string), this._weaponTop10RowFormat));
			}

			if (this._enumStatsTop10 == enumBoolYesNo.Yes)
			{
				pluginsList.Add(new CPluginVariable("Ingame Command Setup|Stats command:", typeof(string), this._statsInGameCommand));
				pluginsList.Add(new CPluginVariable("Ingame Command Setup|ServerStats command:", typeof(string), this._serverStatsInGameCommand));
				pluginsList.Add(new CPluginVariable("Ingame Command Setup|Session command:", typeof(string), this._sessionInGameCommand));
				pluginsList.Add(new CPluginVariable("Ingame Command Setup|Dogtags command:", typeof(string), this._dogtagsInGameCommand));
				pluginsList.Add(new CPluginVariable("Ingame Command Setup|Top10 command:", typeof(string), this._top10InGameCommand));
				pluginsList.Add(new CPluginVariable("Ingame Command Setup|Player Of The Day command:", typeof(string), this._playerOfTheDayInGameCommand));
				pluginsList.Add(new CPluginVariable("Ingame Command Setup|Top10 for Period command:", typeof(string), this._top10ForPeriodInGameCommand));
			}

			pluginsList.Add(new CPluginVariable("Session|Enabled?", typeof(enumBoolYesNo), this._enumSession));

			if (this._enumSession == enumBoolYesNo.Yes)
			{
				pluginsList.Add(new CPluginVariable("Session|Message format", typeof(string[]), this._sessionMessageFormat.ToArray()));
				pluginsList.Add(new CPluginVariable("Session|Persist session?", typeof(enumBoolYesNo), this._enumPersistSession));
			}

			pluginsList.Add(new CPluginVariable("Spam Protection|Enabled?", typeof(enumBoolYesNo), this._enumSpamProtection));

			if (this._enumSession == enumBoolYesNo.Yes)
				pluginsList.Add(new CPluginVariable("Spam Protection|Max requests allowed", typeof(int), this._numberOfAllowedRequests));

			pluginsList.Add(new CPluginVariable("Misc|Logger level", "enum.Actions(Debug|Info|Warning|Error)", this._loggerLevel));
			pluginsList.Add(new CPluginVariable("Misc|DateTimeZone", typeof(double), this._offset));

			return pluginsList;
		}

		public List<CPluginVariable> GetPluginVariables()
        {
			var pluginsList = new List<CPluginVariable>();

			pluginsList.Add(new CPluginVariable("Host", typeof(string), this._dbHost));
			pluginsList.Add(new CPluginVariable("Port", typeof(string), this._dbPort));
			pluginsList.Add(new CPluginVariable("Database Name", typeof(string), this._dbName));
			pluginsList.Add(new CPluginVariable("Username", typeof(string), this._dbUsername));
			pluginsList.Add(new CPluginVariable("Password", typeof(string), this._dbPassword));

			pluginsList.Add(new CPluginVariable("Transaction retry attempts", typeof(int), this._transactionRetryCount));
			pluginsList.Add(new CPluginVariable("ServerInfo Updates", typeof(int), this._serverInfoDelay));

			pluginsList.Add(new CPluginVariable("Chat Logging|Enabled?", typeof(enumBoolYesNo), this._enumChatLogging));

			if (this._enumChatLogging == enumBoolYesNo.Yes)
			{
				pluginsList.Add(new CPluginVariable("Log server spam?", typeof(enumBoolYesNo), this._enumChatLoggingServerSpam));
				pluginsList.Add(new CPluginVariable("Instant logging of chat messages?", typeof(enumBoolYesNo), this._enumInstantChatLogging));
				pluginsList.Add(new CPluginVariable("Enable filter?", typeof(enumBoolYesNo), this._enumChatLoggingFilter));

				if (this._enumChatLoggingFilter == enumBoolYesNo.Yes)
				{
					var replacedList = ReplaceListStrings(this._chatStringFilterRuleList, "&#124", "|");
					replacedList = ReplaceListStrings(replacedList, "&#43", "+");
					this._chatStringFilterRuleList = replacedList;

					pluginsList.Add(new CPluginVariable("Regex", typeof(string[]), this._chatStringFilterRuleList.ToArray()));
				}
			}

			pluginsList.Add(new CPluginVariable("Stats Logging|Enabled?", typeof(enumBoolYesNo), this._enumStatsLogging));

			if (this._enumStatsLogging == enumBoolYesNo.Yes)
			{
				pluginsList.Add(new CPluginVariable("Enable weapon stats?", typeof(enumBoolYesNo), this._enumStatsWeaponLogging));
				pluginsList.Add(new CPluginVariable("Enable Ranking by score?", typeof(enumBoolYesNo), this._enumStatsRankingByScore));
				pluginsList.Add(new CPluginVariable("Enable in-game commands?", typeof(enumBoolYesNo), this._enumStatsIngameCommands));
				pluginsList.Add(new CPluginVariable("Enable Overall ranking?", typeof(enumBoolYesNo), this._enumStatsOverallRanking));
				pluginsList.Add(new CPluginVariable("Send Stats to all Players?", typeof(enumBoolYesNo), this._enumStatsSendToAllPlayers));
				pluginsList.Add(new CPluginVariable("Enable KDR correction?", typeof(enumBoolYesNo), this._enumStatsKdrCorrection));
				pluginsList.Add(new CPluginVariable("Enable real-time scoreboard?", typeof(enumBoolYesNo), this._enumStatsRealTimeScoreboard));
				pluginsList.Add(new CPluginVariable("Enable welcome logging?", typeof(enumBoolYesNo), this._enumStatsWelcomeLogging));
				pluginsList.Add(new CPluginVariable("Enable Top 10 commands?", typeof(enumBoolYesNo), this._enumStatsTop10));

				pluginsList.Add(new CPluginVariable("Server group (0 - 128)", typeof(int), this._serverGroup));

				pluginsList.Add(new CPluginVariable("Player message format", typeof(string[]), this._playerStatsMessageFormat.ToArray()));
				pluginsList.Add(new CPluginVariable("Player of The Day message format", typeof(string[]), this._playerOfTheDayStatsMessageFormat.ToArray()));
				pluginsList.Add(new CPluginVariable("Weapon Stats message format", typeof(string[]), this._weaponStatsMessageFormat.ToArray()));
				pluginsList.Add(new CPluginVariable("Server Stats message format", typeof(string[]), this._serverStatsMessageFormat.ToArray()));
			}

			if (this._enumStatsWelcomeLogging == enumBoolYesNo.Yes)
			{
				pluginsList.Add(new CPluginVariable("Welcome message format", typeof(string[]), this._welcomeMessageFormat.ToArray()));
				pluginsList.Add(new CPluginVariable("New player message format", typeof(string[]), this._newPlayerMessageFormat.ToArray()));
				pluginsList.Add(new CPluginVariable("Delay", typeof(int), this._welcomeLoggingDelay));
			}

			if (this._enumStatsTop10 == enumBoolYesNo.Yes)
			{
				pluginsList.Add(new CPluginVariable("Top10 header line", typeof(string), this._top10HeaderFormat));
				pluginsList.Add(new CPluginVariable("Top10 row format", typeof(string), this._top10RowFormat));

				pluginsList.Add(new CPluginVariable("Top10 for period header line", typeof(string), this._top10ForPeriodHeaderFormat));
				pluginsList.Add(new CPluginVariable("Top10 for period interval days", typeof(int), this._intervalTop10ForPeriod));

				pluginsList.Add(new CPluginVariable("WeaponTop10 header line", typeof(string), this._weaponTop10HeaderFormat));
				pluginsList.Add(new CPluginVariable("WeaponTop10 row format", typeof(string), this._weaponTop10RowFormat));
			}

			if (this._enumStatsTop10 == enumBoolYesNo.Yes)
			{
				pluginsList.Add(new CPluginVariable("Stats command", typeof(string), this._statsInGameCommand));
				pluginsList.Add(new CPluginVariable("ServerStats command", typeof(string), this._serverStatsInGameCommand));
				pluginsList.Add(new CPluginVariable("Session command", typeof(string), this._sessionInGameCommand));
				pluginsList.Add(new CPluginVariable("Dogtags command", typeof(string), this._dogtagsInGameCommand));
				pluginsList.Add(new CPluginVariable("Top10 command", typeof(string), this._top10InGameCommand));
				pluginsList.Add(new CPluginVariable("Player Of The Day command", typeof(string), this._playerOfTheDayInGameCommand));
				pluginsList.Add(new CPluginVariable("Top10 for Period command", typeof(string), this._top10ForPeriodInGameCommand));
			}

			pluginsList.Add(new CPluginVariable("Session|Enabled?", typeof(enumBoolYesNo), this._enumSession));

			if (this._enumSession == enumBoolYesNo.Yes)
			{
				pluginsList.Add(new CPluginVariable("Message format", typeof(string[]), this._sessionMessageFormat.ToArray()));
				pluginsList.Add(new CPluginVariable("Persist session?", typeof(enumBoolYesNo), this._enumPersistSession));
			}

			pluginsList.Add(new CPluginVariable("Spam Protection|Enabled?", typeof(enumBoolYesNo), this._enumSpamProtection));

			if (this._enumSession == enumBoolYesNo.Yes)
				pluginsList.Add(new CPluginVariable("Max requests allowed", typeof(int), this._numberOfAllowedRequests));

			pluginsList.Add(new CPluginVariable("Logger level", "enum.Actions(Debug|Info|Warning|Error)", this._loggerLevel));
			pluginsList.Add(new CPluginVariable("DateTimeZone", typeof(double), this._offset));

			return pluginsList;
		}

        public void SetPluginVariable(string key, string value)
        {
			if (key.Contains("Host"))
			{
				this._dbHost = value;
				this.CreateTables();
			}
			else if (key.Contains("Port"))
			{
				this._dbPort = value;
				this.CreateTables();
			}
			else if (key.Contains("Database Name"))
			{
				this._dbName = value;
				this.CreateTables();
			}
			else if (key.Contains("Username"))
			{
				this._dbUsername = value;
				this.CreateTables();
			}
			else if (key.Contains("Password"))
			{
				this._dbPassword = value;
				this.CreateTables();
			}
			else if (key.Contains("Transaction retry attempts"))
			{
				Int32.TryParse(value, out this._transactionRetryCount);

				if (this._transactionRetryCount < 1)
					this._transactionRetryCount = 3;
			}
			else if (key.Contains("ServerInfo Updates"))
			{
				Int32.TryParse(value, out this._serverInfoDelay);

				if (this._serverInfoDelay < 1)
					this._serverInfoDelay = 30;
			}
			else if (key.Contains("Chat Logging|Enabled?") && Enum.IsDefined(typeof(enumBoolYesNo), value) == true)
			{
				this._enumChatLogging = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), value);
			}
			else if (key.Contains("Log server spam?") && Enum.IsDefined(typeof(enumBoolYesNo), value) == true)
			{
				this._enumChatLoggingServerSpam = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), value);
			}
			else if (key.Contains("Instant logging of chat messages?") && Enum.IsDefined(typeof(enumBoolYesNo), value) == true)
			{
				this._enumInstantChatLogging = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), value);
			}
			else if (key.Contains("Enable filter?") && Enum.IsDefined(typeof(enumBoolYesNo), value) == true)
			{
				this._enumChatLoggingFilter = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), value);
			}
			else if (key.Contains("Regex"))
			{
				this._chatStringFilterRuleList = new List<string>(CPluginVariable.DecodeStringArray(value));
				this.BuildRegexRuleset();
			}
			else if (key.Contains("Stats Logging|Enabled?") && Enum.IsDefined(typeof(enumBoolYesNo), value) == true)
			{
				this._enumStatsLogging = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), value);
			}
			else if (key.Contains("Enable weapon stats?") && Enum.IsDefined(typeof(enumBoolYesNo), value) == true)
			{
				this._enumStatsWeaponLogging = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), value);
			}
			else if (key.Contains("Enable Ranking by score?") && Enum.IsDefined(typeof(enumBoolYesNo), value) == true)
			{
				this._enumStatsRankingByScore = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), value);
			}
			else if (key.Contains("Enable in-game commands?") && Enum.IsDefined(typeof(enumBoolYesNo), value) == true)
			{
				this._enumStatsIngameCommands = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), value);
			}
			else if (key.Contains("Enable Overall ranking?") && Enum.IsDefined(typeof(enumBoolYesNo), value) == true)
			{
				this._enumStatsOverallRanking = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), value);
			}
			else if (key.Contains("Send Stats to all Players?") && Enum.IsDefined(typeof(enumBoolYesNo), value) == true)
			{
				this._enumStatsSendToAllPlayers = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), value);
			}
			else if (key.Contains("Enable KDR correction?") && Enum.IsDefined(typeof(enumBoolYesNo), value) == true)
			{
				this._enumStatsKdrCorrection = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), value);
			}
			else if (key.Contains("Enable real-time scoreboard?") && Enum.IsDefined(typeof(enumBoolYesNo), value) == true)
			{
				this._enumStatsRealTimeScoreboard = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), value);
			}
			else if (key.Contains("Enable welcome logging?") && Enum.IsDefined(typeof(enumBoolYesNo), value) == true)
			{
				this._enumStatsWelcomeLogging = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), value);
			}
			else if (key.Contains("Enable Top 10 commands?") && Enum.IsDefined(typeof(enumBoolYesNo), value) == true)
			{
				this._enumStatsTop10 = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), value);
			}
			else if (key.Contains("Server group (0 - 128)"))
			{
				Int32.TryParse(value, out this._serverGroup);

				if (this._serverGroup < 0 || this._serverGroup > 128)
					this._serverGroup = 0;
			}
			else if (key.Contains("Player message format"))
			{
				this._playerStatsMessageFormat = new List<string>(CPluginVariable.DecodeStringArray(value));
			}
			else if (key.Contains("Player of The Day message format"))
			{
				this._playerOfTheDayStatsMessageFormat = new List<string>(CPluginVariable.DecodeStringArray(value));
			}
			else if (key.Contains("Weapon Stats message format"))
			{
				this._weaponStatsMessageFormat = new List<string>(CPluginVariable.DecodeStringArray(value));
			}
			else if (key.Contains("Server Stats message format"))
			{
				this._serverStatsMessageFormat = new List<string>(CPluginVariable.DecodeStringArray(value));
			}
			else if (key.Contains("Welcome message format"))
			{
				this._welcomeMessageFormat = new List<string>(CPluginVariable.DecodeStringArray(value));
			}
			else if (key.Contains("New player message format"))
			{
				this._newPlayerMessageFormat = new List<string>(CPluginVariable.DecodeStringArray(value));
			}
			else if (key.Contains("Delay"))
			{
				Int32.TryParse(value, out this._welcomeLoggingDelay);

				if (this._welcomeLoggingDelay < 0)
					this._welcomeLoggingDelay = 5;
			}
			else if (key.Contains("Top10 header line"))
			{
				this._top10HeaderFormat = value;
			}
			else if (key.Contains("Top10 row format"))
			{
				this._top10RowFormat = value;
			}
			else if (key.Contains("Top10 for period header line"))
			{
				this._top10ForPeriodHeaderFormat = value;
			}
			else if (key.Contains("Top10 for period interval days"))
			{
				Int32.TryParse(value, out this._intervalTop10ForPeriod);
			}
			else if (key.Contains("WeaponTop10 header line"))
			{
				this._weaponTop10HeaderFormat = value;
			}
			else if (key.Contains("WeaponTop10 row format"))
			{
				this._weaponTop10RowFormat = value;
			}
			else if (key.Contains("Stats command"))
			{
				this._statsInGameCommand = value;
			}
			else if (key.Contains("ServerStats command"))
			{
				this._serverStatsInGameCommand = value;
			}
			else if (key.Contains("Session command"))
			{
				this._sessionInGameCommand = value;
			}
			else if (key.Contains("Dogtags command"))
			{
				this._dogtagsInGameCommand = value;
			}
			else if (key.Contains("Top10 command"))
			{
				this._top10InGameCommand = value;
			}
			else if (key.Contains("Player Of The Day command"))
			{
				this._playerOfTheDayInGameCommand = value;
			}
			else if (key.Contains("Top10 for Period command"))
			{
				this._top10ForPeriodInGameCommand = value;
			}
			else if (key.Contains("Session|Enabled?") && Enum.IsDefined(typeof(enumBoolYesNo), value) == true)
			{
				this._enumSession = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), value);
			}
			else if (key.Contains("Message format"))
			{
				this._sessionMessageFormat = new List<string>(CPluginVariable.DecodeStringArray(value));
			}
			else if (key.Contains("Persist session?") && Enum.IsDefined(typeof(enumBoolYesNo), value) == true)
			{
				this._enumPersistSession = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), value);
			}
			else if (key.Contains("Spam Protection|Enabled?") && Enum.IsDefined(typeof(enumBoolYesNo), value) == true)
			{
				this._enumSpamProtection = (enumBoolYesNo)Enum.Parse(typeof(enumBoolYesNo), value);
			}
			else if (key.Contains("Max requests allowed"))
			{
				Int32.TryParse(value, out this._numberOfAllowedRequests);

				if (this._numberOfAllowedRequests < 0)
					this._numberOfAllowedRequests = 10;
			}
			else if (key.Contains("Logger level"))
			{
				this._loggerLevel = value;
				this.BuildAllowedLoggerLevel();
			}
			else if (key.Contains("DateTimeZone"))
			{
				this._offset = Convert.ToDouble(value);
			}

			this.RegisterAllCommands();
		}

        public void OnPluginEnable()
        {
			this._isStreaming = true;
			this._isPluginEnabled = true;
			this._serverName = "";
			this.LogInfo("OnPluginEnable", string.Format("^b{0} ^1Enabled", this.GetPluginName()));

			if (this._enumSpamProtection == enumBoolYesNo.Yes)
            {
				this._spamProtection = new SpamProtection(this._numberOfAllowedRequests);
				this.LogInfo("OnPluginEnable", string.Format("^2Spam protection set to {0} requests per round for each player", this._numberOfAllowedRequests));
			}

			this.RegisterAllCommands();
		}

		public void OnPluginDisable()
		{
			this._isStreaming = false;

			if (this._conn.State == ConnectionState.Open)
            {
				try
                {
					this._conn.Close();
                }
				catch (Exception e)
                {
					this.LogError("OnPluginDisable", e.Message);
                }
            }

			try
			{
				MySqlConnection.ClearAllPools();
			}
			catch (Exception e)
			{
				this.LogError("OnPluginDisable", e.Message);
			}

			this.LogInfo("OnPluginDisable", string.Format("^b{0} ^1Disabled", this.GetPluginName()));

			this._isPluginEnabled = false;
			this.UnregisterAllCommands();
		}

		public void OnPluginLoaded(string hostName, string port, string PRoConVersion)
		{
			this._serverHostName = hostName;
			this._serverPort = port;
			this._serverPRoConVersion = PRoConVersion;
			this.RegisterEvents(this.GetType().Name, "OnListPlayers","OnPlayerAuthenticated", "OnPlayerJoin", "OnGlobalChat", "OnTeamChat", "OnSquadChat", "OnPunkbusterMessage", "OnPunkbusterPlayerInfo", "OnServerInfo", "OnLevelLoaded",
													 "OnPlayerKilled", "OnPlayerLeft", "OnRoundOverPlayers", "OnPlayerSpawned", "OnLoadingLevel", "OnCommandStats", "OnCommandTop10", "OnCommandDogtags", "OnCommandServerStats",
													 "OnRoundStartPlayerCount", "OnRoundRestartPlayerCount", "OnRoundOver");

			this.RegisterCommand(_loggerStatusCommand);
			this.MaybeCreateGame();
		}

		// Non-official callback

		public void OnPluginLoadingEnv(List<string> pluginEnv) => this._game = pluginEnv[1].ToUpper();

		// PRoConPluginAPI callbacks

		public override void OnPlayerJoin(string soldierName)
		{
			if (!this._statsTracker.ContainsKey(soldierName))
			{
				var stats = new Stats(String.Empty, 0, 0, 0, 0, 0, 0, 0, this._offset, this._usedWeaponDictionary);
				this._statsTracker.Add(soldierName, stats);
				ThreadPool.QueueUserWorkItem(delegate { this.CreateSession(soldierName); });
			}

			if (this._didRoundStart && this._statsTracker.ContainsKey(soldierName))
			{
				if (!this._statsTracker[soldierName].IsOnline)
				{
					this._statsTracker[soldierName].PlayerJoinedAt = new DateTimeWithOffset(this._offset).Now;
					this._statsTracker[soldierName].IsOnline = true;
				}
			}

			this._mapStats.PlayerJoined++;

			if (this._enumStatsWelcomeLogging == enumBoolYesNo.Yes)
			{
				if (!this._welcomeStatsDictionary.ContainsKey(soldierName))
				{
					this.LogDebug("OnPlayerJoin", string.Format("Added player {0} to welcome stats list", soldierName));
					this._welcomeStatsDictionary.Add(soldierName, new DateTimeWithOffset(this._offset).Now);
					return;
				}

				this._welcomeStatsDictionary[soldierName] = new DateTimeWithOffset(this._offset).Now;
			}
		}

		public override void OnPlayerAuthenticated(string soldierName, string guid)
		{
			if (!this._statsTracker.ContainsKey(soldierName))
			{
				var stats = new Stats(guid, 0, 0, 0, 0, 0, 0, 0, this._offset, this._usedWeaponDictionary);
				this._statsTracker.Add(soldierName, stats);
				return;
			}

			this._statsTracker[soldierName].Guid = guid;
		}

		public override void OnGlobalChat(string speaker, string message)
		{
			if (message.Length > 0)
				ThreadPool.QueueUserWorkItem(delegate { this.LogChat(speaker, message, "Global"); });
		}

		public override void OnTeamChat(string speaker, string message, int teamId)
		{
			if (message.Length > 0)
				ThreadPool.QueueUserWorkItem(delegate { this.LogChat(speaker, message, "Team"); });
		}

		public override void OnSquadChat(string speaker, string message, int teamId, int squadId)
		{
			if (message.Length > 0)
				ThreadPool.QueueUserWorkItem(delegate { this.LogChat(speaker, message, "Squad"); });
		}

        public override void OnPunkbusterMessage(string punkbusterMessage)
        {
            base.OnPunkbusterMessage(punkbusterMessage);
        }

        public override void OnPunkbusterPlayerInfo(CPunkbusterInfo playerInfo)
        {
            base.OnPunkbusterPlayerInfo(playerInfo);
        }

		public override void OnServerInfo(CServerInfo serverInfo)
		{
			this._mapStats.GameMode = serverInfo.GameMode;
			this._mapStats.PlayerCountList.Add(serverInfo.PlayerCount);

			var isMapLoadedAt = this._mapStats.MapLoadedAt == DateTime.MinValue || this._mapStats.MapLoadedAt == null;

			if (serverInfo.PlayerCount >= this._roundStartCount && isMapLoadedAt)
				this._mapStats.MapLoadedAt = DateTime.Now;

			this._mapStats.MapName = serverInfo.Map;
			this._mapStats.Round = serverInfo.CurrentRound;
			this._mapStats.NumberOfRounds = serverInfo.TotalRounds;
			this._mapStats.ServerPlayerMax = serverInfo.MaxPlayerCount;
			this._serverName = serverInfo.ServerName;

			var secondsFromLastUpdate = 0D;

			if (this._lastServerUpdatedAt != null)
				secondsFromLastUpdate = DateTime.Now.Subtract(this._lastServerUpdatedAt.Value).TotalSeconds;

			if (this._serverID == 0)
            {
				this._lastServerUpdatedAt = DateTime.Now;
				ThreadPool.QueueUserWorkItem(delegate { this.MaybeCreateServer(serverInfo); });
			}
			else if (this._lastServerUpdatedAt == null)
            {
				this._lastServerUpdatedAt = DateTime.Now;
				ThreadPool.QueueUserWorkItem(delegate { this.MaybeCreateServer(serverInfo); });
			}
			else if (this._serverInfoDelay <= secondsFromLastUpdate)
			{
				this._lastServerUpdatedAt = DateTime.Now;
				ThreadPool.QueueUserWorkItem(delegate { this.MaybeCreateServer(serverInfo); });
			}
		}

        public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset subset)
        {
            base.OnListPlayers(players, subset);
        }

		public override void OnPlayerSpawned(string soldierName, Inventory spawnedInventory)
		{
			base.OnPlayerSpawned(soldierName, spawnedInventory);
		}

		public override void OnPlayerKilled(Kill kKillerVictimDetails)
        {
            base.OnPlayerKilled(kKillerVictimDetails);
        }

        public override void OnPlayerLeft(CPlayerInfo playerInfo)
        {
            base.OnPlayerLeft(playerInfo);
        }

        public override void OnRoundOverPlayers(List<CPlayerInfo> players)
        {
            base.OnRoundOverPlayers(players);
        }

        public override void OnRoundOver(int winningTeamId)
        {
            base.OnRoundOver(winningTeamId);
        }

		public override void OnRoundStartPlayerCount(int limit) => this._roundStartCount = limit;

        public override void OnRoundRestartPlayerCount(int limit) => this._roundRestartCount = limit;

        public override void OnLevelLoaded(string mapFileName, string gamemode, int roundsPlayed, int roundsTotal)
        {
            base.OnLevelLoaded(mapFileName, gamemode, roundsPlayed, roundsTotal);
        }

        // Test only (if DEBUG flag is active)

        #region Test only methods
#if DEBUG
        public Dictionary<string, Stats> __StatsTracker__ { get { return this._statsTracker; } }
		public Dictionary<string, DateTime> __WelcomeStatsDictionary__ { get { return this._welcomeStatsDictionary; } }
		public int? __GetCurrentRankFromPlayer__(string soldierName) => this.GetRank(soldierName);

		public void __CleanDatabase__()
		{
			if (!this._isDatabaseReady) return;

			var statements = new string[]
			{
				// Remove Foreign Key validation
				"SET FOREIGN_KEY_CHECKS = 0",

				// Tables
				"TRUNCATE TABLE `tbl_teamscores`",
				"TRUNCATE TABLE `tbl_dogtags`",
				"TRUNCATE TABLE `tbl_weapons_stats`",
				"TRUNCATE TABLE `tbl_weapons`",
				"TRUNCATE TABLE `tbl_currentplayers`",
				"TRUNCATE TABLE `tbl_sessions`",
				"TRUNCATE TABLE `tbl_playerrank`",
				"TRUNCATE TABLE `tbl_playerstats`",
				"TRUNCATE TABLE `tbl_server_stats`",
				"TRUNCATE TABLE `tbl_server_player`",
				"TRUNCATE TABLE `tbl_playerdata`",
				"TRUNCATE TABLE `tbl_mapstats`",
				"TRUNCATE TABLE `tbl_chatlog`",
				"TRUNCATE TABLE `tbl_server`",
				"TRUNCATE TABLE `tbl_games`",

				// Add Foreign Key validation
				"SET FOREIGN_KEY_CHECKS = 1"
			};

			try
			{
				lock (this._tableBuilderLock)
				{
					if (this._conn == null) this._conn = new MySqlConnection(this.BuildConnectionString());
					if (this._conn.State != ConnectionState.Open) this._conn.Open();
					if (this._conn.State != ConnectionState.Open) return;

					this._transaction = this._conn.BeginTransaction();

					foreach (var statement in statements)
					{
						var command = new MySqlCommand(statement, this._conn, this._transaction);
						command.ExecuteNonQuery();
					}
				}
			}
			catch (MySqlException e)
			{
				this.LogError("__CleanDatabase__", e.Message);

				if (this._transaction != null) this._transaction.Rollback();
				if (this._conn != null && this._conn.State == ConnectionState.Open)
					this._conn.Dispose();

				this._transaction = null;
			}
			catch (Exception e)
			{
				this.LogError("__CleanDatabase__", e.Message);

				if (this._transaction != null) this._transaction.Rollback();
				if (this._conn != null && this._conn.State == ConnectionState.Open)
					this._conn.Close();

				this._transaction = null;
			}
			finally
			{
				if (this._transaction != null) this._transaction.Commit();
				if (this._conn != null && this._conn.State == ConnectionState.Open)
					this._conn.Close();

				this._transaction = null;
			}
		}

		public int? __GetPlayerIDFromPlayer__(string soldierName, int gameID)
		{
			if (!this._isDatabaseReady) return null;

			var parameters = new Dictionary<string, object>();

			parameters.Add("SoldierName", soldierName);
			parameters.Add("GameID", gameID);

			var query = $"SELECT PlayerID FROM tbl_playerdata WHERE GameID = @GameID AND SoldierName = @SoldierName";
			var result = this.ReadFromQuery(query, parameters);

			if (result == null) return null;
			if (result.Count == 1) return Convert.ToInt32(result[0]["PlayerID"]);

			return null;
		}

		public int? __GetStatsIDFromPlayerID__(int serverID, int playerID)
		{
			if (!this._isDatabaseReady) return null;

			var parameters = new Dictionary<string, object>();

			parameters.Add("ServerID", serverID);
			parameters.Add("PlayerID", playerID);

			var query = $"SELECT StatsID FROM tbl_server_player WHERE ServerID = @ServerID AND PlayerID = @PlayerID";
			var result = this.ReadFromQuery(query, parameters);

			if (result == null) return null;
			if (result.Count == 1) return Convert.ToInt32(result[0]["StatsID"]);

			return null;
		}

		public List<Dictionary<string, object>> __GetChatLogFromPlayer__(string soldierName)
		{
			if (!this._isDatabaseReady) return null;

			var parameters = new Dictionary<string, object>();

			parameters.Add("SoldierName", soldierName);

			var query = $"SELECT * FROM tbl_chatlog WHERE logSoldierName = @SoldierName";
			var result = this.ReadFromQuery(query, parameters);

			if (result == null) return null;
			if (result.Count >= 1) return result;

			return null;
		}

		public int? __GetGameID__(string gameName)
		{
			if (!this._isDatabaseReady) return null;
			if (this._gameID != 0) return this._gameID;

			var parameters = new Dictionary<string, object>();
			parameters.Add("Name", this._game);

			var query = "SELECT `GameID` FROM tbl_games WHERE `Name` = @Name";
			var result = this.ReadFromQuery(query, parameters);

			if (result == null) return null;
			if (result.Count == 1) return Convert.ToInt32(result[0]["GameID"]);

			return null;
		}

		public int? __GetServerID__(string ipAddress)
		{
			if (!this._isDatabaseReady) return null;
			if (this._serverID != 0) return this._serverID;

			var result = this.__GetServer__(ipAddress);

			if (result == null) return null;
			if (result.Count == 1) return Convert.ToInt32(result["ServerID"]);

			return null;
		}

		public Dictionary<string, object> __GetServer__(string ipAddress)
		{
			if (!this._isDatabaseReady) return null;

			var parameters = new Dictionary<string, object>();
			parameters.Add("IP_Address", ipAddress);

			var query = "SELECT * FROM tbl_server WHERE IP_Address = @IP_Address";
			var result = this.ReadFromQuery(query, parameters);

			if (result == null) return null;
			if (result.Count == 1) return result[0];

			return null;
		}

		public void __SetServerID__(int serverID)
		{
			if (serverID > 0) this._serverID = serverID;
		}

		public void __SetGameID__(int gameID)
		{
			if (gameID > 0) this._gameID = gameID;
		}

		public long? __InsertData__(string table, Dictionary<string, object> keyValue) => this.InsertData(table, keyValue, this._tableBuilderLock);
#endif
		#endregion

		// Thread methods

		private void LogChat(string speaker, string message, string type)
        {
			if (this._enumChatLogging == enumBoolYesNo.No) return;
			if (this._enumChatLoggingServerSpam == enumBoolYesNo.No && speaker.CompareTo("Server") == 0) return;
			if (this._enumChatLoggingServerSpam == enumBoolYesNo.No && speaker.CompareTo("Server") == 0) return;

			if (this._enumChatLoggingFilter == enumBoolYesNo.Yes)
			{
				foreach (var regexRule in this._chatRegexFilterRuleList)
				{
					if (regexRule.IsMatch(message))
					{
						this.LogDebug("LogChat", string.Format("'{0}' was filtered out by the RegEx rule: {1}", speaker, regexRule.ToString()));
						return;
					}
				}
			}

			if (this._enumInstantChatLogging == enumBoolYesNo.Yes)
			{
				var keyValue = new Dictionary<string, object>();

				keyValue.Add("logDate", new DateTimeWithOffset(this._offset).Now);
				keyValue.Add("ServerID", this._serverID);
				keyValue.Add("logSubset", type);
				keyValue.Add("logSoldierName", speaker);
				keyValue.Add("logMessage", message);

#if DEBUG
				var result = this.InsertData("tbl_chatlog", keyValue, this._chatLogLock);
				if (!result.HasValue) throw new NullReferenceException("tbl_chatlog.ID is null");
#else
				this.InsertData("tbl_chatlog", keyValue, this._chatLogLock);
#endif
				return;
			}

			var chatLog = new ChatLog(new DateTimeWithOffset(this._offset).Now, speaker, message, type);
			this._chatLogTracker.Add(chatLog);
		}

		private void CreateSession(string soldierName, int score = 0, string guid = "")
        {
			if (this._serverID == 0) return;

			try
			{
				if (this._enumSession == enumBoolYesNo.Yes)
				{
					lock (this._sessionLock)
					{
						if (!this._sessionTracker.ContainsKey(soldierName))
						{
							this.LogDebug("CreateSession", $"Session for player {soldierName} created");
							this._sessionTracker.Add(soldierName, new Stats(guid, score, 0, 0, 0, 0, 0, 0, this._offset, this._usedWeaponDictionary));

							var rank = this.GetRank(soldierName);
							if (rank != null) this._sessionTracker[soldierName].Rank = rank.Value;
						}
					}
				}
			}
			catch (Exception e)
			{
				this.LogDebug("CreateSession", e.Message);
			}
			finally
			{
				lock (this._sessionLock)
				{
					if (this._sessionTracker.ContainsKey(soldierName) && this._enumSession == enumBoolYesNo.Yes)
					{
						if (score != 0) this._sessionTracker[soldierName].AddScore(score);
						if (guid.Length > 2) this._sessionTracker[soldierName].EAGuid = guid;
					}
				}
			}
		}

		private void MaybeCreateServer(CServerInfo serverInfo)
		{
			if (!this._isDatabaseReady)
			{
				this.LogError("MaybeCreateServer", "Database isn't connected yet");
				return;
			}

			this.LogDebug("MaybeCreateServer", "Executing the ServerID query");

			var query = "SELECT `ServerID` FROM tbl_server WHERE IP_Address = @IP_Address";
			var ipAddress = $"{this._serverHostName}:{this._serverPort}";

			var queryKeyValue = new Dictionary<string, object>();
			queryKeyValue.Add("IP_Address", ipAddress);

			var result = this.ReadFromQuery(query, queryKeyValue);
			if (result == null) this._serverID = 0;
			if (result.Count == 1) this._serverID = Convert.ToInt32(result[0]["ServerID"]);

			var statementKeyValue = new Dictionary<string, object>();

			statementKeyValue.Add("IP_Address", ipAddress);
			statementKeyValue.Add("ServerName", serverInfo.ServerName);
			statementKeyValue.Add("ServerGroup", this._serverGroup);
			statementKeyValue.Add("UsedSlots", serverInfo.PlayerCount);
			statementKeyValue.Add("MaxSlots", serverInfo.MaxPlayerCount);
			statementKeyValue.Add("MapName", serverInfo.Map);
			statementKeyValue.Add("GameID", this._gameID);
			statementKeyValue.Add("GameMode", serverInfo.GameMode);

			if (this._serverID == 0)
				this._serverID = Convert.ToInt32(this.InsertData("tbl_server", statementKeyValue));

			if (this._serverID != 0)
			{
				statementKeyValue.Remove("IP_Address");
				this.UpdateData("tbl_server", statementKeyValue, queryKeyValue);
			}

			this.MaybeUpdateCurrentPlayerStatsTable(serverInfo);
		}

		private void MaybeUpdateCurrentPlayerStatsTable(CServerInfo serverInfo)
		{
			if (!this._isDatabaseReady)
			{
				this.LogError("MaybeUpdateCurrentPlayerStatsTable", "Database isn't connected yet");
				return;
			}

			this.LogDebug("MaybeUpdateCurrentPlayerStatsTable", "Checking if AdKatsLogger should update team scores");

			if (this._serverID == 0 || this._enumStatsRealTimeScoreboard == enumBoolYesNo.No || serverInfo.TeamScores.Count == 0)
				return;

			var queryKeyValue = new Dictionary<string, object>();
			queryKeyValue.Add("ServerID", this._serverID);

			this.DeleteData("tbl_teamscores", queryKeyValue);

			foreach (var teamScore in serverInfo.TeamScores)
            {
				var statementKeyValue = new Dictionary<string, object>();

				statementKeyValue.Add("ServerID", this._serverID);
				statementKeyValue.Add("TeamID", teamScore.TeamID);
				statementKeyValue.Add("Score", teamScore.Score);
				statementKeyValue.Add("WinningScore", teamScore.WinningScore);

				this.InsertData("tbl_teamscores", statementKeyValue);
			}
		}

		// Database methods

		private void CreateTables()
        {
            if (string.IsNullOrEmpty(this._dbHost) ||
				string.IsNullOrEmpty(this._dbPort) ||
				string.IsNullOrEmpty(this._dbUsername) ||
				string.IsNullOrEmpty(this._dbPassword) ||
				string.IsNullOrEmpty(this._dbName)) return;

			var statements = new string[]
			{
				// tbl_games
				@"CREATE TABLE IF NOT EXISTS `tbl_games` (
                    `GameID` tinyint(4) unsigned NOT NULL AUTO_INCREMENT,
                    `Name` varchar(45) DEFAULT NULL,
                    PRIMARY KEY (`GameID`),
                    UNIQUE KEY `name_unique` (`Name`)
                 ) ENGINE = InnoDB",

				// tbl_server
				@"CREATE TABLE IF NOT EXISTS `tbl_server` (
                    `ServerID` SMALLINT UNSIGNED NOT NULL AUTO_INCREMENT,
                    `ServerGroup` TINYINT UNSIGNED NOT NULL DEFAULT 0,
                    `IP_Address` VARCHAR(45) NULL DEFAULT NULL,
                    `ServerName` VARCHAR(200) NULL DEFAULT NULL,
                    `GameID` tinyint(4)unsigned NOT NULL DEFAULT '0',
                    `UsedSlots` SMALLINT UNSIGNED NULL DEFAULT 0,
                    `MaxSlots` SMALLINT UNSIGNED NULL DEFAULT 0,
                    `MapName` VARCHAR(45) NULL DEFAULT NULL,
                    `FullMapName` TEXT NULL DEFAULT NULL,
                    `GameMode` VARCHAR(45) NULL DEFAULT NULL,
                    `GameMod` VARCHAR(45) NULL DEFAULT NULL,
                    `PunkbusterVersion` VARCHAR(45) NULL DEFAULT NULL,
                    `ConnectionState` VARCHAR(45) NULL DEFAULT NULL,
                    PRIMARY KEY (`ServerID`),
                    INDEX `INDEX_SERVERGROUP` (`ServerGroup` ASC),
                    UNIQUE INDEX `IP_Address_UNIQUE` (`IP_Address` ASC),
                    CONSTRAINT `fk_tbl_server_tbl_games` FOREIGN KEY (`GameID`) REFERENCES `tbl_games` (`GameID`) ON DELETE CASCADE ON UPDATE NO ACTION
                 ) ENGINE = InnoDB",

				// tbl_chatlog
				@"CREATE TABLE IF NOT EXISTS `tbl_chatlog` (
                    `ID` INT NOT NULL AUTO_INCREMENT,
                    `logDate` DATETIME NULL DEFAULT NULL,
                    `ServerID` SMALLINT UNSIGNED NOT NULL,
                    `logSubset` VARCHAR(45) NULL DEFAULT NULL,
                    `logSoldierName` VARCHAR(45) NULL DEFAULT NULL,
                    `logMessage` TEXT NULL DEFAULT NULL,
                    PRIMARY KEY (`ID`),
                    INDEX `INDEX_SERVERID` (`ServerID` ASC),
                    INDEX `INDEX_logDate` (`logDate` ASC),
                    CONSTRAINT `fk_tbl_chatlog_tbl_server` FOREIGN KEY (`ServerID`) REFERENCES `tbl_server` (`ServerID`) ON DELETE CASCADE ON UPDATE NO ACTION
                 ) ENGINE = InnoDB",

				// tbl_mapstats
				@"CREATE TABLE IF NOT EXISTS `tbl_mapstats` (
                    `ID` INT NOT NULL AUTO_INCREMENT,
                    `ServerID` SMALLINT UNSIGNED NOT NULL DEFAULT '0',
                    `TimeMapLoad` DATETIME NULL DEFAULT NULL,
                    `TimeRoundStarted` DATETIME NULL DEFAULT NULL,
                    `TimeRoundEnd` DATETIME NULL DEFAULT NULL,
                    `MapName` VARCHAR(45) NULL DEFAULT NULL,
                    `Gamemode` VARCHAR(45) NULL DEFAULT NULL,
                    `Roundcount` SMALLINT NOT NULL DEFAULT '0',
                    `NumberofRounds` SMALLINT NOT NULL DEFAULT '0',
                    `MinPlayers` SMALLINT NOT NULL DEFAULT '0',
                    `AvgPlayers` DOUBLE NOT NULL DEFAULT '0',
                    `MaxPlayers` SMALLINT NOT NULL DEFAULT '0',
                    `PlayersJoinedServer` SMALLINT NOT NULL DEFAULT '0',
                    `PlayersLeftServer` SMALLINT NOT NULL DEFAULT '0',
                    PRIMARY KEY (`ID`),
                    INDEX `ServerID_INDEX` (`ServerID` ASC),
                    CONSTRAINT `fk_tbl_mapstats_tbl_server` FOREIGN KEY (`ServerID`) REFERENCES `tbl_server` (`ServerID`) ON DELETE CASCADE ON UPDATE NO ACTION
                 ) ENGINE = InnoDB",

				// tbl_playerdata
				@"CREATE TABLE IF NOT EXISTS `tbl_playerdata` (
                    `PlayerID` INT UNSIGNED NOT NULL AUTO_INCREMENT,
                    `GameID` tinyint(4)unsigned NOT NULL DEFAULT '0',
                    `ClanTag` VARCHAR(10) NULL DEFAULT NULL,
                    `SoldierName` VARCHAR(45) NULL DEFAULT NULL,
                    `GlobalRank` SMALLINT UNSIGNED NOT NULL DEFAULT '0',
                    `PBGUID` VARCHAR(32) NULL DEFAULT NULL,
                    `EAGUID` VARCHAR(35) NULL DEFAULT NULL,
                    `IP_Address` VARCHAR(15) NULL DEFAULT NULL,
                    `IPv6_Address` VARBINARY(16) NULL DEFAULT NULL,
                    `CountryCode` VARCHAR(2) NULL DEFAULT NULL,
                    PRIMARY KEY (`PlayerID`),
                    UNIQUE INDEX `UNIQUE_playerdata` (`GameID` ASC, `EAGUID` ASC),
                    INDEX `INDEX_SoldierName` (`SoldierName` ASC),
                    CONSTRAINT `fk_tbl_playerdata_tbl_games` FOREIGN KEY (`GameID`) REFERENCES `tbl_games` (`GameID`) ON DELETE CASCADE ON UPDATE NO ACTION
                 ) ENGINE = InnoDB",

				// tbl_server_player
				@"CREATE TABLE IF NOT EXISTS `tbl_server_player` (
                    `StatsID` INT UNSIGNED NOT NULL AUTO_INCREMENT,
                    `ServerID` SMALLINT UNSIGNED NOT NULL,
                    `PlayerID` INT UNSIGNED NOT NULL,
                    PRIMARY KEY (`StatsID`),
                    UNIQUE INDEX `UNIQUE_INDEX` (`ServerID` ASC, `PlayerID` ASC),
                    INDEX `fk_tbl_server_player_tbl_playerdata` (`PlayerID` ASC),
                    INDEX `fk_tbl_server_player_tbl_server` (`ServerID` ASC),
                    CONSTRAINT `fk_tbl_server_player_tbl_playerdata` FOREIGN KEY (`PlayerID`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE CASCADE ON UPDATE NO ACTION,
                    CONSTRAINT `fk_tbl_server_player_tbl_server` FOREIGN KEY (`ServerID`) REFERENCES `tbl_server` (`ServerID`) ON DELETE CASCADE ON UPDATE NO ACTION
                 ) ENGINE = InnoDB",

				// tbl_server_stats
				@"CREATE TABLE IF NOT EXISTS `tbl_server_stats` (
                    `ServerID` SMALLINT(5) UNSIGNED NOT NULL,
                    `CountPlayers` BIGINT NOT NULL DEFAULT 0,
                    `SumScore` BIGINT NOT NULL DEFAULT 0,
                    `AvgScore` FLOAT NOT NULL DEFAULT 0,
                    `SumKills` BIGINT NOT NULL DEFAULT 0,
                    `AvgKills` FLOAT NOT NULL DEFAULT 0,
                    `SumHeadshots` BIGINT NOT NULL DEFAULT 0,
                    `AvgHeadshots` FLOAT NOT NULL DEFAULT 0,
                    `SumDeaths` BIGINT NOT NULL DEFAULT 0,
                    `AvgDeaths` FLOAT NOT NULL DEFAULT 0,
                    `SumSuicide` BIGINT NOT NULL DEFAULT 0,
                    `AvgSuicide` FLOAT NOT NULL DEFAULT 0,
                    `SumTKs` BIGINT NOT NULL DEFAULT 0,
                    `AvgTKs` FLOAT NOT NULL DEFAULT 0,
                    `SumPlaytime` BIGINT NOT NULL DEFAULT 0,
                    `AvgPlaytime` FLOAT NOT NULL DEFAULT 0,
                    `SumRounds` BIGINT NOT NULL DEFAULT 0,
                    `AvgRounds` FLOAT NOT NULL DEFAULT 0,
                    PRIMARY KEY (`ServerID`),
                    INDEX `fk_tbl_server_stats_tbl_server` (`ServerID` ASC),
                    CONSTRAINT `fk_tbl_server_stats_tbl_server` FOREIGN KEY (`ServerID`) REFERENCES `tbl_server` (`ServerID`) ON DELETE CASCADE ON UPDATE NO ACTION
                 ) ENGINE = InnoDB",

				// tbl_playerstats
				@"CREATE TABLE IF NOT EXISTS `tbl_playerstats` (
                    `StatsID` INT UNSIGNED NOT NULL,
                    `Score` INT NOT NULL DEFAULT '0',
                    `Kills` INT UNSIGNED NOT NULL DEFAULT '0',
                    `Headshots` INT UNSIGNED NOT NULL DEFAULT '0',
                    `Deaths` INT UNSIGNED NOT NULL DEFAULT '0',
                    `Suicide` INT UNSIGNED NOT NULL DEFAULT '0',
                    `TKs` INT UNSIGNED NOT NULL DEFAULT '0',
                    `Playtime` INT UNSIGNED NOT NULL DEFAULT '0',
                    `Rounds` INT UNSIGNED NOT NULL DEFAULT '0',
                    `FirstSeenOnServer` DATETIME NULL DEFAULT NULL,
                    `LastSeenOnServer` DATETIME NULL DEFAULT NULL,
                    `Killstreak` SMALLINT UNSIGNED NOT NULL DEFAULT '0',
                    `Deathstreak` SMALLINT UNSIGNED NOT NULL DEFAULT '0',
                    `HighScore` MEDIUMINT UNSIGNED NOT NULL DEFAULT '0',
                    `rankScore` INT UNSIGNED NOT NULL DEFAULT '0',
                    `rankKills` INT UNSIGNED NOT NULL DEFAULT '0',
                    `Wins` INT UNSIGNED NOT NULL DEFAULT '0',
                    `Losses` INT UNSIGNED NOT NULL DEFAULT '0',
                    PRIMARY KEY (`StatsID`),
                    INDEX `INDEX_Score` (`Score`),
                    KEY `INDEX_RANK_SCORE` (`rankScore`),
                    KEY `INDEX_RANK_KILLS` (`rankKills`),
                    CONSTRAINT `fk_tbl_playerstats_tbl_server_player1` FOREIGN KEY (`StatsID`) REFERENCES `tbl_server_player` (`StatsID`) ON DELETE CASCADE ON UPDATE NO ACTION
                 ) ENGINE = InnoDB",

				// tbl_playerrank
				@"CREATE TABLE IF NOT EXISTS `tbl_playerrank` (
                    `PlayerID` INT UNSIGNED NOT NULL DEFAULT 0,
                    `ServerGroup` SMALLINT UNSIGNED NOT NULL DEFAULT 0,
                    `rankScore` INT UNSIGNED NOT NULL DEFAULT 0,
                    `rankKills` INT UNSIGNED NOT NULL DEFAULT 0,
                    INDEX `INDEX_SCORERANKING` (`rankScore` ASC),
                    INDEX `INDEX_KILLSRANKING` (`rankKills` ASC),
                    PRIMARY KEY (`PlayerID`, `ServerGroup`),
                    CONSTRAINT `fk_tbl_playerrank_tbl_playerdata` FOREIGN KEY (`PlayerID`) REFERENCES `tbl_playerdata` (`PlayerID`) ON DELETE CASCADE ON UPDATE NO ACTION
                 ) ENGINE = InnoDB",

				// tbl_sessions
				@"CREATE TABLE IF NOT EXISTS `tbl_sessions` (
                    `SessionID` INT UNSIGNED NOT NULL AUTO_INCREMENT,
                    `StatsID` INT UNSIGNED NOT NULL,
                    `StartTime` DATETIME NOT NULL,
                    `EndTime` DATETIME NOT NULL,
                    `Score` MEDIUMINT NOT NULL DEFAULT '0',
                    `Kills` SMALLINT(5) UNSIGNED NOT NULL DEFAULT '0',
                    `Headshots` SMALLINT(5) UNSIGNED NOT NULL DEFAULT '0',
                    `Deaths` SMALLINT(5) UNSIGNED NOT NULL DEFAULT '0',
                    `TKs` SMALLINT(5) UNSIGNED NOT NULL DEFAULT '0',
                    `Suicide` SMALLINT(5) UNSIGNED NOT NULL DEFAULT '0',
                    `RoundCount` TINYINT UNSIGNED NOT NULL DEFAULT '0',
                    `Playtime` MEDIUMINT UNSIGNED NOT NULL DEFAULT '0',
                    `Killstreak` SMALLINT(5) UNSIGNED NOT NULL DEFAULT '0' ,
                    `Deathstreak` SMALLINT(5) UNSIGNED NOT NULL DEFAULT '0' ,
                    `HighScore` MEDIUMINT UNSIGNED NOT NULL DEFAULT '0' ,
                    `Wins` TINYINT UNSIGNED NOT NULL DEFAULT '0' ,
                    `Losses` TINYINT UNSIGNED NOT NULL DEFAULT '0' ,
                    PRIMARY KEY (`SessionID`),
                    INDEX `INDEX_STATSID` (`StatsID` ASC),
                    INDEX `INDEX_STARTTIME` (`StartTime` ASC),
                    CONSTRAINT `fk_tbl_sessions_tbl_server_player` FOREIGN KEY (`StatsID`) REFERENCES `tbl_server_player` (`StatsID`) ON DELETE CASCADE ON UPDATE NO ACTION
                 ) ENGINE = InnoDB",

				// tbl_currentplayers
				@"CREATE TABLE IF NOT EXISTS `tbl_currentplayers` (
                    `ServerID` SMALLINT UNSIGNED NOT NULL,
                    `SoldierName` varchar(45) NOT NULL,
                    `GlobalRank` SMALLINT UNSIGNED NOT NULL DEFAULT '0',
                    `ClanTag` varchar(45) DEFAULT NULL,
                    `Score` int(11) NOT NULL DEFAULT '0',
                    `Kills` int(11) NOT NULL DEFAULT '0',
                    `Headshots` int(11) NOT NULL DEFAULT '0',
                    `Deaths` int(11) NOT NULL DEFAULT '0',
                    `Suicide` int(11) DEFAULT NULL,
                    `Killstreak` smallint(6) DEFAULT '0',
                    `Deathstreak` smallint(6) DEFAULT '0',
                    `TeamID` tinyint(4) DEFAULT NULL,
                    `SquadID` tinyint(4) DEFAULT NULL,
                    `EA_GUID` varchar(45) NOT NULL DEFAULT '',
                    `PB_GUID` varchar(45) NOT NULL DEFAULT '',
                    `IP_Address` VARCHAR(15) NULL DEFAULT NULL,
                    `IPv6_Address` VARBINARY(16) NULL DEFAULT NULL,
                    `CountryCode` varchar(2) DEFAULT '',
                    `Ping` smallint(6) DEFAULT NULL,
                    `PlayerJoined` datetime DEFAULT NULL,
                    PRIMARY KEY (`ServerID`, `SoldierName`),
                    CONSTRAINT `fk_tbl_currentplayers_tbl_server` FOREIGN KEY (`ServerID`) REFERENCES `tbl_server` (`ServerID`) ON DELETE CASCADE ON UPDATE NO ACTION
                 ) ENGINE = InnoDB",

				// tbl_weapons
				@"CREATE TABLE IF NOT EXISTS `tbl_weapons` (
                    `WeaponID` int(11) unsigned NOT NULL AUTO_INCREMENT,
                    `GameID` tinyint(4)unsigned NOT NULL,
                    `FriendlyName` varchar(45) DEFAULT NULL,
                    `FullName` varchar(100) DEFAULT NULL,
                    `Aliases` varchar(255) DEFAULT NULL,
                    `DamageType` varchar(45) DEFAULT NULL,
                    `Slot` varchar(45) DEFAULT NULL,
                    `KitRestriction` varchar(45) DEFAULT NULL,
                    PRIMARY KEY (`WeaponID`),
                    UNIQUE KEY `unique` (`GameID`, `FullName`),
                    CONSTRAINT `fk_tbl_weapons_tbl_games` FOREIGN KEY (`GameID`) REFERENCES `tbl_games` (`GameID`) ON DELETE CASCADE ON UPDATE NO ACTION
                 ) ENGINE = InnoDB",

				// tbl_weapons_stats
				@"CREATE TABLE IF NOT EXISTS `tbl_weapons_stats` (
                    `StatsID` INT unsigned NOT NULL,
                    `WeaponID` int(11) unsigned NOT NULL,
                    `Kills` int(11) unsigned NOT NULL DEFAULT '0',
                    `Headshots` int(11) unsigned NOT NULL DEFAULT '0',
                    `Deaths` int(11) unsigned NOT NULL DEFAULT '0',
                    PRIMARY KEY (`StatsID`, `WeaponID`),
                    KEY `Kills_Death_idx` (`Kills`, `Deaths`),
                    KEY `Kills_Head_idx` (`Kills`, `Headshots`),
                    CONSTRAINT `fk_tbl_weapons_stats_tbl_server_player` FOREIGN KEY (`StatsID`) REFERENCES `tbl_server_player` (`StatsID`) ON DELETE CASCADE ON UPDATE NO ACTION,
                    CONSTRAINT `fk_tbl_weapons_stats_tbl_weapons` FOREIGN KEY (`WeaponID`) REFERENCES `tbl_weapons` (`WeaponID`) ON DELETE CASCADE ON UPDATE NO ACTION
                 ) ENGINE = InnoDB",

				// tbl_dogtags
				@"CREATE TABLE IF NOT EXISTS `tbl_dogtags` (
                    `KillerID` INT UNSIGNED NOT NULL,
                    `VictimID` INT UNSIGNED NOT NULL,
                    `Count` SMALLINT UNSIGNED NOT NULL DEFAULT '0',
                    PRIMARY KEY (`KillerID`, `VictimID`),
                    CONSTRAINT `fk_killer_tbl_dogtags_tbl_server_player` FOREIGN KEY (`KillerID`) REFERENCES `tbl_server_player` (`StatsID`) ON DELETE CASCADE ON UPDATE NO ACTION,
                    CONSTRAINT `fk_victim_tbl_dogtags_tbl_server_player` FOREIGN KEY (`VictimID`) REFERENCES `tbl_server_player` (`StatsID`) ON DELETE CASCADE ON UPDATE NO ACTION
                 ) ENGINE = InnoDB",

				// tbl_teamscores
				@"CREATE TABLE IF NOT EXISTS `tbl_teamscores` (
                    `ServerID` smallint(5) unsigned NOT NULL,
                    `TeamID` smallint(5) unsigned NOT NULL,
                    `Score` int(11) DEFAULT NULL,
                    `WinningScore` int(11) DEFAULT NULL,
                    PRIMARY KEY (`ServerID`, `TeamID`),
                    CONSTRAINT `fk_tbl_teamscores_tbl_server` FOREIGN KEY (`ServerID`) REFERENCES `tbl_server` (`ServerID`) ON DELETE CASCADE ON UPDATE NO ACTION
                 ) ENGINE = InnoDB"
			};

			try
            {
				if (this._conn == null) this._conn = new MySqlConnection(this.BuildConnectionString());
				if (this._conn.State != ConnectionState.Open) this._conn.Open();
				if (this._conn.State != ConnectionState.Open) return;

				this._transaction = this._conn.BeginTransaction();

				foreach (var statement in statements)
                {
					using (var command = new MySqlCommand(statement, this._conn, this._transaction))
					{
						command.ExecuteNonQuery();
					}
				}
			}
			catch (MySqlException e)
			{
				this.LogError("CreateTables", e.Message);

				if (this._transaction != null) this._transaction.Rollback();
				if (this._conn != null && this._conn.State == ConnectionState.Open)
					this._conn.Dispose();

				this._transaction = null;
			}
			catch (Exception e)
			{
				this.LogError("CreateTables", e.Message);

				if (this._transaction != null) this._transaction.Rollback();
				if (this._conn != null && this._conn.State == ConnectionState.Open)
					this._conn.Close();

				this._transaction = null;
			}
			finally
			{
				if (this._transaction != null)
                {
					this._transaction.Commit();
					this._isDatabaseReady = true;
				}

				if (this._conn != null && this._conn.State == ConnectionState.Open)
					this._conn.Close();

				this._transaction = null;
			}
		}

		private void MaybeCreateGame()
        {
			if (!this._isDatabaseReady) return;

			var parameters = new Dictionary<string, object>();
			parameters.Add("Name", this._game);

			var query = "SELECT `GameID` FROM tbl_games WHERE `Name` = @Name";
			var queryResult = this.ReadFromQuery(query, parameters);
			var shouldCreate = false;

			if (queryResult == null) shouldCreate = true;
			if (queryResult.Count == 0) shouldCreate = true;

			if (!shouldCreate)
            {
				this._gameID = Convert.ToInt32(queryResult[0]["GameID"]);
				return;
            }

			var statementResult = this.InsertData("tbl_games", parameters, this._tableBuilderLock);
			if (statementResult.HasValue) this._gameID = Convert.ToInt32(statementResult.Value);
		}

		private int? GetRank(string soldierName)
        {
			this.LogDebug("GetRank", $"Retrieving rank from player {soldierName}");
			var query = this.BuildGetRankQuery();

			var keyValue = new Dictionary<string, object>();
			keyValue.Add("soldierName", soldierName);

			if (this._enumStatsOverallRanking == enumBoolYesNo.Yes) keyValue.Add("serverGroup", this._serverGroup);
			if (this._enumStatsOverallRanking == enumBoolYesNo.No) keyValue.Add("serverID", this._serverID);

			var result = this.ReadFromQuery(query, keyValue);
			if (result == null) return null;
			if (result.Count == 1) return Convert.ToInt32(result[0]["rank"]);

			return null;
        }

		private List<Dictionary<string, object>> ReadFromQuery(string query, Dictionary<string, object> keyValue = null, object lockObject = null)
		{
			if (lockObject == null) lockObject = this._sqlQueryLock;

			if (!this._isDatabaseReady)
			{
				this.LogError("ReadFromQuery", "Database isn't connected yet");
				return null;
			}

			var command = new MySqlCommand(query);

			if (keyValue != null)
            {
				foreach (var item in keyValue)
					command.Parameters.AddWithValue(string.Format("@{0}", item.Key), item.Value);
			}

			var dataTable = new DataTable();

			try
			{
				lock (lockObject)
				{
					if (this._conn == null) this._conn = new MySqlConnection(this.BuildConnectionString());
					if (this._conn.State != ConnectionState.Open) this._conn.Open();
					if (this._conn.State != ConnectionState.Open) return null;

					command.Connection = this._conn;

					using (var dataAdapter = new MySqlDataAdapter(command))
					{
						if (dataAdapter != null) dataAdapter.Fill(dataTable);
					}
				}
			}
			catch (MySqlException e)
			{
				this.LogError("ReadFromQuery", e.Message);

				if (this._conn != null && this._conn.State == ConnectionState.Open)
					this._conn.Dispose();
			}
			catch (Exception e)
			{
				this.LogError("ReadFromQuery", e.Message);

				if (this._conn != null && this._conn.State == ConnectionState.Open)
					this._conn.Close();
			}
			finally
			{
				if (this._conn != null && this._conn.State == ConnectionState.Open)
					this._conn.Close();
			}

			return this.ConvertDataTableToList(dataTable);
		}

		private long? InsertData(string table, Dictionary<string, object> keyValue, object lockObject = null)
        {
			if (!this._isDatabaseReady)
            {
				this.LogError("InsertData", "Database isn't connected yet");
				return null;
            }

			if (keyValue.Count == 0)
            {
				this.LogError("InsertData", "Given dictionary data is empty");
				return null;
			}

			var fields = new List<string>();
			var values = new List<string>();

			foreach (var item in keyValue)
            {
				fields.Add(item.Key);
				values.Add($"@{item.Key}");
			}

			var query = string.Format(
				"INSERT INTO {0} ({1}) VALUES ({2})",
				table,
				string.Join(", ", fields),
				string.Join(", ", values)
			);

			var command = new MySqlCommand(query);

			foreach (var item in keyValue)
				command.Parameters.AddWithValue($"@{item.Key}", item.Value);

			this.ExecuteStatementWithParameters(command, lockObject);
			return command.LastInsertedId;
		}

		private void UpdateData(string table, Dictionary<string, object> statementKeyValue, Dictionary<string, object> queryKeyValue, object lockObject = null)
		{
			if (!this._isDatabaseReady)
			{
				this.LogError("UpdateData", "Database isn't connected yet");
				return;
			}

			if (statementKeyValue.Count == 0)
			{
				this.LogError("UpdateData", "Given dictionary data is empty");
				return;
			}

			if (queryKeyValue.Count == 0)
			{
				this.LogError("UpdateData", "Given dictionary query data is empty");
				return;
			}

			var setClause = new List<string>();
			var whereClause = new List<string>();

			foreach (var item in statementKeyValue)
				setClause.Add($"{item.Key} = @{item.Key}");

			foreach (var item in queryKeyValue) 
				whereClause.Add($"{item.Key} = @{item.Key}");

			var query = string.Format(
				"UPDATE {0} SET {1} WHERE {2}",
				table,
				string.Join(", ", setClause),
				string.Join(" AND ", whereClause)
			);

			var command = new MySqlCommand(query);

			foreach (var item in statementKeyValue)
				command.Parameters.AddWithValue($"@{item.Key}", item.Value);

			foreach (var item in queryKeyValue)
				command.Parameters.AddWithValue($"@{item.Key}", item.Value);

			this.ExecuteStatementWithParameters(command, lockObject);
		}

		private void DeleteData(string table, Dictionary<string, object> keyValue, object lockObject = null)
		{
			if (!this._isDatabaseReady)
			{
				this.LogError("DeleteData", "Database isn't connected yet");
				return;
			}

			if (keyValue.Count == 0)
			{
				this.LogError("DeleteData", "Given dictionary data is empty");
				return;
			}

			var whereClause = new List<string>();

			foreach (var item in keyValue)
				whereClause.Add($"{item.Key} = @{item.Key}");

			var query = string.Format(
				"DELETE FROM {0} WHERE {1}",
				table,
				string.Join(" AND ", whereClause)
			);

			var command = new MySqlCommand(query);

			foreach (var item in keyValue)
				command.Parameters.AddWithValue($"@{item.Key}", item.Value);

			this.ExecuteStatementWithParameters(command, lockObject);
		}

		private void ExecuteStatementWithParameters(MySqlCommand command, object lockObject = null)
        {
			if (lockObject == null) lockObject = this._tableBuilderLock;

			try
			{
				lock (lockObject)
				{
					if (this._conn == null) this._conn = new MySqlConnection(this.BuildConnectionString());
					if (this._conn.State != ConnectionState.Open) this._conn.Open();
					if (this._conn.State != ConnectionState.Open) return;

					this._transaction = this._conn.BeginTransaction();

					command.Connection = this._conn;
					command.Transaction = this._transaction;
					command.ExecuteNonQuery();

					this._lastCommandExecutedAt = DateTime.Now;
				}
			}
			catch (MySqlException e)
			{
				this.LogError("ExecuteStatement", e.Message);

				if (this._transaction != null) this._transaction.Rollback();
				if (this._conn != null && this._conn.State == ConnectionState.Open)
					this._conn.Dispose();

				this._transaction = null;
			}
			catch (Exception e)
			{
				this.LogError("ExecuteStatement", e.Message);

				if (this._transaction != null) this._transaction.Rollback();
				if (this._conn != null && this._conn.State == ConnectionState.Open)
					this._conn.Close();

				this._transaction = null;
			}
			finally
			{
				if (this._transaction != null) this._transaction.Commit();
				if (this._conn != null && this._conn.State == ConnectionState.Open)
					this._conn.Close();

				this._transaction = null;
			}
		}

		private string BuildConnectionString()
        {
			lock (this._connectionStringBuilderLock)
			{
				uint port = 3306;
				uint.TryParse(this._dbPort, out port);

				this._connStrBuilder.Port = port;
				this._connStrBuilder.Server = this._dbHost;
				this._connStrBuilder.UserID = this._dbUsername;
				this._connStrBuilder.Password = this._dbPassword;
				this._connStrBuilder.Database = this._dbName;
				this._connStrBuilder.UseCompression = false;
				this._connStrBuilder.Pooling = false;
				this._connStrBuilder.AllowUserVariables = true;
				this._connStrBuilder.DefaultCommandTimeout = 3600;
				this._connStrBuilder.ConnectionTimeout = 5;

				return this._connStrBuilder.ConnectionString;
			}
		}

		private Dictionary<string, object> ConvertDataRowToDictionary(string[] columnNames, DataRow dataRow)
		{
			var rowDictionary = new Dictionary<string, object>();
			if (columnNames.Length == 0) return null;

			foreach (var columnName in columnNames)
				rowDictionary.Add(columnName, dataRow[columnName]);

			return rowDictionary;
		}

		private List<Dictionary<string, object>> ConvertDataTableToList(DataTable dataTable)
        {
			var dictList = new List<Dictionary<string, object>>();
			var columnNames = new List<string>();

			foreach (DataColumn dataColumn in dataTable.Columns)
				columnNames.Add(dataColumn.ColumnName);

			foreach (DataRow dataRow in dataTable.Rows)
            {
				var rowDictionary = this.ConvertDataRowToDictionary(columnNames.ToArray(), dataRow);
				if (rowDictionary != null) dictList.Add(rowDictionary);
			}

			return dictList;
		}

		private string BuildGetRankQuery()
        {
			var field = "rankKills";
			if (this._enumStatsRankingByScore == enumBoolYesNo.Yes) field = "rankScore";

			var query = @"SELECT tps.{0} AS rank
                            FROM tbl_playerstats tps
                      INNER JOIN tbl_server_player tsp ON tps.StatsID = tsp.StatsID
                      INNER JOIN tbl_playerdata tpd ON tsp.PlayerID = tpd.PlayerID
                           WHERE tpd.SoldierName = @soldierName
                             AND tsp.ServerID = @serverID";

			if (this._enumStatsOverallRanking == enumBoolYesNo.Yes)
			{
				query = @"SELECT tpr.{0} AS rank
                            FROM tbl_playerrank tpr
                      INNER JOIN tbl_playerdata tpd ON tpr.PlayerID = tpd.PlayerID
                           WHERE tpd.SoldierName = @soldierName
                             AND tpr.ServerGroup = @serverGroup";
			}

			return string.Format(query, field);
		}

		// Private helper methods

		private void BuildRegexRuleset()
        {
			try
			{
				this._chatRegexFilterRuleList = new List<Regex>();

				foreach (var rule in this._chatStringFilterRuleList)
					this._chatRegexFilterRuleList.Add(new Regex(rule.Replace("&#124", "|").Replace("&#124", "+")));

				this.LogDebug("BuildRegexRuleset", "Active Regex-Ruleset:");
				foreach (var regexRule in this._chatRegexFilterRuleList)
					this.LogDebug("BuildRegexRuleset", regexRule.ToString());
			}
			catch (Exception e)
			{
				this.LogError("BuildRegexRuleset", e.Message);
			}
		}

		private void BuildAllowedLoggerLevel()
        {
			this._allowedLoggerLevel = new List<string>();

			switch (this._loggerLevel)
			{
				case "Error":
					_allowedLoggerLevel.Add("Error");
					break;

				case "Warning":
					_allowedLoggerLevel.Add("Warning");
					_allowedLoggerLevel.Add("Error");
					break;

				case "Info":
					_allowedLoggerLevel.Add("Info");
					_allowedLoggerLevel.Add("Warning");
					_allowedLoggerLevel.Add("Error");
					break;

				default:
					_allowedLoggerLevel.Add("Debug");
					_allowedLoggerLevel.Add("Info");
					_allowedLoggerLevel.Add("Warning");
					_allowedLoggerLevel.Add("Error");
					break;
			}
		}

		private void SetupIngameCommands()
		{
			lock (this._commandsSetupLock)
			{
				var enabled = false;
				var top10Enabled = false;

				this._ingameCommands.Clear();

				if (this._enumStatsLogging == enumBoolYesNo.Yes && this._enumStatsIngameCommands == enumBoolYesNo.Yes)
				{
					enabled = true;

					if (this._enumStatsTop10 == enumBoolYesNo.Yes)
						top10Enabled = true;
				}

				this._ingameCommands.Add("playerstats", new IngameCommand()
				{
					FunctionCall = "OnCommandStats",
					Command = this._statsInGameCommand,
					Description = "Provides a player his personal server stats",
					Enabled = enabled
				});

				this._ingameCommands.Add("serverstats", new IngameCommand()
				{
					FunctionCall = "OnCommandServerStats",
					Command = this._serverStatsInGameCommand,
					Description = "Provides a player his personal server stats",
					Enabled = enabled
				});

				this._ingameCommands.Add("dogtagstats", new IngameCommand()
				{
					FunctionCall = "OnCommandDogtags",
					Command = this._serverStatsInGameCommand,
					Description = "Provides a player his personal dogtag stats",
					Enabled = enabled
				});

				this._ingameCommands.Add("session", new IngameCommand()
				{
					FunctionCall = "OnCommandSession",
					Command = this._serverStatsInGameCommand,
					Description = "Provides a player his personal session data",
					Enabled = enabled
				});

				this._ingameCommands.Add("playeroftheday", new IngameCommand()
				{
					FunctionCall = "OnCommandPlayerOfTheDay",
					Command = this._playerOfTheDayInGameCommand,
					Description = "Provides the player of the day stats",
					Enabled = enabled
				});

				this._ingameCommands.Add("top10", new IngameCommand()
				{
					FunctionCall = "OnCommandTop10",
					Command = this._top10InGameCommand,
					Description = "Provides a player top10 Players",
					Enabled = top10Enabled
				});

				this._ingameCommands.Add("top10pe", new IngameCommand()
				{
					FunctionCall = "OnCommandTop10ForPeriod",
					Command = this._top10ForPeriodInGameCommand,
					Description = "Provides a player top10 Players for a specific timeframe",
					Enabled = top10Enabled
				});
			}
		}

		private void RegisterAllCommands()
		{
			lock (this._registerAllCommandsLock)
			{
				this.SetupIngameCommands();

				if (this._isPluginEnabled)
				{
					if (this._enumStatsIngameCommands == enumBoolYesNo.No)
					{
						this.UnregisterAllCommands();
						return;
					}

					try
					{
						foreach (var kvp in this._ingameCommands)
						{
							var ingameCommand = kvp.Value;

							if (ingameCommand.Command != string.Empty)
							{
								foreach (var command in ingameCommand.Command.Split(','))
								{
									var matchCommand = new MatchCommand(
										"AdKatsLogger",
										ingameCommand.FunctionCall,
										this.Listify<string>("@", "!", "#"),
										command,
										this.Listify<MatchArgumentFormat>(),
										new ExecutionRequirements(ExecutionScope.All),
										ingameCommand.Description
									);

									if (kvp.Value.Enabled)
									{
										this.RegisterCommand(matchCommand);
										continue;
									}

									this.UnregisterCommand(matchCommand);
								}
							}
						}
					}
					catch (Exception e)
					{
						this.LogError("RegisterAllCommands", e.Message);
					}
				}
			}
		}

		private void UnregisterAllCommands()
        {
			this.SetupIngameCommands();

			try
			{
				foreach (var kvp in this._ingameCommands)
				{
					if (kvp.Value.Command != string.Empty)
					{
						var ingameCommand = kvp.Value;

						if (ingameCommand.Command != string.Empty)
						{
							foreach (var command in ingameCommand.Command.Split(','))
							{
								this.UnregisterCommand(
									new MatchCommand(
										"AdKatsLogger",
										ingameCommand.FunctionCall,
										this.Listify<string>("@", "!", "#"),
										command,
										this.Listify<MatchArgumentFormat>(),
										new ExecutionRequirements(ExecutionScope.All),
										ingameCommand.Description
									)
								);
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				this.LogDebug("UnregisterAllCommands", e.Message);
			}
		}

		private List<string> GetExcludedCommandStrings(string accountName)
		{
			var returnCommandsList = new List<string>();
			var commandsList = this.GetRegisteredCommands();
			var accountPrivileges = this.GetAccountPrivileges(accountName);

			foreach (var matchCommand in commandsList)
			{
				if (matchCommand.Requirements.HasValidPermissions(accountPrivileges) && !returnCommandsList.Contains(matchCommand.Command))
					returnCommandsList.Add(matchCommand.Command);
			}

			return returnCommandsList;
		}

		private List<string> GetCommandStrings()
		{
			var returnCommandsList = new List<string>();
			var commandsList = this.GetRegisteredCommands();

			foreach (var matchCommand in commandsList)
			{
				if (!returnCommandsList.Contains(matchCommand.Command))
					returnCommandsList.Add(matchCommand.Command);
			}

			return returnCommandsList;
		}

		public List<string> ReplaceListStrings(string[] targetArray, string wordToReplace, string replacement) =>
			ReplaceListStrings(new List<string>(targetArray), wordToReplace, replacement);

		public List<string> ReplaceListStrings(List<string> targetList, string wordToReplace, string replacement)
		{
			List<string> replacedList = new List<string>();

			foreach (var value in targetList)
				replacedList.Add(value.Replace(wordToReplace, replacement));

			return replacedList;
		}

		private void LogDebug(string method, string message) => this.ConsoleWrite(method, "Debug", message);
		private void LogError(string method, string message) => this.ConsoleWrite(method, "Error", message);
		private void LogWarning(string method, string message) => this.ConsoleWrite(method, "Warning", message);
		private void LogInfo(string method, string message) => this.ConsoleWrite(method, "Info", message);

		private void ConsoleWrite(string method, string level, string message)
		{
#if DEBUG
			Console.WriteLine($"[{method}] {level}: {message}");
#else
			if (this._allowedLoggerLevel.Contains(level))
				this.ExecuteCommand("procon.protected.pluginconsole.write", $"[{method}] {FormatLevel(level)}: {message}");
#endif
		}

		private string FormatLevel(string level)
        {
			if (level == "Debug")
				return "^9^bDEBUG^0";

			return string.Format("^1^b{0}^0", level.ToUpper());
		}
	}

	public class Stats
    {
		public string ClanTag { get; set; }
		public string Guid { get; set; }
		public string EAGuid { get; set; }
		public string IP { get; set; }
		public string PlayerCountryCode { get; set; }
		public int Score { get; set; }
		public int HighScore { get; set; }
		public int LastScore { get; set; }
		public int Kills { get; set; }
		public int BeforeLeftKills { get; set; }
		public int Headshots { get; set; }
		public int Deaths { get; set; }
		public int BeforeLeftDeaths { get; set; }
		public int Suicides { get; set; }
		public int TeamKills { get; set; }
		public int PlayedTime { get; set; }
		public int Rounds { get; set; }
		public DateTime PlayerLeftAt { get; set; }
		public DateTime PlayerJoinedAt { get; set; }
		public int PlayerLeftServerScore { get; set; }
		public bool IsOnline { get; set; }
		public int Rank { get; set; }
		public int Killstreak { get; set; }
		public int Deathstreak { get; set; }
		public int Wins { get; set; }
		public int Losses { get; set; }
		public int TeamId { get; set; }
		public int GlobalRank { get; set; }
		public Awards Awards { get; set; }
		public Dictionary<string, Dictionary<string, UsedWeapon>> WeaponKills => this._dictUsedWeapons;
		public int TotalScore => this.PlayerLeftServerScore + this.Score;

		private int _beforeLeftKills = 0;
		private int _beforeLeftDeaths = 0;
		private int _killCount = 0;
		private int _deathCount = 0;
		private double _offseat = 0;
		private Dictionary<string, Dictionary<string, UsedWeapon>> _dictUsedWeapons = new Dictionary<string, Dictionary<string, UsedWeapon>>();

		public double KDR
        {
			get
            {
				double kills = Convert.ToDouble(this.Kills);
				double deaths = Convert.ToDouble(this.Deaths);

				if (this.Deaths == 0) return this.Kills;

				return Math.Round(kills / deaths, 2);
			}
        }

		public int TotalPlayedTime
		{
			get
			{
				if (this.IsOnline)
					return this.PlayedTime + Convert.ToInt32(PlayedTimeDuration.TotalSeconds);

				return this.PlayedTime;
			}
		}

		public Stats(string guid, int score, int kills, int headshots, int deaths, int suicides, int teamKills, int playedTime, double offset, Dictionary<string, Dictionary<string, UsedWeapon>> dictUsedWeapons)
		{
			this._offseat = offset;

			this.ClanTag = "";
			this.Guid = guid;
			this.EAGuid = "";
			this.IP = "";
			this.Score = score;
			this.LastScore = 0;
			this.HighScore = score;
			this.Kills = kills;
			this.Headshots = headshots;
			this.Deaths = deaths;
			this.Suicides = suicides;
			this.TeamKills = teamKills;
			this.PlayedTime = playedTime;
			this.Rounds = 0;
			this.PlayerLeftServerScore = 0;
			this.PlayerCountryCode = "";
			this.PlayerJoinedAt = this.DateTimeWithOffset.Now;
			this.PlayerLeftAt = DateTime.MinValue;
			this.Rank = 0;
			this._killCount = 0;
			this.Killstreak = 0;
			this._deathCount = 0;
			this.Deathstreak = 0;
			this.Wins = 0;
			this.Losses = 0;
			this.Awards = new Awards();

			foreach (var pair in dictUsedWeapons)
			{
				this._dictUsedWeapons.Add(pair.Key, new Dictionary<string, UsedWeapon>());

				foreach (var subpair in pair.Value)
				{
					var usedWeapon = new UsedWeapon(
						subpair.Value.Name,
						subpair.Value.FieldName,
						subpair.Value.Slot,
						subpair.Value.KitRestriction
					);

					this._dictUsedWeapons[pair.Key].Add(subpair.Key, usedWeapon);
				}
			}
		}

		public void AddScore(int score)
		{
			if (score == 0) return;

			this.Score = this.Score + (score - this.LastScore);
			this.LastScore = score;

			if (score > this.HighScore) this.HighScore = score;
		}

		public void AddKill(string damageType, string weaponType, bool headshot)
		{
			this.Kills++;

			if (this._dictUsedWeapons.ContainsKey(damageType) && this._dictUsedWeapons[damageType].ContainsKey(weaponType))
				this._dictUsedWeapons[damageType][weaponType].Kills++;

			if (headshot)
			{
				if (this._dictUsedWeapons.ContainsKey(damageType) && this._dictUsedWeapons[damageType].ContainsKey(weaponType))
					this._dictUsedWeapons[damageType][weaponType].Headshots++;

				this.Headshots++;
			}

			this._killCount++;
			this._deathCount = 0;

			if (this._killCount > this.Killstreak)
				this.Killstreak = this._killCount;

			this.Awards.OnKill(Kills, Headshots, Deaths, _killCount, _deathCount);
		}

		public void AddDeath(string damageType, string weaponType)
		{
			this.Deaths++;

			if (this._dictUsedWeapons.ContainsKey(damageType) && this._dictUsedWeapons[damageType].ContainsKey(weaponType))
				this._dictUsedWeapons[damageType][weaponType].Deaths++;

			this._deathCount++;
			this._killCount = 0;

			if (this._deathCount > this.Deathstreak)
				this.Deathstreak = this._deathCount;

			this.Awards.OnDeath(Kills, Headshots, Deaths, _killCount, _deathCount);
		}
		
		public void OnPlayerLeft()
        {
			this.PlayerLeftServerScore += this.Score;
			this.Score = 0;
			this._beforeLeftKills += this.Kills;
			this._beforeLeftDeaths += this.Deaths;
			this.PlayedTime += Convert.ToInt32(PlayedTimeDuration.TotalSeconds);
			this.IsOnline = false;
		}

		// Private methods

		private DateTimeWithOffset DateTimeWithOffset => new DateTimeWithOffset(this._offseat);
		private TimeSpan PlayedTimeDuration => this.DateTimeWithOffset.Now - this.PlayerJoinedAt;
	}

	public class UsedWeapon
	{
		public int Kills { get; set; }
		public int Headshots { get; set; }
		public int Deaths { get; set; }
		public string Name { get; set; }
		public string FieldName { get; set; }
		public string Slot { get; set; }
		public string KitRestriction { get; set; }

		public UsedWeapon(string name, string fieldName, string slot, string kitRestriction)
		{
			this.Name = name;
			this.FieldName = fieldName;
			this.Slot = slot;
			this.KitRestriction = kitRestriction;
			this.Kills = 0;
			this.Headshots = 0;
			this.Deaths = 0;
		}
	}

	public class Awards
    {
		private Dictionary<string, int> _dictAwards = new Dictionary<string, int>();

		public Awards()
		{
			this._dictAwards = new Dictionary<string, int>();
		}

		public Dictionary<string, int> DicAwards => this._dictAwards;

		public void Add(string key, int count)
		{
			if (this._dictAwards.ContainsKey(key))
			{
				this._dictAwards[key] = this._dictAwards[key] + count;
			}
			else
			{
				this._dictAwards.Add(key, count);
			}
		}

		public void OnKill(int kills, int headshots, int deaths, int killCount, int deathCount)
		{
			double kdr = (Double)kills / (Double)deaths;

			// Purple Heart
			if (kills >= 5 && deaths >= 20 && kdr == 0.25)
			{
				this.Add("Purple_Heart", 1);
			}

			// Killstreaks
			if (killCount == 5)
			{
				// 5 Kills in a row
				this.Add("Killstreak_5", 1);
			}
			else if (killCount == 10)
			{
				// 10 kills in a row
				this.Add("Killstreak_10", 1);
			}
			else if (killCount == 15)
			{
				// 15 kills in a row
				this.Add("Killstreak_15", 1);
			}
			else if (killCount == 20)
			{
				// 20 kills in a row
				this.Add("Killstreak_20", 1);
			}
		}

		public void OnDeath(int kills, int headshots, int deaths, int killCount, int deathCount)
		{
			// Purple Heart
			if (kills >= 5 && deaths >= 20 && ((Double)kills / (Double)deaths) == 0.25)
			{
				this.Add("Purple_Heart", 1);
			}
		}
	}

	public class PlayerCache
    {
		private int PlayerId { get; set; }
		private int StatsID { get; set; }
		private bool IsOnline { get; set; }
	}

	public class ChatLog
    {
		private readonly string _name;
		private string _message;
		private string _subset;
		private DateTime _createdAt;

		public string Name => _name;

		public string Message => _message;

		public string Subset => _subset;

		public DateTime CreatedAt => _createdAt;

		public ChatLog(DateTime createdAt, string name, string message, string subset)
		{
			this._name = name;
			this._message = message;
			this._subset = subset;
			this._createdAt = createdAt;
		}
	}

	public class MapStats
    {
        public DateTime MapLoadedAt { get; set; }
        public DateTime RoundStartedAt { get; set; }
		public DateTime RoundEndedAt { get; set; }
		public string MapName { get; set; }
		public string GameMode { get; set; }
		public int Round { get; set; }
		public int NumberOfRounds { get; set; }
		public List<int> PlayerCountList { get; set; }
		public int MinPlayers { get; set; }
		public int MaxPlayers { get; set; }
		public int ServerPlayerMax { get; set; }
		public double AveragePlayers { get; set; }
		public int PlayersLeft { get; set; }
		public int PlayerJoined { get; set; }
		public DateTimeWithOffset DateTimeWithOffset { get; set; }

		public MapStats(DateTime mapLoadedAt, string mapName, int round, int numberOfRounds, double offset)
		{
			this.MapLoadedAt = mapLoadedAt;
			this.MapName = mapName;
			this.Round = round;
			this.NumberOfRounds = numberOfRounds;
			this.DateTimeWithOffset = new DateTimeWithOffset(offset);

			this.MaxPlayers = 32;
			this.ServerPlayerMax = 32;
			this.MinPlayers = 0;
			this.PlayerJoined = 0;
			this.PlayersLeft = 0;
			this.PlayerCountList = new List<int>();
			this.RoundStartedAt = DateTime.MinValue;
			this.RoundEndedAt = DateTime.MinValue;
			this.GameMode = "";
		}

		public void CalculateMaxMinAveragePlayers()
        {
            this.MaxPlayers = 0;
            this.MinPlayers = ServerPlayerMax;
            this.AveragePlayers = 0;
            int entries = 0;

            foreach (int playerCount in this.PlayerCountList)
            {
                if (playerCount >= this.MaxPlayers) this.MaxPlayers = playerCount;
                if (playerCount <= this.MinPlayers) this.MinPlayers = playerCount;

                this.AveragePlayers = this.AveragePlayers + playerCount;
                entries++;
            }

            if (entries != 0)
            {
                this.AveragePlayers = this.AveragePlayers / (Convert.ToDouble(entries));
                this.AveragePlayers = Math.Round(this.AveragePlayers, 1);
            }
            else
            {
                this.AveragePlayers = 0;
                this.MaxPlayers = 0;
                this.MinPlayers = 0;
            }
        }
    }

	public class SpamProtection
	{
		private Dictionary<string, int> dicPlayer;
		private int _allowedRequests;

		public SpamProtection(int allowedRequests)
		{
			this._allowedRequests = allowedRequests;
			this.dicPlayer = new Dictionary<string, int>();
		}

		public bool IsAllowed(string strSpeaker)
		{
			if (this.dicPlayer.ContainsKey(strSpeaker) == true)
			{
				int i = this.dicPlayer[strSpeaker];
				if (0 >= i)
				{
					//Player is blocked
					this.dicPlayer[strSpeaker]--;
					return true;
				}
				else
				{
					//Player is not blocked
					this.dicPlayer[strSpeaker]--;
					return true;
				}
			}
			else
			{
				this.dicPlayer.Add(strSpeaker, this._allowedRequests);
				this.dicPlayer[strSpeaker]--;

				return true;
			}
		}

		public void Reset() => this.dicPlayer.Clear();
	}

	public class DateTimeWithOffset
	{
		private double _offset = 0;

		public DateTime Now
		{
			get { return DateTime.Now.AddHours(_offset); }
		}

		public DateTimeWithOffset(double offset) => this._offset = offset;
	}

	public class IngameCommand
	{
		public string FunctionCall { get; set; }
		public string Command { get; set; }
		public string Description { get; set; }
		public bool Enabled { get; set; }
	}
}