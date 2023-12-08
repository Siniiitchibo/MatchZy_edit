using System;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;


namespace MatchZy
{

    public partial class MatchZy
    {
        private HookResult EventRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            _countRounds++;
            var maxrounds = ConVar.Find("mp_maxrounds").GetPrimitiveValue<int>();
            if (_countRounds == (maxrounds - _config.VotingRoundInterval))
            {
                VoteMap(false);
            }
            else if (_countRounds == maxrounds)
            {
                Server.ExecuteCommand(!IsWsMaps(_selectedMap)
                    ? $"map {_selectedMap}"
                    : $"ds_workshop_changelevel {_selectedMap}");
            }

            return HookResult.Continue;
        }

        private void CommandRtv(CCSPlayerController? player, CommandInfo commandinfo)
        {
            if (isWarmup || isPractice)
            {
                if (player == null) return;

                if (_selectedMap != null)
                {
                    PrintToChat(player, "Hlasovanie uû je ukonËenÈ a nie je moûnÈ ho spustiù znova.");
                    return;
                }

                if (!_isVotingActive)
                {
                    PrintToChat(player, "Hlasovanie v tomto momente nie je moûnÈ.");
                    return;
                }

                var countPlayers = _usersArray.Count(user => user != null);
                var countVote = (int)(countPlayers * _config.Needed) == 0 ? 1 : countPlayers * _config.Needed;
                var user = _usersArray[player.Index]!;
                if (user.VotedRtv)
                {
                    PrintToChat(player, "Uû si hlasoval pre zmenu mapy!");
                    return;
                }

                user.VotedRtv = true;
                _votedRtv++;
                PrintToChatAll($"{player.PlayerName} chce spustiù hlasovanie. ({_votedRtv} hlasov, {(int)countVote} potrebn˝ch)");

                if (_votedRtv == (int)countVote)
                    VoteMap(true);
            }
            else if (isMatchLive || isKnifeRound) {
                PrintToChat(player, $" {ChatColors.Gold}Nie je moûnÈ hlasovaù poËas z·pasu!");
                return;
            }
        }
        void HandleNominate(CCSPlayerController player, ChatMenuOption option)
        {
            if (_selectedMap != null)
            {
                PrintToChat(player, "Hlasovanie uû je ukonËenÈ a nie je moûnÈ ho spustiù znova.");
                return;
            }

            if (!_isVotingActive)
            {
                PrintToChat(player, "Hlasovanie v tomto momente nie je moûnÈ.");
                return;
            }

            var indexToAdd = Array.IndexOf(_proposedMaps, null);

            if (indexToAdd == -1)
            {
                PrintToChat(player, "Maxim·lne mnoûstvo m·p pre nomin·ciu.");
                return;
            }

            foreach (var map in _proposedMaps)
            {
                if (map != option.Text) continue;
                PrintToChat(player, "Mapa ktor˙ si zvolil uû bola nominovan·.");
                return;
            }

            foreach (var playedMap in _playedMaps)
            {
                if (playedMap != option.Text) continue;
                PrintToChat(player, "Mapa ktor˙ si zvolil bola pr·ve hran· a nie je moûnÈ ju nominovaù!");
                return;
            }

            var user = _usersArray[player.Index]!;
            if (!string.IsNullOrEmpty(user!.ProposedMaps))
            {
                var buffer = user.ProposedMaps;

                for (int i = 0; i < _proposedMaps.Length; i++)
                {
                    if (_proposedMaps[i] == buffer)
                        _proposedMaps[i] = option.Text;
                }
            }
            else
                _proposedMaps[indexToAdd] = option.Text;

            user.ProposedMaps = option.Text;
            PrintToChatAll($" Player '{player.PlayerName}' nominoval mapu '{option.Text}'");
        }

        private void VoteMap(bool forced)
        {
            // Define the file path
            var rtvmapsfileName = "MatchZy/rtvmaps.cfg";
            var mapsPath = Path.Join(Server.GameDirectory + "/csgo/cfg", rtvmapsfileName);

            _isVotingActive = false;
            var nominateMenu = new ChatMenu("RTV");
            //var mapList = File.ReadAllLines(Path.Combine(ModuleDirectory, "maps.txt"));
            var mapList = File.ReadAllLines(mapsPath);
            var newMapList = mapList.Except(_playedMaps).Except(_proposedMaps)
                .Select(map => map.StartsWith("ws:") ? map.Substring(3) : map)
                .ToList();

            if (mapList.Length < 7)
            {
                for (int i = 0; i < mapList.Length - 1; i++)
                {
                    if (_proposedMaps[i] == null)
                    {
                        if (newMapList.Count > 0)
                        {
                            var rand = new Random().Next(newMapList.Count);
                            _proposedMaps[i] = newMapList[rand];
                            newMapList.RemoveAt(rand);
                        }
                        else
                        {
                            var unplayedMaps = _playedMaps.Except(_proposedMaps).Where(map => map != NativeAPI.GetMapName())
                                .ToList();
                            if (unplayedMaps.Count > 0)
                            {
                                var rand = new Random().Next(unplayedMaps.Count);
                                _proposedMaps[i] = unplayedMaps[rand];
                                unplayedMaps.RemoveAt(rand);
                            }
                        }
                    }
                    nominateMenu.AddMenuOption($"{_proposedMaps[i]}", (controller, option) =>
                    {
                        if (!optionCounts.TryGetValue(option.Text, out int count))
                            optionCounts[option.Text] = 1;
                        else
                            optionCounts[option.Text] = count + 1;
                        _votedMap++;
                        PrintToChatAll($"{controller.PlayerName} zvolil {option.Text}");
                    });
                }
            }
            else
            {
                for (int i = 0; i < 7; i++)
                {
                    if (_proposedMaps[i] == null)
                    {
                        if (newMapList.Count > 0)
                        {
                            var rand = new Random().Next(newMapList.Count);
                            _proposedMaps[i] = newMapList[rand];
                            newMapList.RemoveAt(rand);
                        }
                        else
                        {
                            var unplayedMaps = _playedMaps.Except(_proposedMaps).Where(map => map != NativeAPI.GetMapName())
                                .ToList();
                            if (unplayedMaps.Count > 0)
                            {
                                var rand = new Random().Next(unplayedMaps.Count);
                                _proposedMaps[i] = unplayedMaps[rand];
                                unplayedMaps.RemoveAt(rand);
                            }
                        }
                    }

                    nominateMenu.AddMenuOption($"{_proposedMaps[i]}", (controller, option) =>
                    {
                        if (!optionCounts.TryGetValue(option.Text, out int count))
                            optionCounts[option.Text] = 1;
                        else
                            optionCounts[option.Text] = count + 1;
                        _votedMap++;
                        PrintToChatAll($"{controller.PlayerName} zvolil {option.Text}");
                    });
                }
            }

            var playerEntities = Utilities.FindAllEntitiesByDesignerName<CCSPlayerController>("cs_player_controller");
            foreach (var player in playerEntities)
            {
                ChatMenus.OpenMenu(player, nominateMenu);
            }

            AddTimer(20.0f, () => TimerVoteMap(forced));
        }

        private void TimerVoteMap(bool forced)
        {
            if (optionCounts.Count == 0 && forced)
            {
                PrintToChatAll("Nebolo dosiahnutÈ potrebnÈ mnoûstvo hlasov, ost·va aktu·lna mapa!");
                ResetData();
                return;
            }

            if (_votedMap == 0 && !forced)
            {
                var random = Random.Shared;
                _selectedMap = _proposedMaps[random.Next(_proposedMaps.Length)];
                PrintToChatAll($"PoËas hlasovania bola zvolen· mapa {_selectedMap}.");
            }

            if (_selectedMap != null && !forced)
            {
                if (IsTimeLimit)
                {
                    AddTimer(_config.VotingTimeInterval * 60.0f, () =>
                    {
                        Server.ExecuteCommand(IsWsMaps(_selectedMap)
                            ? $"ds_workshop_changelevel {_selectedMap}"
                            : $"map {_selectedMap}");
                    });
                    return;
                }

                return;
            }

            _selectedMap = optionCounts.Aggregate((x, y) => x.Value > y.Value ? x : y).Key;

            if (forced)
            {
                PrintToChatAll($"PoËas hlasovania bola zvolen· mapa {_selectedMap}.");

                AddTimer(5, ChangeMapRTV);
                return;
                /*
                Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ =>
                {
                    Server.ExecuteCommand(IsWsMaps(_selectedMap)
                        ? $"ds_workshop_changelevel {_selectedMap}"
                        : $"map {_selectedMap}");
                });              
                return;  
                */
            }

            PrintToChatAll($"PoËas hlasovania bola zvolen· mapa {_selectedMap}.");
            if (!IsTimeLimit) return;
            AddTimer(_config.VotingTimeInterval * 60.0f, () =>
            {
                Server.ExecuteCommand(IsWsMaps(_selectedMap)
                    ? $"ds_workshop_changelevel {_selectedMap}"
                    : $"map {_selectedMap}");
            });
        }
        private void ChangeMapRTV()
        {
            Server.ExecuteCommand(IsWsMaps(_selectedMap)
            ? $"ds_workshop_changelevel {_selectedMap}"
            : $"map {_selectedMap}");
        }
            private bool IsWsMaps(string selectMap)
        {
            // Define the file path
            string rtvmapsfileName = "MatchZy/rtvmaps.cfg";
            string mapsPath = Path.Join(Server.GameDirectory + "/csgo/cfg", rtvmapsfileName);

            //var mapsPath = Path.Combine(ModuleDirectory, "maps.txt");
            var mapList = File.ReadAllLines(mapsPath);

            return mapList.Any(map => map.Trim() == "ws:" + selectMap);
            ;
        }

        private void PrintToChat(CCSPlayerController controller, string msg)
        {
            controller.PrintToChat($"\x08[ \x0CRockTheVote \x08] {msg}");
        }

        private void PrintToChatAll(string msg)
        {
            Server.PrintToChatAll($"\x08[ \x0CRockTheVote \x08] {msg}");
        }

        private Config LoadConfig()
        {
            string settingsfileName = "MatchZy/settings.json";
            string configPath = Path.Join(Server.GameDirectory + "/csgo/cfg", settingsfileName);

            //var configPath = Path.Combine(ModuleDirectory, "settings.json");

            if (!File.Exists(configPath)) return CreateConfig(configPath);

            var config = JsonSerializer.Deserialize<Config>(File.ReadAllText(configPath))!;

            return config;
        }

        private Config CreateConfig(string configPath)
        {
            var config = new Config
            {
                Needed = 0.6,
                VotingRoundInterval = 5,
                VotingTimeInterval = 10,
                RoundsBeforeNomination = 6,
            };

            File.WriteAllText(configPath,
                JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine("[MapChooser] The configuration was successfully saved to a file: " + configPath);
            Console.ResetColor();

            return config;
        }

        private void ResetData()
        {
            _isVotingActive = true;
            _votedMap = 0;
            optionCounts = new Dictionary<string, int>(0);
            _votedRtv = 0;
            var playerEntities = Utilities.FindAllEntitiesByDesignerName<CCSPlayerController>("cs_player_controller");
            foreach (var player in playerEntities)
            {
                _usersArray[player.Index]!.VotedRtv = false;
                _usersArray[player.Index]!.ProposedMaps = null;
            }

            for (var i = 0; i < _proposedMaps.Length; i++)
            {
                _proposedMaps[i] = null;
            }
        }
    }
}

    public class Config
    {
        public int RoundsBeforeNomination { get; set; }
        public float VotingTimeInterval { get; set; }
        public int VotingRoundInterval { get; set; }
        public double Needed { get; set; }
    }

    public class Users
    {
        public required string ProposedMaps { get; set; }
        public bool VotedRtv { get; set; }
    }