using BepInEx;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using UnityEngine;

namespace BoplModSyncer.Utils
{
	internal static class GameUtils
	{
		const string DataPrefix = "almafa64>";

		public static string MyCachePath { get; private set; }
		public static string DownloadedModsPath { get; private set; }
		public static string OldConfigsPath { get; private set; }
		public static string OldPluginPath { get; private set; }

		internal static void Init()
		{
			MyCachePath = Path.Combine(Paths.CachePath, Plugin.plugin.Info.Metadata.GUID);
			DownloadedModsPath = Path.Combine(MyCachePath, "downloaded");
			OldConfigsPath = Path.Combine(MyCachePath, "configs_old");
			OldPluginPath = Path.Combine(MyCachePath, "plugins_old");
		}

		public static void CancelSyncing(WebClient client = null)
		{
			Plugin.lastLobbyId.Value = 0;
			client?.CancelAsync();
			try { Directory.Delete(DownloadedModsPath, true); }
			catch (DirectoryNotFoundException) { }
		}

		public static void RestartGameAfterDownload(LocalModData[] toDeleteMods)
		{
			int gameId = Plugin.IsDemo ? 2494960 : 1686940;
			int pid = Process.GetCurrentProcess().Id;
			string guid = Plugin.plugin.Info.Metadata.GUID;
			
			string path = Path.Combine(MyCachePath, "mod_installer_deleter.bat");
			string myCachePath = Path.GetFileName(MyCachePath);
			string downloadPath = Path.GetFileName(DownloadedModsPath);
			string oldPluginPath = Path.GetFileName(OldPluginPath);

			string toDelete = ";";
			if (toDeleteMods != null && toDeleteMods.Length > 0)
			{
				toDelete = '"' + string.Join(
					"\";\"",
					toDeleteMods.Select(e => BaseUtils.GetRelativePath(e.Plugin.Location, Paths.PluginPath + "\\"))
				) + '"';
			}

			/**
			 * 1. wait until game is closed
			 * 2. copy old mods to tmp folder for safe keeping
			 * 3. delete not needed mods
			 * 4. copy downloaded mods to plugins
			 * 5. delete download folder
			 * 6. start game
			 */
			File.WriteAllText(path, $"""
				@echo off
				cd BepInEx\cache\{Path.GetFileName(MyCachePath)} 2>nul
				goto :start

				:err_check
				if not "%errorlevel%" == "0" (
					echo\
					echo [91mthere was an error code: %errorlevel%![0m
					pause
					exit 0
				)
				exit /b 0

				rem wait for game to finish
				:start
				tasklist /fi "pid eq {pid}" /fo csv 2>nul | find /i "{pid}" > nul
				if "%ERRORLEVEL%"=="0" goto start
				set "errorlevel=0"

				call :err_check

				rem only copy mods if there is no copy already
				if not exist "{oldPluginPath}" (
					echo\
					echo [92mcopying old mods to new folder[0m
					robocopy ..\..\plugins\ {oldPluginPath}\ /e /ndl /nc /ns
					call :err_check
				)

				echo\
				echo [92mdeleting not needed mods[0m
				for %%f in ({toDelete}) do del ..\..\plugins\%%f

				call :err_check

				echo\
				echo [92mcopying downloaded dlls into mod folder[0m
				cd ..\..\plugins
				for /r %%f in (..\cache\{myCachePath}\{downloadPath}\*.zip) do (
					md "%%~nf"
					cd "%%~nf"
					tar -xvf "%%f"
					cd ..
				)
				cd ..\cache\{Path.GetFileName(MyCachePath)}

				call :err_check

				echo\
				echo [92mcleanup[0m
				rmdir /s /q "{downloadPath}"

				call :err_check

				echo\
				echo [91mRestarting game![0m
				start "" steam://rungameid/{gameId}
				""");
			Process.Start(path);
			Application.Quit();
		}

		public static void RestartGameAfterSync()
		{
			int gameId = Plugin.IsDemo ? 2494960 : 1686940;
			int pid = Process.GetCurrentProcess().Id;
			string path = Path.Combine(MyCachePath, "syncer.bat");

			/**
			 * 1. wait until game is closed
			 * 2. start game
			 */
			File.WriteAllText(path, $"""
				@echo off

				rem wait for game to finish
				:start
				tasklist /fi "pid eq {pid}" /fo csv 2>nul | find /i "{pid}" > nul
				if "%ERRORLEVEL%"=="0" goto start
				set "errorlevel=0"

				timeout 2

				echo\
				echo [91mRestarting game![0m
				start "" steam://rungameid/{gameId}
				""");
			Process.Start(path);
			Application.Quit();
		}

		public static string GenerateField(string key) => DataPrefix + key;

		public static Manifest GetManifest(BepInEx.PluginInfo plugin)
		{
			string dir = plugin.Location;
			// loop until at top of plugins folder (dir length is plugin path length)
			while((dir = Path.GetDirectoryName(dir)).Length > Paths.PluginPath.Length)
			{
				string path = Path.Combine(dir, "manifest.json");
				try
				{
					// todo check if found manifest has any connection to plugin (e.g.: mod is in mod)
					string text = File.ReadAllText(path);
					return new Manifest(JsonUtility.FromJson<ManifestJSON>(text), dir);
				}
				catch (FileNotFoundException) { }
				catch (Exception ex)
				{
					Plugin.logger.LogError(ex);
					break;
				}
			}
			return null;
		}
	}
}
