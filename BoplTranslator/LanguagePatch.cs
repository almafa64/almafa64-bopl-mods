using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
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
		};
		private static readonly string[] keys = _translationLookUp.Keys.ToArray();
		public static readonly ReadOnlyDictionary<string, string> translationLookUp = new ReadOnlyDictionary<string, string>(_translationLookUp);

		public static readonly List<string[]> languages = [];

		private static int tmpMaxLanguageIndex = 0;

		static MethodInfo _updateText;
		static MethodInfo _localTable;

		public static int MaxOGLanguage { get; private set; }

		public static void Init()
		{
			_updateText = typeof(LocalizedText).GetMethod(nameof(LocalizedText.UpdateText));
			BoplTranslator.harmony.Patch(_updateText, prefix: new HarmonyMethod(Utils.GetMethod(nameof(UpdateTextPatch))));

			_localTable = typeof(LocalizationTable).GetMethod(nameof(LocalizationTable.GetText));
			BoplTranslator.harmony.Patch(_localTable, prefix: new HarmonyMethod(Utils.GetMethod(nameof(GetTextPatch))));

			MaxOGLanguage = Utils.MaxOfEnum<Language>();

			// read languages
			foreach (FileInfo file in BoplTranslator.translationsDir.EnumerateFiles())
			{
				string[] words = new string[keys.Length];
				languages.Add(words);
				foreach (string line in File.ReadLines(file.FullName))
				{
					string[] splitted = line.Split(['='], 2);
					if (splitted.Length == 1) continue;
					string key = splitted[0].Trim();
					string value = splitted[1].Trim().Replace("\\n", "\n");
					int index = Array.FindIndex(keys, e => e.Equals(key));
					if (index == -1) continue;
					words[index] = value;
				}
				for (int i = 0; i < words.Length; i++)
				{
					string word = words[i];
					if (word != null) continue;
					words[i] = _translationLookUp.GetValueSafe(keys[i]);
					BoplTranslator.logger.LogWarning($"No translation for \"{keys[i]}\" in \"{file.Name}\"");
				}
			}
			tmpMaxLanguageIndex = languages.Count + MaxOGLanguage;
		}

		internal static bool UpdateTextPatch(LocalizedText __instance)
		{
			Language currentLanguage = Settings.Get().Language;
			if ((int)currentLanguage <= MaxOGLanguage) return true;
			if ((int)currentLanguage > tmpMaxLanguageIndex) return false;

			// own language
			TMP_FontAsset font = LocalizedText.localizationTable.GetFont(Language.EN, __instance.useFontWithStroke);

			Traverse traverse = Traverse.Create(__instance);
			traverse.Field("currentLanguage").SetValue(currentLanguage);
			TextMeshProUGUI textToLocalize = traverse.Field("textToLocalize").GetValue<TextMeshProUGUI>();
			string enText = traverse.Field("enText").GetValue<string>();
			TextMesh textToLocalize2 = traverse.Field("textToLocalize2").GetValue<TextMesh>();

			if (textToLocalize == null)
			{
				textToLocalize2.text = LocalizedText.localizationTable.GetText(enText, currentLanguage);
				return false;
			}

			textToLocalize.fontStyle = __instance.useFontWithStroke ? FontStyles.Bold : FontStyles.Normal;
			textToLocalize.text = LocalizedText.localizationTable.GetText(enText, currentLanguage);

			if (!__instance.ignoreFontChange && textToLocalize.font != font)
			{
				textToLocalize.font = font;
			}

			return false;
		}

		internal static bool GetTextPatch(LocalizationTable __instance, ref string __result, string __0, Language __1)
		{
			if ((int)__1 <= MaxOGLanguage) return true;

			__result = Traverse.Create(__instance).Method("getText", __0, languages[(int)__1 - MaxOGLanguage - 1]).GetValue<string>();

			return false;
		}
	}
}