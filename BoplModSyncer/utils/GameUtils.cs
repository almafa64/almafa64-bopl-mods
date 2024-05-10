using BepInEx;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BoplModSyncer.Utils
{
	internal static class GameUtils
	{
		const string DataPrefix = "almafa64>";

		public static void RestartGame(LocalModData[] toDeleteMods)
		{
			int gameId = Plugin.IsDemo ? 2494960 : 1686940;
			int pid = Process.GetCurrentProcess().Id;
			string guid = Plugin.plugin.Info.Metadata.GUID;
			string path = Path.Combine(Paths.CachePath, "mod_installer_deleter.bat");
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
				cd BepInEx\cache 2>nul
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
				if not exist "..\old_plugins" (
					echo\
					echo [92mcopying old mods to new folder[0m
					robocopy ..\plugins\ ..\old_plugins\ /e /ndl /nc /ns
					call :err_check
				)

				echo\
				echo [92mdeleting not needed mods[0m
				for %%f in ({toDelete}) do del ..\plugins\%%f

				call :err_check

				echo\
				echo [92mcopying downloaded dlls into mod folder[0m
				rem for /r %%f in ({guid}\*.dll) do robocopy "%%~dpf " "..\plugins\%%~nf" "%%~nxf" /ndl /nc /ns
				cd ..\plugins
				for /r %%f in (..\cache\{guid}\*.zip) do (
					md "%%~nf"
					cd "%%~nf"
					tar -xvf "%%f"
					cd ..
				)
				cd ..\cache

				call :err_check
				pause
				echo\
				echo [92mcleanup[0m
				rmdir /s /q "{guid}"

				call :err_check

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
			// ToDo better search algorithm
			string dir = Path.GetDirectoryName(plugin.Location);
			string path = Path.Combine(dir, "manifest.json");
			try
			{
				string text = File.ReadAllText(path);
				return new Manifest(JsonUtility.FromJson<ManifestJSON>(text), dir);
			}
			catch (Exception ex)
			{
				Plugin.logger.LogError(ex);
				return null;
			}
		}
	}
}
