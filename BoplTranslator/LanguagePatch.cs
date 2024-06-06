using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BoplTranslator
{
	internal static class LanguagePatch
	{
		private static readonly Dictionary<string, string> _translationLookUp = new()
		{
			{ "menu_language", "en" },
			{ "menu_play", "play" },
			{ "play_start", "start!" },
			{ "menu_online", "online" },
			{ "menu_settings", "settings" },
			{ "menu_exit", "exit" },
			{ "settings_sfx_vol", "sfx\nvol" },
			{ "settings_music_vol", "music\nvol" },
			{ "settings_abilities", "abilities" },
			{ "settings_screen_shake", "screen shake" },
			{ "settings_rumble", "rumble" },
			{ "settings_resolution", "resolution" },
			{ "settings_save", "save" },
			{ "general_on", "on" },
			{ "general_off", "off" },
			{ "general_high", "high" },
			{ "screen_fullscreen", "fullscreen" },
			{ "screen_windowed", "windowed" },
			{ "screen_borderless", "borderless" },
			{ "settings_screen", "screen" },
			{ "play_click", "click to join!" },
			{ "play_ready", "ready!" },
			{ "play_color", "color" },
			{ "play_team", "team" },
			{ "rebind_keys", "rebind keys" },
			{ "rebind_jump", "click jump" },
			{ "rebind_ability_left", "click ability_left" },
			{ "rebind_ability_right", "click ability_right" },
			{ "rebind_ability_top", "click ability_top" },
			{ "rebind_move_left", "click move_left" },
			{ "rebind_move_down", "click move_down" },
			{ "rebind_move_right", "click move_right" },
			{ "rebind_move_up", "click move_up" },
			{ "settings_vsync", "vsync" },
			{ "hide_nothing", "nothing" },
			{ "settings_hide", "hide" },
			{ "hide_names", "names" },
			{ "hide_names_avatars", "names and avatars" },
			{ "undefined_mouse_only", "mouse only" },
			{ "play_local_game", "local game" },
			{ "undefined_click_start", "click to start!" },
			{ "end_next_level", "next level" },
			{ "end_ability_select", "ability select" },
			{ "end_winner", "winner!!" },
			{ "end_winners", "winners!!" },
			{ "end_draw", "draw!" },
			{ "undefined_whishlist", "wishlist bopl battle!" },
			{ "play_choosing", "choosing..." },
			{ "pause_leave", "leave game?" },
			{ "menu_invite", "invite friend" },
			{ "undefined_practice", "practice" },
			{ "tutorial_hold_dow", "hold down" },
			{ "tutorial_aim", "to aim" },
			{ "tutorial_throw_greneade", "to throw grenade" },
			{ "tutorial_dash", "to dash" },
			{ "tutorial_click", "click" },
			{ "menu_credits", "credits" },
			{ "credits_back", "back" },
			{ "menu_tutorial", "tutorial" },
			{ "play_empty_lobby", "your lobby is empty" },
			{ "play_invite", "invite a friend to play online" },
			{ "play_not_abailable_demo", "not available in demo" },
			{ "play_find_players", "find players" },
			{ "play_stop_player_search", "stop search" },
			{ "item_bow", "bow" },
			{ "item_tesla_coil", "tesla coil" },
			{ "item_engine", "engine" },
			{ "item_smoke", "smoke" },
			{ "item_invisibility", "invisibility" },
			{ "item_platform", "platform" },
			{ "item_meteor", "meteor" },
			{ "item_random", "random" },
			{ "item_missile", "missile" },
			{ "item_black_hole", "black hole" },
			{ "item_rock", "rock" },
			{ "item_push", "push" },
			{ "item_dash", "dash" },
			{ "item_grenade", "grenade" },
			{ "item_roll", "roll" },
			{ "item_time_stop", "time stop" },
			{ "item_blink_gun", "blink gun" },
			{ "item_gust", "gust" },
			{ "item_mine", "mine" },
			{ "item_revival", "revival" },
			{ "item_spike", "spike" },
			{ "item_shrink_ray", "shrink ray" },
			{ "item_growth_ray", "growth ray" },
			{ "item_chain", "chain" },
			{ "item_time_lock", "time lock" },
			{ "item_throw", "throw" },
			{ "item_teleport", "teleport" },
			{ "item_grappling_hook", "grappling hook" },
			{ "item_drill", "drill" },
			{ "item_beam", "beam" },
		};

		internal static readonly List<CustomLanguage> ogLanguages = [];
		internal static readonly List<CustomLanguage> customLanguages = [];
		internal static int OGLanguagesCount { get; private set; }

		internal static readonly FieldInfo textField = typeof(LocalizedText).GetField("enText", AccessTools.all);
		
		internal static void Init()
		{
			Plugin.harmony.Patch(
				AccessTools.Method(typeof(LocalizationTable), nameof(LocalizationTable.GetText)),
				prefix: new(typeof(LanguagePatch), nameof(GetText_Prefix))
			);

			Plugin.harmony.Patch(
				AccessTools.Method(typeof(LocalizedText), "Start"),
				prefix: new(typeof(LanguagePatch), nameof(TextStart_Prefix))
			);

			Plugin.harmony.Patch(
				AccessTools.Method(typeof(LocalizationTable), nameof(LocalizationTable.GetFont)),
				prefix: new(typeof(LanguagePatch), nameof(GetFont_Prefix))
			);

			OGLanguagesCount = Utils.MaxOfEnum<Language>();

			string[] translationKeys = [.. _translationLookUp.Keys];
			List<string> translationValues = [.. _translationLookUp.Values];

			// --- load built-in languages ---

			LocalizationTable table = Resources.FindObjectsOfTypeAll<LocalizationTable>()[0];

			void MakeCustomFromBuiltIn(string[] translations, GameFont font)
			{
				CustomLanguage language = new(translations[0], font, false);

				for (int i = 0; i < translations.Length; i++)
				{
					// add dict entry by searching for original text in _translationLookUp and using the key from it
					string text = table.en[i];
					int valueIndex = translationValues.FindIndex(e => e == text);
					if (valueIndex == -1) continue;
					language.translationPairs.Add(translationKeys[valueIndex], translations[i]);
				}

				ogLanguages.Add(language);
			}

			/// IMPORTANT: order is based on <see cref="Language"/> 
			MakeCustomFromBuiltIn(table.en, GameFont.English);
			MakeCustomFromBuiltIn(table.de, GameFont.English);
			MakeCustomFromBuiltIn(table.es, GameFont.English);
			MakeCustomFromBuiltIn(table.fr, GameFont.English);
			MakeCustomFromBuiltIn(table.it, GameFont.English);
			MakeCustomFromBuiltIn(table.jp, GameFont.Japan);
			MakeCustomFromBuiltIn(table.ko, GameFont.Korean);
			MakeCustomFromBuiltIn(table.pl, GameFont.Poland);
			MakeCustomFromBuiltIn(table.ptbr, GameFont.Poland);
			MakeCustomFromBuiltIn(table.ru, GameFont.Russian);
			MakeCustomFromBuiltIn(table.se, GameFont.English);
			MakeCustomFromBuiltIn(table.tr, GameFont.Poland);
			MakeCustomFromBuiltIn(table.zhcn, GameFont.Chinese);
			MakeCustomFromBuiltIn(table.zhtw, GameFont.Chinese);

			// --- read languages from files ---

			foreach (FileInfo file in Plugin.translationsDir.EnumerateFiles())
			{
				string[] words = new string[translationKeys.Length];

				CustomLanguage language = new("", GameFont.English, false);

				foreach (string line in File.ReadLines(file.FullName))
				{
					// syntaxt: "guid.of.translation = something very funny"

					string[] splitted = line.Split(['='], 2, System.StringSplitOptions.RemoveEmptyEntries);
					if (splitted.Length < 2) continue;

					string key = splitted[0].Trim();
					string value = splitted[1].Trim().Replace(@"\n", "\n");

					language.translationPairs.Add(key, value);

					int index = System.Array.FindIndex(translationKeys, e => e.Equals(key));
					if (index == -1) continue;

					words[index] = value;
				}

				if (!language.translationPairs.TryGetValue("menu_language", out string languageName))
				{
					Plugin.logger.LogError("No 'menu_language' entry!");
					continue;
				}

				language.Name = languageName;

				CustomLanguage fallbackLanguage = BoplTranslator.GetCustomLanguage(Plugin.fallbackLanguage.Value);

				for (int i = 0; i < words.Length; i++)
				{
					string word = words[i];
					if (word != null) continue;

					// word at i index was left out of translation file -> add default value from fallback language

					string key = translationKeys[i];
					string defaultText = fallbackLanguage.translationPairs.GetValueSafe(key);

					language.translationPairs.Add(key, defaultText);

					if (!key.StartsWith("undefined"))
						Plugin.logger.LogWarning($"No translation for \"{translationKeys[i]}\" in \"{file.Name}\"");
				}

				customLanguages.Add(language);
			}
		}

		internal static bool GetText_Prefix(LocalizationTable __instance, ref string __result, string enText, Language lang)
		{
			if (enText == null) return false;
			__result = enText;

			CustomLanguage customLanguage = BoplTranslator.GetCustomLanguage(lang);
			if (customLanguage == null) return false;

			// enText should be guid for this
			if (customLanguage.translationPairs.TryGetValue(enText, out string translatedText))
			{
				// found translation for language
				__result = translatedText;
				return false;
			}

			// try getting with built in english translation if text was not transformed into guid
			foreach(KeyValuePair<string, string> translationPair in _translationLookUp)
			{
				if (translationPair.Value != enText) continue;

				__result = customLanguage.translationPairs[translationPair.Key];
				return false;
			}

			return false;
		}

		internal static void TextStart_Prefix(LocalizedText __instance)
		{
			string ogText = textField.GetValue(__instance) as string;
			if (ogText == null) return;

			// ToDo check if enText is already guid

			try
			{
				// change enText field to guid
				string newText = _translationLookUp.First(e => e.Value == ogText).Key;
				textField.SetValue(__instance, newText);
			}
			catch (System.InvalidOperationException) { }
		}

		internal static void GetFont_Prefix(ref Language lang)
		{
			if (!lang.IsCustomLanguage()) return;

			CustomLanguage customLanguage = GetCustomLanguage(lang);

			switch (customLanguage.Font)
			{
				case GameFont.English: lang = Language.EN; break;
				case GameFont.Japan: lang = Language.JP; break;
				case GameFont.Korean: lang = Language.KO; break;
				case GameFont.Russian: lang = Language.RU; break;
				case GameFont.Chinese: lang = Language.ZHCN; break;
				case GameFont.Poland: lang = Language.PL; break;
			}
		}

		internal static CustomLanguage GetCustomLanguage(Language lang) =>
			customLanguages.ElementAtOrDefault((int)lang - OGLanguagesCount - 1);
	}
}