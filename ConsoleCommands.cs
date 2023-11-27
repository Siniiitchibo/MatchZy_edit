using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;

namespace MatchZy
{
    public partial class MatchZy
    {
        [ConsoleCommand("css_whitelist", "Toggles Whitelisting of players")]
        public void OnWLCommand(CCSPlayerController? player, CommandInfo? command) {
            if (IsPlayerAdmin(player, "css_whitelist", "@css/config"))
            {
                isWhitelistRequired = !isWhitelistRequired;
                string WLStatus = isWhitelistRequired ? "Enabled" : "Disabled";
                if (player == null) {
                    ReplyToUserCommand(player, $"Whitelist is now {WLStatus}!");
                } else {
                    player.PrintToChat($"{chatPrefix} Whitelist is now {ChatColors.Green}{WLStatus}{ChatColors.Default}!");
                }
            } else {
                SendPlayerNotAdminMessage(player);
            }
        }
        
        [ConsoleCommand("css_ready", "Marks the player ready")]
        public void OnPlayerReady(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;
            Log($"[!ready command] Sent by: {player.UserId}, connectedPlayers: {connectedPlayers}");
            if (readyAvailable && !matchStarted) {
                if (player.UserId.HasValue) {
                    if (!playerReadyStatus.ContainsKey(player.UserId.Value)) {
                        playerReadyStatus[player.UserId.Value] = false;
                    }
                    if (playerReadyStatus[player.UserId.Value]) {
                        player.PrintToChat($" Uû si oznaËen˝ ako pripraven˝!");
                    } else {
                        playerReadyStatus[player.UserId.Value] = true;
                        player.PrintToChat($" Bol si oznaËen˝ ako pripraven˝!");
                    }
                    AddTimer(afterReadyDelay, CheckLiveRequired);
                    HandleClanTags();
                    UnreadyHintMessageStart();
                }
            }
        }

        [ConsoleCommand("css_unready", "Marks the player unready")]
        public void OnPlayerUnReady(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;
            Log($"[!unready command] {player.UserId}");
            if (readyAvailable && !matchStarted) {
                if (player.UserId.HasValue) {
                    if (!playerReadyStatus.ContainsKey(player.UserId.Value)) {
                        playerReadyStatus[player.UserId.Value] = false;
                    }
                    if (!playerReadyStatus[player.UserId.Value]) {
                        player.PrintToChat($" Uû si oznaËen˝ ako nepripraven˝!");
                    } else {
                        playerReadyStatus[player.UserId.Value] = false;
                        player.PrintToChat($" Bol si oznaËen˝ ako nepripraven˝!");
                    }
                    HandleClanTags();
                    UnreadyHintMessageStart();
                }
            }
        }

        [ConsoleCommand("css_stay", "Stays after knife round")]
        public void OnTeamStay(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;
            
            Log($"[!stay command] {player.UserId}, TeamNum: {player.TeamNum}, knifeWinner: {knifeWinner}, isSideSelectionPhase: {isSideSelectionPhase}");
            if (isSideSelectionPhase) {
                if (player.TeamNum == knifeWinner) {
                    Server.PrintToChatAll($" {knifeWinnerName} zvolili {ChatColors.Green}stay{ChatColors.Default}!");
                    StartLive();
                }
            }
        }

        [ConsoleCommand("css_switch", "Switch after knife round")]
        public void OnTeamSwitch(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;
            
            Log($"[!switch command] {player.UserId}, TeamNum: {player.TeamNum}, knifeWinner: {knifeWinner}, isSideSelectionPhase: {isSideSelectionPhase}");
            if (isSideSelectionPhase) {
                if (player.TeamNum == knifeWinner) {
                    Server.ExecuteCommand("mp_swapteams;");
                    SwapSidesInTeamData(true);
                    Server.PrintToChatAll($" {knifeWinnerName} zvolili {ChatColors.Green}switch{ChatColors.Default}!");
                    StartLive();
                }
            }
        }

        [ConsoleCommand("css_tech", "Pause the match")]
        public void OnTechCommand(CCSPlayerController? player, CommandInfo? command) {            
            PauseMatch(player, command);
        }

        [ConsoleCommand("css_pause", "Pause the match")]
        public void OnPauseCommand(CCSPlayerController? player, CommandInfo? command) {            
            PauseMatch(player, command);
        }

        [ConsoleCommand("css_forcepause", "Admin Pause the match")]
        public void OnForcePauseCommand(CCSPlayerController? player, CommandInfo? command)
        {
            ForcePauseMatch(player, command);
        }

        [ConsoleCommand("css_unpause", "Unpause the match")]
        public void OnUnpauseCommand(CCSPlayerController? player, CommandInfo? command) {
            if (isMatchLive && isPaused) {
                var pauseTeamName = unpauseData["pauseTeam"];
                if ((string)pauseTeamName == "Admin") {
                    player?.PrintToChat($"Z·pas bol pozastaven˝ Adminom. Pauzu mÙûe zruöiù iba Admin!");
                    return;
                }
                string unpauseTeamName = "UnPauseTeam";
                string remainingUnpauseTeam = "UnPauseTeam";
                if (player?.TeamNum == 2) {
                    unpauseTeamName = reverseTeamSides["TERRORIST"].teamName;
                    remainingUnpauseTeam = reverseTeamSides["CT"].teamName;
                    if (!(bool)unpauseData["t"]) {
                        unpauseData["t"] = true;
                    }
                    
                } else if (player?.TeamNum == 3) {
                    unpauseTeamName = reverseTeamSides["CT"].teamName;
                    remainingUnpauseTeam = reverseTeamSides["TERRORIST"].teamName;
                    if (!(bool)unpauseData["ct"]) {
                        unpauseData["ct"] = true;
                    }
                } else {
                    return;
                }
                if ((bool)unpauseData["t"] && (bool)unpauseData["ct"]) {
                    Server.PrintToChatAll($" Pauza zruöen·, z·pas bude pokraËovaù!");
                    Server.ExecuteCommand("mp_unpause_match;");
                    isPaused = false;
                    unpauseData["ct"] = false;
                    unpauseData["t"] = false;
                }
                else {
                    Server.PrintToChatAll($" {ChatColors.Green}{unpauseTeamName}{ChatColors.Default} chc˙ pokraËovaù v z·pase. {ChatColors.Green}{remainingUnpauseTeam}{ChatColors.Default}Pre potvrdenie napÌö {ChatColors.Green}.unpause");
                }
                if (!isPaused && pausedStateTimer != null) {
                    pausedStateTimer.Kill();
                    pausedStateTimer = null;
                }
            }
        }

        [ConsoleCommand("css_forceunpause", "Force Unpause the match")]
        public void OnForceUnpauseCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (isMatchLive && isPaused)
            {
                if (!IsPlayerAdmin(player, "css_forceunpause", "@css/config"))
                {
                    SendPlayerNotAdminMessage(player);
                    return;
                }
                Server.PrintToChatAll($" {ChatColors.Green}Admin{ChatColors.Default} spustil hru, z·pas bude pokraËovaù!");
                Server.ExecuteCommand("mp_unpause_match;");
                isPaused = false;
                unpauseData["ct"] = false;
                unpauseData["t"] = false;
                if (!isPaused && pausedStateTimer != null)
                {
                    pausedStateTimer.Kill();
                    pausedStateTimer = null;
                }
                if (player == null)
                {
                    Server.PrintToConsole("Admin spustil hru, z·pas bude pokraËovaù!");
                }
            }
        }

        [ConsoleCommand("css_tac", "Starts a tactical timeout for the requested team")]
        public void OnTacCommand(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;
            
            if (matchStarted && isMatchLive) {
                Log($"[.tac command sent via chat] Sent by: {player.UserId}, connectedPlayers: {connectedPlayers}");
                if (player.TeamNum == 2) {
                    Server.ExecuteCommand("timeout_terrorist_start");
                } else if (player.TeamNum == 3) {
                    Server.ExecuteCommand("timeout_ct_start");
                } 
            }
        }

        [ConsoleCommand("css_kniferound", "Toggles knife round for the match")]
        public void OnKifeCommand(CCSPlayerController? player, CommandInfo? command) {
            if (IsPlayerAdmin(player, "css_kniferound", "@css/config"))
            {
                isKnifeRequired = !isKnifeRequired;
                string knifeStatus = isKnifeRequired ? "Enabled" : "Disabled";
                if (player == null) {
                    ReplyToUserCommand(player, $"Knife round is now {knifeStatus}!");
                } else {
                    player.PrintToChat($"{chatPrefix} Knife round is now {ChatColors.Green}{knifeStatus}{ChatColors.Default}!");
                }
            } else {
                SendPlayerNotAdminMessage(player);
            }
        }

        [ConsoleCommand("css_readyrequired", "Sets number of ready players required to start the match")]
        public void OnReadyRequiredCommand(CCSPlayerController? player, CommandInfo command) {
            if (IsPlayerAdmin(player, "css_readyrequired", "@css/config"))
            {
                if (command.ArgCount >= 2) {
                    string commandArg = command.ArgByIndex(1);
                    HandleReadyRequiredCommand(player, commandArg);
                }
                else {
                    string minimumReadyRequiredFormatted = (player == null) ? $"{minimumReadyRequired}" : $"{ChatColors.Green}{minimumReadyRequired}{ChatColors.Default}";
                    ReplyToUserCommand(player, $"Current Ready Required: {minimumReadyRequiredFormatted} .Usage: !readyrequired <number_of_ready_players_required>");
                }                
            } else {
                SendPlayerNotAdminMessage(player);
            }
        }

        [ConsoleCommand("css_settings", "Shows the current match configuration/settings")]
        public void OnMatchSettingsCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (player == null) return;

            if (IsPlayerAdmin(player, "css_settings", "@css/config"))
            {
                string knifeStatus = isKnifeRequired ? "Enabled" : "Disabled";
                string playoutStatus = isPlayOutEnabled ? "Enabled" : "Disabled";
                player.PrintToChat($" Current Settings:");
                player.PrintToChat($" Knife: {ChatColors.Green}{knifeStatus}{ChatColors.Default}");
                player.PrintToChat($" Minimum Ready Required: {ChatColors.Green}{minimumReadyRequired}{ChatColors.Default}");
                player.PrintToChat($" Playout: {ChatColors.Green}{playoutStatus}{ChatColors.Default}");
            }
            else
            {
                SendPlayerNotAdminMessage(player);
            }
        }

        [ConsoleCommand("css_restart", "Restarts the match")]
        public void OnRestartMatchCommand(CCSPlayerController? player, CommandInfo? command) {
            if (IsPlayerAdmin(player, "css_restart", "@css/config"))
            {
                if (!isPractice) {
                    ResetMatch();
                } else {
                    ReplyToUserCommand(player, "Practice mÛd je aktÌvny, nemÙûeö reötartovaù z·pas.");
                }
            } else {
                SendPlayerNotAdminMessage(player);
            }
        }

        [ConsoleCommand("css_map", "Changes the map using changelevel")]
        public void OnChangeMapCommand(CCSPlayerController? player, CommandInfo command) {
            if (player == null) return;
            var mapName = command.ArgByIndex(1);
            HandleMapChangeCommand(player, mapName);
        }

        [ConsoleCommand("css_start", "Force starts the match")]
        public void OnStartCommand(CCSPlayerController? player, CommandInfo? command) {
            if (player == null) return;

            if (IsPlayerAdmin(player, "css_start", "@css/config"))
            {
                if (isPractice) {
                    ReplyToUserCommand(player, $"PoËas Practice mÛdu nemÙûeö spustiù z·pas. Pouûi prÌkaz {ChatColors.Green}.exitprac {ChatColors.Default}pre n·vrat do Pug mÛdu!");
                    return;
                }
                if (matchStarted) {
                    player.PrintToChat($" Start command cannot be used if match is already started! If you want to unpause, please use .unpause");
                } else {
                    Server.PrintToChatAll($" {ChatColors.Green}Admin spustil z·pas!");
                    HandleMatchStart();
                }
            } else {
                SendPlayerNotAdminMessage(player);
            }
        }

        [ConsoleCommand("css_asay", "Say as an admin")]
        public void OnAdminSay(CCSPlayerController? player, CommandInfo? command)
        {
            if (command == null) return;
            if (player == null)
            {
                Server.PrintToChatAll($"{adminChatPrefix} {command.ArgString}");
                return;
            }
            if (!IsPlayerAdmin(player, "css_asay", "@css/chat"))
            {
                SendPlayerNotAdminMessage(player);
                return;
            }
            string message = "";
            for (int i = 1; i < command.ArgCount; i++)
            {
                message += command.ArgByIndex(i) + " ";
            }
            Server.PrintToChatAll($"{adminChatPrefix} {message}");
        }

        [ConsoleCommand("reload_admins", "Reload admins of MatchZy")]
        public void OnReloadAdmins(CCSPlayerController? player, CommandInfo? command) {
            if (IsPlayerAdmin(player, "reload_admins", "@css/config"))
            {
                LoadAdmins();
                UpdatePlayersMap();
            } else {
                SendPlayerNotAdminMessage(player);
            }
        }

        [ConsoleCommand("css_match", "Starts match mode")]
        public void OnMatchCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (!IsPlayerAdmin(player, "css_match", "@css/map", "@custom/prac"))
            {
                SendPlayerNotAdminMessage(player);
                return;
            }
            
            if (matchStarted) {
                ReplyToUserCommand(player, "Pug mÛd uû je nastaven˝!");
                return;
            }

            StartMatchMode();
        }

        [ConsoleCommand("css_exitprac", "Starts match mode")]
        public void OnExitPracCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (pracMessageTimer != null)
            {
                pracMessageTimer.Kill();
                pracMessageTimer = null;
            }
            if (matchStarted) {
                ReplyToUserCommand(player, "Pug mÛd uû je nastaven˝!");
                return;
            }

            StartMatchMode();
        }

        [ConsoleCommand("css_rcon", "Triggers provided command on the server")]
        public void OnRconCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (!IsPlayerAdmin(player, "css_rcon", "@css/rcon"))
            {
                SendPlayerNotAdminMessage(player);
                return;
            }
            Server.ExecuteCommand(command.ArgString);
            ReplyToUserCommand(player, "Command sent successfully!");
        }

        [ConsoleCommand("css_help", "Triggers provided command on the server")]
        public void OnHelpCommand(CCSPlayerController? player, CommandInfo? command)
        {
            SendAvailableCommandsMessage(player);
        }

        [ConsoleCommand("css_playout", "Toggles playout (Playing of max rounds)")]
        public void OnPlayoutCommand(CCSPlayerController? player, CommandInfo? command)
        {
            if (IsPlayerAdmin(player, "css_playout", "@css/config"))
            {
                isPlayOutEnabled = !isPlayOutEnabled;
                string playoutStatus = isPlayOutEnabled ? "Enabled" : "Disabled";
                if (player == null)
                {
                    ReplyToUserCommand(player, $"Playout is now {playoutStatus}!");
                }
                else
                {
                    player.PrintToChat($"{chatPrefix} Playout is now {ChatColors.Green}{playoutStatus}{ChatColors.Default}!");
                }

                if (isPlayOutEnabled)
                {
                    Server.ExecuteCommand("mp_match_can_clinch false");
                }
                else
                {
                    Server.ExecuteCommand("mp_match_can_clinch true");
                }

            }
            else
            {
                SendPlayerNotAdminMessage(player);
            }
        }

    }
}
