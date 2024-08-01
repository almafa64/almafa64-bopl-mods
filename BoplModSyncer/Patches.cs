﻿using BepInEx;
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
		private static bool hostSetupDone = false;
		private static bool isHost = false;

		public static void OnEnterLobby_Prefix(Lobby lobby)
		{
			hostSetupDone = isHost = false;

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
			Plugin.logger.LogWarning($"Left lobby because: '{message}'");
			// ToDo: send this to everyone else
		}

		private static void HostDoesntHaveSyncer(Lobby lobby)
		{
			LeaveLobby("host doesnt have syncer");

			// --- no host syncer panel ---
			PanelMaker.InstantiatePanel(Plugin.noSyncerPanel);
		}

		private static void InstallMods(OnlineModData[] toInstallMods, LocalModData[] toDeleteMods)
		{
			Queue<OnlineModData> modsToDownload = new();
			foreach (OnlineModData mod in toInstallMods)
			{
				if (mod.Link != "" && mod.DoInstall) modsToDownload.Enqueue(mod);
			}

			GameObject installingPanel = PanelMaker.InstantiatePanel(Plugin.installingPanel);

			Slider progressBar = PanelMaker.GetProgressBarComp();
			TextMeshProUGUI percentageText = installingPanel.transform.Find("ProgressBar/Percentage").GetComponent<TextMeshProUGUI>();
			TextMeshProUGUI infoText = PanelMaker.GetInfoText();
			TextMeshProUGUI textArea = PanelMaker.GetTextAreaText();
			Button okButton = PanelMaker.GetOkButtonComp();

			okButton.interactable = false;
			textArea.color = UnityEngine.Color.red;

			Directory.CreateDirectory(GameUtils.DownloadedModsPath);

			void downloadComplete()
			{
				infoText.text = "Press OK to restart game";
				PanelMaker.GetTitleText().text = "Downloading completed!";
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

						Plugin.logger.LogInfo($"downloading: {name}");
						infoText.text = $"{downloadingMod.Guid} v{downloadingMod.Version}";

						using FileStream file = File.Create(path);
						await BaseUtils.CopyToAsync(args.Result, file, new SynchronousProgress<int>(value =>
						{
							progressBar.value = value;
							percentageText.text = $"{value}%";
						}));

						downloadNext();
					}
					catch (System.Exception ex)
					{
						Plugin.logger.LogError($"error while downloading: {ex}");
						textArea.text = ex.Message;

						Button continueButton = installingPanel.transform.Find("Continue Button").GetComponent<Button>();
						continueButton.gameObject.SetActive(true);
						continueButton.onClick.AddListener(() =>
						{
							continueButton.gameObject.SetActive(false);
							textArea.text = "";
							downloadNext();
						});
					}
					finally { client.Dispose(); }
				};

				client.OpenReadAsync(new System.Uri(downloadingMod.Link));
			}

			downloadNext();
		}

		private static void ModMismatch(Lobby lobby)
		{
			string hostModListText = lobby.GetData(hostModListField);
			string hostConfigListText = lobby.GetData(hostConfigListField);

			Plugin.lastLobbyId.Value = lobby.Id;
			LeaveLobby("Missing mods");

			MethodInfo bindMethod = null;
			foreach (var method in typeof(ConfigFile).GetMethods())
			{
				if (method.Name != "Bind") continue;
				ParameterInfo[] pInfos = method.GetParameters();
				if (pInfos[0].ParameterType == typeof(ConfigDefinition))
				{
					bindMethod = method;
					break;
				}
			}

			if (bindMethod == null) throw new System.NullReferenceException("No bind method was found");

			// guid,ver,link,hash|
			string[] hostMods = hostModListText.Split(['|'], System.StringSplitOptions.RemoveEmptyEntries);
			OnlineModData[] toInstallMods = new OnlineModData[hostMods.Length];
			int missingModsCount = 0;
			Dictionary<string, HostConfigEntry[]> hostConfigs = GameUtils.GetHostConfigs(hostConfigListText);

			foreach (string hostMod in hostMods)
			{
				string[] datas = hostMod.Split(',');
				OnlineModData modData = new(datas[0], datas[1], datas[2], datas[3]);

				if (hostConfigs.TryGetValue(modData.Guid, out HostConfigEntry[] configEntries))
				{
					ConfigFile config = new(Path.Combine(Paths.ConfigPath, modData.Guid + ".cfg"), true);

					foreach (HostConfigEntry entry in configEntries)
					{
						object configEntry = bindMethod.MakeGenericMethod(entry.Type).Invoke(config, [entry.Definition, 0, null]);
						(configEntry as ConfigEntryBase).SetSerializedValue(entry.Value);
					}

					config.Save();
				}

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
			GameObject missingModsPanel = PanelMaker.InstantiatePanel(
				Plugin.missingModsPanel,
				() => InstallMods(toInstallMods, toDeleteMods));

			Transform row = missingModsPanel.transform.Find("NeededModList/Viewport/Content/tmprow");
			LinkClicker linkClicker = missingModsPanel.AddComponent<LinkClicker>();

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
			Dictionary<string, HostConfigEntry[]> hostConfigs = GameUtils.GetHostConfigs(lobby.GetData(hostConfigListField));
			Dictionary<string, List<KeyValuePair<ConfigEntryBase, string>>> newConfigs = [];

			bool configsSynced = true;

			foreach (KeyValuePair<string, LocalModData> mod in Plugin.mods)
			{
				ConfigFile config = mod.Value.Plugin.Instance.Config;
				List<KeyValuePair<ConfigEntryBase, string>> newConfigEntries = [];

				if (!hostConfigs.TryGetValue(mod.Key, out HostConfigEntry[] hostEntries)) continue;

				foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> entryDic in config)
				{
					ConfigEntryBase entry = entryDic.Value;

					string value = "";
					foreach (HostConfigEntry hostEntry in hostEntries)
					{
						if (!hostEntry.Definition.Equals(entry.Definition)) continue;
						value = hostEntry.Value;
						break;
					}

					// dont do anything until there is a difference in a config
					// after that set every config then restart
					if (configsSynced && value != entry.GetSerializedValue())
					{
						configsSynced = false;
						Plugin.lastLobbyId.Value = lobby.Id;
						LeaveLobby("syncing configs");
					}

					if (!configsSynced) newConfigEntries.Add(new(entry, value));
				}

				if (!configsSynced) newConfigs.Add(mod.Key, newConfigEntries);
			}

			if (configsSynced) return;

			// --- restart panel ---
			GameObject restartPanel = PanelMaker.InstantiatePanel(Plugin.restartPanel, () =>
			{
				Directory.CreateDirectory(GameUtils.OldConfigsPath);

				foreach (KeyValuePair<string, LocalModData> mod in Plugin.mods)
				{
					ConfigFile config = mod.Value.Plugin.Instance.Config;

					// copy user configs if they werent already
					string newPath = Path.Combine(GameUtils.OldConfigsPath, Path.GetFileName(config.ConfigFilePath));
					try { File.Copy(config.ConfigFilePath, newPath); }
					catch (IOException) { }

					// turn off auto saving, so file isnt saved after every entry loop
					config.SaveOnConfigSet = false;

					foreach (KeyValuePair<ConfigEntryBase, string> entry in newConfigs[mod.Key])
					{
						entry.Key.SetSerializedValue(entry.Value);
					}

					config.Save();
				}

				GameUtils.RestartGameAfterSync();
			});
		}

		// ToDo: cache these
		private static void OnHostJoin(Lobby lobby)
		{
			isHost = true;

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

			hostSetupDone = true;
		}

		internal static void OnLobbyMemberJoinedCallback_Postfix(Lobby lobby, Friend friend)
		{
			// lazy to check if this method is run for every connected user or just host
			if(!isHost) return;

			// if host hasnt finished "booting" dont let people join
			if(!hostSetupDone)
			{
				int playerIndex = SteamManager.instance.connectedPlayers.FindIndex(e => e.id == friend.Id);
				if (playerIndex == -1) return;

				SteamManager.instance.KickPlayer(playerIndex);
				Plugin.logger.LogWarning($"Kicked \"{friend.Name}\" because host(you) still havent finished booting!");
				return;
			}

			// ToDo: maybe use Entwined instead?
			// kick those who dont have syncer
			IEnumerator waitForField()
			{
				// wait needed to give time to member to set memberHasSyncerField
				yield return new WaitForSeconds(1);

				if (lobby.GetMemberData(friend, memberHasSyncerField) == "1") yield break;

				int playerIndex = SteamManager.instance.connectedPlayers.FindIndex(e => e.id == friend.Id);
				if (playerIndex == -1) yield break;

				SteamManager.instance.KickPlayer(playerIndex);
				Plugin.logger.LogWarning($"Kicked \"{friend.Name}\" because he doesnt has syncer!");
			}
			Plugin.plugin.StartCoroutine(waitForField());
		}

		internal static void JoinLobby_Prefix()
		{
			// emulate clicking cancel button
			if (PanelMaker.currentPanel != null) PanelMaker.GetCancelButtonComp().onClick.Invoke();
		}
	}
}