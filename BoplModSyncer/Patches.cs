using BepInEx;
using BepInEx.Configuration;
using BoplModSyncer.Utils;
using HarmonyLib;
using Steamworks;
using Steamworks.Data;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BoplModSyncer
{
	internal static class Patches
	{
		private static readonly string checksumField = GameUtils.GenerateField("checksum");
		private static readonly string hostModListField = GameUtils.GenerateField("modlist");
		private static readonly string hostConfigListField = GameUtils.GenerateField("configlist");
		private static readonly string memberHasSyncerField = GameUtils.GenerateField("syncer");

		private static bool firstJoin = true;

		public static void OnEnterLobby_Prefix(Lobby lobby)
		{
			if (SteamManager.LocalPlayerIsLobbyOwner)
			{
				if (firstJoin && Plugin.lastLobbyId.Value != 0)   // rejoin lobby you left
				{
					Plugin.logger.LogMessage("rejoining: " + Plugin.lastLobbyId.Value);
					new Traverse(SteamManager.instance).Method("TryJoinLobby", Plugin.lastLobbyId.Value).GetValue();
				}
				else OnHostJoin(lobby);                           // you are host

				return;
			}

			lobby.SetMemberData(memberHasSyncerField, "1");
			string lobbyHash = lobby.GetData(checksumField);
			firstJoin = false;
			Plugin.lastLobbyId.Value = 0;

			if (lobbyHash == Plugin.CHECKSUM)
				SyncConfigs(lobby);                               // you have same mods as host
			else if (string.IsNullOrEmpty(lobbyHash))
				HostDoesntHaveSyncer(lobby);                      // host didnt install syncer
			else
				ModMismatch(lobby);                               // you dont have same mods as host
		}

		private static void LeaveLobby(string message = null)
		{
			SteamManager.instance.LeaveLobby();
			Plugin.logger.LogWarning("Left lobby because: '" + message + "'");
		}

		private static void HostDoesntHaveSyncer(Lobby lobby)
		{
			LeaveLobby("host doesnt have syncer");

			// --- no host syncer panel ---

			Transform canvas = PanelUtils.GetCanvas();
			GameObject noSyncerPanel = Object.Instantiate(Plugin.noSyncerPanel, canvas);
			PanelMaker.SetupCloseButton(noSyncerPanel);
		}

		private static Dictionary<string, Dictionary<string, string[]>> GetHostConfigs(Lobby lobby)
		{
			string hostConfigListText = lobby.GetData(hostConfigListField);
			Dictionary<string, Dictionary<string, string[]>> hostConfigs = [];

			// guid:entry_name1\ttype1\tvalue1\nentry_name2\ttype2\tvalue2\\
			string[] modSplit = hostConfigListText.Split('\\');
			foreach (string mod in modSplit)
			{
				string[] guidSplit = mod.Split([':'], 2);
				string[] entriesSplit = guidSplit[1].Split('\n');
				Dictionary<string, string[]> entries = [];

				foreach (string entry in entriesSplit)
				{
					string[] entrySplit = entry.Split(['\t'], 3);
					entries.Add(entrySplit[0], [entrySplit[1], entrySplit[2]]);
				}

				hostConfigs.Add(guidSplit[0], entries);
			}
			return hostConfigs;
		}

		private static void InstallMods(OnlineModData[] toInstallMods, LocalModData[] toDeleteMods, Transform canvas)
		{
			Queue<OnlineModData> modsToDownload = new();
			foreach (OnlineModData mod in toInstallMods)
			{
				if (mod.Link != "" && mod.DoInstall) modsToDownload.Enqueue(mod);
			}

			GameObject installingPanel = Object.Instantiate(Plugin.installingPanel, canvas);

			Slider progressBar = PanelMaker.GetProgressBar(installingPanel).GetComponent<Slider>();
			TextMeshProUGUI percentageText = installingPanel.transform.Find("ProgressBar/Percentage").GetComponent<TextMeshProUGUI>();
			TextMeshProUGUI infoText = PanelMaker.GetInfoText(installingPanel);
			TextMeshProUGUI titleText = PanelMaker.GetTitleText(installingPanel);
			TextMeshProUGUI textArea = PanelMaker.GetTextArea(installingPanel).GetComponent<TextMeshProUGUI>();
			Button okButton = PanelMaker.GetOkButtonComp(installingPanel);

			okButton.interactable = false;
			PanelMaker.SetupCloseButton(installingPanel);
			textArea.color = UnityEngine.Color.red;

			void downloadComplete()
			{
				infoText.text = "Press OK to restart game";
				titleText.text = "Downloading completed!";
				okButton.interactable = true;
				okButton.onClick.AddListener(() => GameUtils.RestartGameAfterDownload(toDeleteMods));
			}

			void downloadNext()
			{
				if (!modsToDownload.Any())
				{
					downloadComplete();
					return;
				}

				OnlineModData downloadingMod = modsToDownload.Dequeue();

				WebClient client = new();

				client.OpenReadCompleted += async (sender, args) =>
				{
					try
					{
						if (args.Cancelled) throw new("cancelled");
						if (args.Error != null) throw args.Error;

						// e.g. link: https://thunderstore.io/package/download/almafa64/AtomGrenade/1.0.3/
						string[] linkParts = downloadingMod.Link.Split('/');
						string name = string.Join("-", [linkParts[5], linkParts[6], linkParts[7]]) + ".zip";
						string path = Path.Combine(GameUtils.DownloadedModsPath, name);
						Directory.CreateDirectory(GameUtils.DownloadedModsPath);

						Plugin.logger.LogInfo("downloading: " + path);
						infoText.text = $"{downloadingMod.Guid} v{downloadingMod.Version}";

						using FileStream file = File.Create(path);
						await BaseUtils.CopyToAsync(args.Result, file, new SynchronousProgress<int>(value =>
						{
							progressBar.value = value;
							percentageText.text = $"{value}%";
						}));
					}
					catch (System.Exception ex)
					{
						Plugin.logger.LogError("error while downloading: " + ex);
						textArea.text = ex.Message;
						Button continueButton = installingPanel.transform.Find("Continue Button").GetComponent<Button>();
						continueButton.gameObject.SetActive(true);
						continueButton.onClick.AddListener(() =>
						{
							continueButton.gameObject.SetActive(false);
							textArea.text = "";
							downloadNext();
						});
						return;
					}
					finally
					{
						client.Dispose();
					}
					downloadNext();
				};
				client.OpenReadAsync(new System.Uri(downloadingMod.Link));
			}
			downloadNext();
		}

		private static void ModMismatch(Lobby lobby)
		{
			// ToDo maybe make configs too now so game doesnt need to restart twice

			string hostModListText = lobby.GetData(hostModListField);
			Dictionary<string, Dictionary<string, string[]>> hostConfigs = GetHostConfigs(lobby);

			Plugin.lastLobbyId.Value = lobby.Id;
			LeaveLobby("Missing mods");

			MethodInfo bindMethod = null;
			foreach (var method in typeof(ConfigFile).GetMethods())
			{
				if (method.Name != "Bind") continue;
				ParameterInfo[] pInfos = method.GetParameters();
				if (pInfos[0].ParameterType == typeof(string) && pInfos[3].ParameterType == typeof(ConfigDescription))
				{
					bindMethod = method;
					break;
				}
			}

			// guid,ver,link,hash|
			string[] hostMods = hostModListText.Split(['|'], System.StringSplitOptions.RemoveEmptyEntries);
			OnlineModData[] toInstallMods = new OnlineModData[hostMods.Length];
			int missingModsCount = 0;
			foreach (string hostMod in hostMods)
			{
				string[] datas = hostMod.Split(',');
				OnlineModData modData = new(datas[0], datas[1], datas[2], datas[3]);

				Dictionary<string, string[]> configEntries = hostConfigs[modData.Guid];
				ConfigFile config = new(Path.Combine(Paths.ConfigPath, modData.Guid + ".cfg"), true);
				
				foreach (KeyValuePair<string, string[]> entry in configEntries)
				{
					string[] entrySplit = entry.Key.Split('=');
					object configEntry = bindMethod.MakeGenericMethod(System.Type.GetType(entry.Value[0]))
						.Invoke(config, [entrySplit[0], entrySplit[1], 0, null]);
					(configEntry as ConfigEntryBase).SetSerializedValue(entry.Value[1]);
				}

				config.Save();

				// if guid is in local mods and hash is same then skip
				if (Plugin.mods.TryGetValue(modData.Guid, out LocalModData mod) && mod.Hash == modData.Hash)
					continue;

				toInstallMods[missingModsCount] = modData;
				missingModsCount++;
			}
			System.Array.Resize(ref toInstallMods, missingModsCount);

			// get locally installed mods that the host doesnt have
			LocalModData[] toDeleteMods = Plugin.mods.Values.Where(e => !hostModListText.Contains(e.Hash)).ToArray();

			Plugin.logger.LogWarning("missing:\n\t- " + string.Join("\n\t- ", toInstallMods.Select(m => $"{m.Guid} v{m.Version}")));
			Plugin.logger.LogWarning("to delete:\n\t- " + string.Join("\n\t- ", toDeleteMods.Select(m => $"{m.Plugin.Metadata.GUID} {m.Plugin.Metadata.Version}")));

			// --- missing mods panel ---

			Transform canvas = PanelUtils.GetCanvas();
			GameObject missingModsPanel = Object.Instantiate(Plugin.missingModsPanel, canvas);
			Transform row = missingModsPanel.transform.Find("NeededModList/Viewport/Content/tmprow");
			LinkClicker linkClicker = missingModsPanel.AddComponent<LinkClicker>();

			// close panel with button click
			PanelMaker.SetupCloseButton(missingModsPanel);
			PanelMaker.GetOkButtonComp(missingModsPanel).onClick.AddListener(() => InstallMods(toInstallMods, toDeleteMods, canvas));

			void makeRow(int index, bool toDelete)
			{
				IModData modData = toDelete ? toDeleteMods[index] : toInstallMods[index];

				// create new row
				Transform newRow = Object.Instantiate(row, row.parent);
				newRow.gameObject.SetActive(true);

				TextMeshProUGUI label = newRow.Find("Background/Label").GetComponent<TextMeshProUGUI>();
				Toggle toggle = newRow.GetComponent<Toggle>();

				// com.test.testmod v1.0.0 (a4bf) link
				StringBuilder rowTextBuilder = new StringBuilder(modData.Guid)
					.Append(" v").Append(modData.Version)
					.Append(" (").Append(modData.Hash.Substring(0, 4)).Append(")");

				if (!string.IsNullOrEmpty(modData.Link))
				{
					// get release PAGE of mod
					// e.g.:
					//     download: https://thunderstore.io/package/download/almafa64/AtomGrenade/1.0.3/
					//     release: https://thunderstore.io/c/bopl-battle/p/almafa64/AtomGrenade/
					string[] parts = modData.Link.Split('/');
					string releasePage = $"https://thunderstore.io/c/bopl-battle/p/{parts[5]}/{parts[6]}/";
					rowTextBuilder.Append(" <link=").Append(releasePage).Append("><color=blue><b>link</b></color></link>");
				}

				label.text = rowTextBuilder.ToString();
				linkClicker.textMeshes.Add(label);

				if (toDelete)
				{
					// --- delete row style ---
					label.fontStyle |= FontStyles.Strikethrough;
					toggle.onValueChanged.AddListener((isOn) => toDeleteMods[index].DoDelete = isOn);
					toggle.isOn = true;
					return;
				}

				// --- download row style ---
				// dont give chance to download mod if it isnt offical
				if (!string.IsNullOrEmpty(modData.Link))
				{
					toggle.onValueChanged.AddListener((isOn) => toInstallMods[index].DoInstall = isOn);
					toggle.isOn = true;
				}
				else toggle.interactable = false;
			}

			void makeInstallRow(int index) => makeRow(index, false);
			void makeDeleteRow(int index) => makeRow(index, true);

			for (int i = 0; i < toInstallMods.Length; i++)
			{
				makeInstallRow(i);
			}

			for (int i = 0; i < toDeleteMods.Length; i++)
			{
				makeDeleteRow(i);
			}
		}

		private static void SyncConfigs(Lobby lobby)
		{
			Dictionary<string, Dictionary<string, string[]>> hostConfigs = GetHostConfigs(lobby);

			Directory.CreateDirectory(GameUtils.OldConfigsPath);
			bool configsSynced = true;

			foreach (KeyValuePair<string, LocalModData> mod in Plugin.mods)
			{
				ConfigFile config = mod.Value.Plugin.Instance.Config;

				// copy user configs if they werent already
				string newPath = Path.Combine(GameUtils.OldConfigsPath, Path.GetFileName(config.ConfigFilePath));
				try { File.Copy(config.ConfigFilePath, newPath); }
				catch (IOException) { }

				// turn off auto saving, so file isnt saved after every entry loop
				bool saveOnSet = config.SaveOnConfigSet;
				config.SaveOnConfigSet = false;

				Dictionary<string, string[]> hostEntries = hostConfigs[mod.Key];
				foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> entryDic in config)
				{
					ConfigEntryBase entry = entryDic.Value;
					string value = hostEntries[entryDic.Key.MyToString()][1];

					// dont do anything until there is a difference in a config
					// after that set every config then restart
					if (configsSynced && value != entryDic.Value.GetSerializedValue())
					{
						configsSynced = false;
						Plugin.lastLobbyId.Value = lobby.Id;
						LeaveLobby("syncing configs");
					}
					if (!configsSynced) entry.SetSerializedValue(value);
				}

				config.SaveOnConfigSet = saveOnSet;
				if (!configsSynced) config.Save();
			}

			if (configsSynced) return;

			// --- restart panel ---

			Transform canvas = PanelUtils.GetCanvas();
			GameObject restartPanel = Object.Instantiate(Plugin.restartPanel, canvas);
			PanelMaker.SetupCloseButton(restartPanel);
			Object.Destroy(PanelMaker.GetCancelButtonComp(restartPanel).gameObject);
			PanelMaker.GetOkButtonComp(restartPanel).onClick.AddListener(() => GameUtils.RestartGameAfterSync());
		}

		private static void OnHostJoin(Lobby lobby)
		{
			lobby.SetData(checksumField, Plugin.CHECKSUM);

			StringBuilder modListBuilder = new();
			StringBuilder configListBuilder = new();
			foreach (KeyValuePair<string, LocalModData> modDir in Plugin.mods)
			{
				LocalModData mod = modDir.Value;

				ConfigFile config = mod.Plugin.Instance.Config;

				// guid,ver,link,hash
				modListBuilder.Append(modDir.Key).Append(',')
					.Append(mod.Version).Append(',')
					.Append(mod.Link).Append(',')
					.Append(mod.Hash).Append('|');

				if (config.Count == 0) continue;

				// guid:entry_name1\ttype1\tvalue1\nentry_name2\ttype2\tvalue2\\
				// \t, \n and \\ are blocked by ConfigDefinition so they can be used as a separator
				configListBuilder.Append(modDir.Key).Append(':');
				foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> entryDic in config)
				{
					ConfigEntryBase entry = entryDic.Value;
					
					// get short type name if its built in, else full name with assembly
					string type = entry.SettingType.FullName;
					if (System.Type.GetType(type) == null) type = entry.SettingType.AssemblyQualifiedName;

					configListBuilder.Append(entryDic.Key.MyToString()).Append('\t')
						.Append(type).Append('\t')
						.Append(entry.GetSerializedValue()).Append('\n');
				}
				configListBuilder.RemoveLast().Append('\\');
			}

			if (modListBuilder.Length > 0) modListBuilder.RemoveLast(); // remove last '|'
			lobby.SetData(hostModListField, modListBuilder.ToString());

			if (configListBuilder.Length > 0) configListBuilder.RemoveLast(); // remove last '\\'
			lobby.SetData(hostConfigListField, configListBuilder.ToString());
		}

		internal static void OnLobbyMemberJoinedCallback_Postfix(Lobby lobby, Friend friend)
		{
			// kick those who dont have syncer
			IEnumerator waitForField()
			{
				// wait needed to give time to member to set memberHasSyncerField
				yield return new WaitForSeconds(1);
				if (lobby.GetMemberData(friend, memberHasSyncerField) != "1")
				{
					int playerIndex = SteamManager.instance.connectedPlayers.FindIndex(e => e.id == friend.Id);
					if (playerIndex != -1)
					{
						SteamManager.instance.KickPlayer(playerIndex);
						Plugin.logger.LogWarning($"Kicked \"{friend.Name}\" because he doesnt has syncer!");
					}
				}
			}
			Plugin.plugin.StartCoroutine(waitForField());
		}
	}
}
