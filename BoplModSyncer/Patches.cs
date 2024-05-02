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
		private static readonly string memberHasSyncerField = GameUtils.GenerateField("syncer");

		private static bool firstJoin = true;

		public static void OnEnterLobby_Prefix(Lobby lobby)
		{
			if (SteamManager.LocalPlayerIsLobbyOwner)
			{
				if (firstJoin && Plugin.lastLobbyId.Value != 0) // rejoining lobby you left
				{
					Plugin.logger.LogWarning("rejoining: " + Plugin.lastLobbyId.Value);
					new Traverse(SteamManager.instance).Method("TryJoinLobby", Plugin.lastLobbyId.Value).GetValue();
					Plugin.lastLobbyId.Value = 0;
				}

				OnHostJoin(lobby);                                // you are host
				return;
			}

			lobby.SetMemberData(memberHasSyncerField, "1");
			string lobbyHash = lobby.GetData(checksumField);
			firstJoin = false;

			if (lobbyHash == Plugin.CHECKSUM)
				SyncConfigs(lobby);                               // you have same mods as host
			else if (lobbyHash == null || lobbyHash == "")
				HostDoesntHaveSyncer(lobby);                      // host didnt install syncer
			else
				ModMismatch(lobby);                               // you dont have same mods as host
		}

		private static void HostDoesntHaveSyncer(Lobby lobby)
		{
			Plugin.logger.LogWarning("host doesnt have syncer");

			SteamManager.instance.LeaveLobby();

			// --- no host syncer panel ---

			Transform canvas = PanelUtils.GetCanvas();
			GameObject noSyncerPanel = Object.Instantiate(Plugin.noSyncerPanel, canvas);
			PanelMaker.SetupCloseButton(noSyncerPanel);
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
			Button okButton = PanelMaker.GetButtonComp(installingPanel);

			okButton.interactable = false;
			PanelMaker.SetupCloseButton(installingPanel);
			textArea.color = UnityEngine.Color.red;

			void downloadComplete()
			{
				infoText.text = "Press OK to restart game";
				titleText.text = "Downloading completed!";
				okButton.interactable = true;
				okButton.onClick.AddListener(() => GameUtils.RestartGame(toDeleteMods));
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

						string name = client.ResponseHeaders["Content-Disposition"];
						name = name.Substring(name.IndexOf("filename=") + 9);
						string path = Path.Combine(Paths.CachePath, PluginInfo.PLUGIN_NAME, name);
						Directory.CreateDirectory(Path.GetDirectoryName(path));

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
			string[] hostMods = hostModListText.Split('|');
			OnlineModData[] toInstallMods = new OnlineModData[hostMods.Length];
			int missingModsCount = 0;
			foreach (string hostMod in hostMods)
			{
				// guid,ver,link,hash
				string[] datas = hostMod.Split(',');
				OnlineModData modData = new(datas[0], datas[1], datas[2], datas[3]);

				// skip if mod is client only
				if (Plugin._clientOnlyGuids.Contains(modData.Guid))
					continue;

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

			Plugin.lastLobbyId.Value = lobby.Id;
			SteamManager.instance.LeaveLobby();

			// --- missing mods panel ---

			Transform canvas = PanelUtils.GetCanvas();
			GameObject missingModsPanel = Object.Instantiate(Plugin.missingModsPanel, canvas);
			Transform row = missingModsPanel.transform.Find("NeededModList/Viewport/Content/tmprow");
			LinkClicker linkClicker = missingModsPanel.AddComponent<LinkClicker>();

			// close panel with button click
			PanelMaker.SetupCloseButton(missingModsPanel);
			PanelMaker.GetButtonComp(missingModsPanel).onClick.AddListener(() => InstallMods(toInstallMods, toDeleteMods, canvas));

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
					string releasePage = modData.Link.Substring(0, modData.Link.LastIndexOf("/"));
					releasePage = releasePage.Remove(releasePage.LastIndexOf("/download"), "/download".Length);
					rowTextBuilder.Append(" <link=").Append(releasePage).Append("><color=blue><b>link</b></color></link>");
				}

				label.text = rowTextBuilder.ToString();
				linkClicker.textMeshes.Add(label);

				// --- delete row style ---
				if(toDelete)
				{
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
			foreach (KeyValuePair<string, LocalModData> mod in Plugin.mods)
			{
				ConfigFile config = mod.Value.Plugin.Instance.Config;

				// turn off auto saving to keep users own settings in file
				bool saveOnSet = config.SaveOnConfigSet;
				config.SaveOnConfigSet = false;

				foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> entryDir in config)
				{
					ConfigEntryBase entry = entryDir.Value;
					string data = lobby.GetData(GameUtils.GenerateField($"{mod.Key}|{entry.Definition}"));
					entry.SetSerializedValue(data);
				}

				config.SaveOnConfigSet = saveOnSet;
			}
		}

		private static void OnHostJoin(Lobby lobby)
		{
			lobby.SetData(checksumField, Plugin.CHECKSUM);

			StringBuilder sb = new();
			foreach (KeyValuePair<string, LocalModData> modDir in Plugin.mods)
			{
				LocalModData mod = modDir.Value;

				ConfigFile config = mod.Plugin.Instance.Config;

				// no built in json lib + cant embed dlls without project restructure = custom format
				// guid,ver,link,hash
				sb.Append(modDir.Key).Append(',')
					.Append(mod.Plugin.Metadata.Version).Append(',')
					.Append(mod.Link).Append(',')
					.Append(mod.Hash)
					.Append("|");

				foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> entryDir in config)
				{
					// guid|full_name_of_entry, value
					ConfigEntryBase entry = entryDir.Value;
					lobby.SetData(GameUtils.GenerateField($"{modDir.Key}|{entry.Definition}"), entry.GetSerializedValue());
				}
			}

			sb.Remove(sb.Length - 1, 1); // remove last '|'
			lobby.SetData(hostModListField, sb.ToString());
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
					if(playerIndex != -1) SteamManager.instance.KickPlayer(playerIndex);
				}
			}
			Plugin.plugin.StartCoroutine(waitForField());
		}
	}
}
