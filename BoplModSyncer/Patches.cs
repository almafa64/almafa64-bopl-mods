using BepInEx;
using BepInEx.Configuration;
using BoplModSyncer.Utils;
using HarmonyLib;
using Steamworks;
using Steamworks.Data;
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
        private static readonly string checksumField = "almafa64>checksum";
        private static readonly string hostModListField = "almafa64>modlist";

        public static void OnEnterLobby_Prefix(Lobby lobby)
        {
            string lobbyHash = lobby.MyGetData(checksumField);

            if (SteamManager.LocalPlayerIsLobbyOwner)
                OnHostJoin(lobby);                                // you are host
            else if (lobbyHash == Plugin.CHECKSUM)
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

        private static void InstallMods(ModNetData[] toInstallMods, Mod[] toDeleteMods, Transform canvas)
        {
            Queue<ModNetData> modsToDownload = new();
            foreach (ModNetData mod in toInstallMods)
            {
                if (mod.link != "" && mod.doInstall) modsToDownload.Enqueue(mod);
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

                ModNetData downloadingMod = modsToDownload.Dequeue();

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
                        infoText.text = $"{downloadingMod.guid} v{downloadingMod.version}";

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
                            // ToDo continue to next download
                        });
                        return; // debug
                    }
                    finally
                    {
                        client.Dispose();
                    }
                    downloadNext();
                };
                client.OpenReadAsync(new System.Uri(downloadingMod.link));
            }
            downloadNext();
        }

        private static void ModMismatch(Lobby lobby)
        {
            // ToDo maybe make configs too now so game doesnt need to restart twice

            string hostModListText = lobby.MyGetData(hostModListField);
            string[] hostMods = hostModListText.Split('|');
            ModNetData[] toInstallMods = new ModNetData[hostMods.Length];
            int missingModsCount = 0;
            foreach (string hostMod in hostMods)
            {
                // guid,ver,link,hash
                string[] datas = hostMod.Split(',');
                ModNetData modData = new(datas[0], datas[1], datas[2], datas[3]);

                // skip if mod is client only
                if (Plugin._clientOnlyGuids.Contains(modData.guid))
                    continue;

                // if guid is in local mods and hash is same then skip
                if (Plugin.mods.TryGetValue(modData.guid, out Mod mod) && mod.Hash == modData.hash)
                    continue;

                toInstallMods[missingModsCount] = modData;
                missingModsCount++;
            }
            System.Array.Resize(ref toInstallMods, missingModsCount);

            // get locally installed mods that the host doesnt have
            Mod[] toDeleteMods = Plugin.mods.Values.Where(e => !hostModListText.Contains(e.Hash)).ToArray();

            Plugin.logger.LogWarning("missing:\n\t- " + string.Join("\n\t- ", toInstallMods.Select(m => $"{m.guid} v{m.version}")));
            Plugin.logger.LogWarning("to delete:\n\t- " + string.Join("\n\t- ", toDeleteMods.Select(m => $"{m.Plugin.Metadata.GUID} {m.Plugin.Metadata.Version}")));

            SteamManager.instance.LeaveLobby();

            // --- missing mods panel ---

            Transform canvas = PanelUtils.GetCanvas();
            GameObject missingModsPanel = Object.Instantiate(Plugin.missingModsPanel, canvas);
			Transform row = missingModsPanel.transform.Find("NeededModList/Viewport/Content/tmprow");
            LinkClicker linkClicker = missingModsPanel.AddComponent<LinkClicker>();

            // close panel with button click
            PanelMaker.SetupCloseButton(missingModsPanel);
            PanelMaker.GetButtonComp(missingModsPanel).onClick.AddListener(() => InstallMods(toInstallMods, toDeleteMods, canvas));

            void makeRow(int index, ModNetData[] array)
            {
                ModNetData modData = array[index];

                // create new row
                Transform newRow = Object.Instantiate(row, row.parent);
                newRow.gameObject.SetActive(true);

				TextMeshProUGUI label = newRow.Find("Background/Label").GetComponent<TextMeshProUGUI>();
				Toggle toggle = newRow.GetComponent<Toggle>();

				// com.test.testmod v1.0.0 (a4bf) link
				StringBuilder sb = new StringBuilder(modData.guid)
                    .Append(" v").Append(modData.version)
                    .Append(" (").Append(modData.hash.Substring(0, 4)).Append(")");

                if (modData.link != "")
                {
                    // get release PAGE of mod
                    string releasePage = modData.link.Substring(0, modData.link.LastIndexOf("/"));
                    sb.Append(" <link=").Append(releasePage).Append("><color=blue><b>link</b></color></link>");
                }

                label.text = sb.ToString();
                linkClicker.textMeshes.Add(label);

                // if delete needed mod is installed strikethrough it
                //if (doDeleteMods.Any(e => e.Hash == modData.hash)) label.fontStyle = FontStyles.Strikethrough;

                // dont give chance to download mod if it isnt offical
                if (modData.link != "")
                {
                    toggle.onValueChanged.AddListener((isOn) => array[index].doInstall = isOn);
                    toggle.isOn = true;
                }
                else toggle.interactable = false;
            }

            for (int i = 0; i < toInstallMods.Length; i++)
            {
                makeRow(i, toInstallMods);
            }

            // ToDo make rows for only doDeleteMods
        }

        private static void SyncConfigs(Lobby lobby)
        {
            foreach (KeyValuePair<string, Mod> mod in Plugin.mods)
            {
                ConfigFile config = mod.Value.Plugin.Instance.Config;

                // turn off auto saving to keep users own settings in file
                bool saveOnSet = config.SaveOnConfigSet;
                config.SaveOnConfigSet = false;

                foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> entryDir in config)
                {
                    ConfigEntryBase entry = entryDir.Value;
                    string data = lobby.MyGetData($"{mod.Key}|{entry.Definition}");
                    entry.SetSerializedValue(data);
                }

                config.SaveOnConfigSet = saveOnSet;
            }
        }

        private static void OnHostJoin(Lobby lobby)
        {
            lobby.MySetData(checksumField, Plugin.CHECKSUM);

            StringBuilder sb = new();
            foreach (KeyValuePair<string, Mod> modDir in Plugin.mods)
            {
                Mod mod = modDir.Value;

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
                    lobby.MySetData($"{modDir.Key}|{entry.Definition}", entry.GetSerializedValue());
                }
            }

            sb.Remove(sb.Length - 1, 1); // remove last '|'
            lobby.MySetData(hostModListField, sb.ToString());
        }

        internal static void OnLobbyMemberJoinedCallback_Prefix(Lobby lobby, Friend friend)
        {
            // ToDo somehow kick player who doesnt have syncer (steam api doesnt support this)
            Plugin.logger.LogWarning($"player {friend.Name} joined!");
        }
    }
}
