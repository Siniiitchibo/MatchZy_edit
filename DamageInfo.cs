using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;


namespace MatchZy
{

    public partial class MatchZy
    {

        private void InitPlayerDamageInfo()
        {
            foreach (var key in playerData.Keys) {
                if (playerData[key].IsBot) continue;
                int attackerId = key;
                foreach (var key2 in playerData.Keys) {
                    if (key == key2) continue;
                    if (playerData[key2].IsBot) continue;
                    if (playerData[key].TeamNum == playerData[key2].TeamNum) continue;
                    if (playerData[key].TeamNum == 2) {
                        if (playerData[key2].TeamNum != 3) continue;
                        int targetId = key2;
                        if (!playerDamageInfo.TryGetValue(attackerId, out var attackerInfo))
                            playerDamageInfo[attackerId] = attackerInfo = new Dictionary<int, DamagePlayerInfo>();

                        if (!attackerInfo.TryGetValue(targetId, out var targetInfo))
                            attackerInfo[targetId] = targetInfo = new DamagePlayerInfo();
                    } else if (playerData[key].TeamNum == 3) {
                        if (playerData[key2].TeamNum != 2) continue;
                        int targetId = key2;
                        if (!playerDamageInfo.TryGetValue(attackerId, out var attackerInfo))
                            playerDamageInfo[attackerId] = attackerInfo = new Dictionary<int, DamagePlayerInfo>();

                        if (!attackerInfo.TryGetValue(targetId, out var targetInfo))
                            attackerInfo[targetId] = targetInfo = new DamagePlayerInfo(); 
                    }
                }
            }
        }

		public Dictionary<int, Dictionary<int, DamagePlayerInfo>> playerDamageInfo = new Dictionary<int, Dictionary<int, DamagePlayerInfo>>();
		private void UpdatePlayerDamageInfo(EventPlayerHurt @event, int targetId)
		{
			int attackerId = (int)@event.Attacker.UserId!;
			if (!playerDamageInfo.TryGetValue(attackerId, out var attackerInfo))
				playerDamageInfo[attackerId] = attackerInfo = new Dictionary<int, DamagePlayerInfo>();

			if (!attackerInfo.TryGetValue(targetId, out var targetInfo))
				attackerInfo[targetId] = targetInfo = new DamagePlayerInfo();

			targetInfo.DamageHP += @event.DmgHealth;
			targetInfo.Hits++;
		}

        private void ShowDamageInfo()
		{
			HashSet<(int, int)> processedPairs = new HashSet<(int, int)>();

			foreach (var entry in playerDamageInfo)
			{
				int attackerId = entry.Key;
				foreach (var (targetId, targetEntry) in entry.Value)
				{
					if (processedPairs.Contains((attackerId, targetId)) || processedPairs.Contains((targetId, attackerId)))
						continue;

					// Access and use the damage information as needed.
					int damageGiven = targetEntry.DamageHP;
					int hitsGiven = targetEntry.Hits;
					int damageTaken = 0;
					int hitsTaken = 0;

					if (playerDamageInfo.TryGetValue(targetId, out var targetInfo) && targetInfo.TryGetValue(attackerId, out var takenInfo))
					{
						damageTaken = takenInfo.DamageHP;
						hitsTaken = takenInfo.Hits;
					}

					var attackerController = Utilities.GetPlayerFromUserid(attackerId);
					var targetController = Utilities.GetPlayerFromUserid(targetId);

					if (attackerController != null && targetController != null)
					{
						int attackerHP = attackerController.PlayerPawn.Value.Health < 0 ? 0 : attackerController.PlayerPawn.Value.Health;
						string attackerName = attackerController.PlayerName;

						int targetHP = targetController.PlayerPawn.Value.Health < 0 ? 0 : targetController.PlayerPawn.Value.Health;
						string targetName = targetController.PlayerName;

                        attackerController.PrintToChat($"===> {ChatColors.Lime}To: [{damageGiven} / {hitsGiven} hits] From: [{damageTaken} / {hitsTaken} hits] - {ChatColors.Gold}{targetName} - ({targetHP} HP){ChatColors.Default}");
						targetController.PrintToChat($"===> {ChatColors.Lime}To: [{damageTaken} / {hitsTaken} hits] From: [{damageGiven} / {hitsGiven} hits] - {ChatColors.Gold}{attackerName} - ({attackerHP} HP){ChatColors.Default}");
					}

					// Mark this pair as processed to avoid duplicates.
					processedPairs.Add((attackerId, targetId));
				}
			}
			playerDamageInfo.Clear();
		}
    }

	public class DamagePlayerInfo
	{
		public int DamageHP { get; set; } = 0;
		public int Hits { get; set; } = 0;
	}
}
