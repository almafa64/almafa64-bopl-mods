﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine.SceneManagement;
using System.Collections;
using BepInEx.Logging;

namespace BoplTranslator
{
	[BepInPlugin("com.almafa64.BoplTranslator", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	[BepInProcess("BoplBattle.exe")]
	public class Plugin : BaseUnityPlugin
	{
		internal static Harmony harmony;
		internal static DirectoryInfo translationsDir;
		internal static ConfigFile config;
		internal static ConfigEntry<string> lastCustomLanguageCode;
		internal static ConfigEntry<Language> fallbackLanguage;
		internal static ManualLogSource logger;

		private void Awake()
		{
			translationsDir = new DirectoryInfo(Path.Combine(Path.GetDirectoryName(Info.Location), "translations"));
			translationsDir.Create();

			harmony = new(PluginInfo.PLUGIN_GUID);
			logger = Logger;
			config = Config;
			lastCustomLanguageCode = Config.Bind("store", "last_custom_language_code", "", "dont modify pls");
			fallbackLanguage = Config.Bind("settings", "fallback language", Language.EN, new ConfigDescription("The built-in language to use when translation is not found", new AcceptableLanguageRange()));

			SceneManager.sceneLoaded += OnSceneLoaded;

			try
			{
				LanguagePatch.Init();
			}
			catch (Exception e)
			{
				Logger.LogFatal($"Error at Language: {Environment.NewLine}{e}");
			}
		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			if (scene.name == "MainMenu")
				StartCoroutine(TimeoutSceneLoad());
		}

		private IEnumerator TimeoutSceneLoad()
		{
			// dont create selector if there is no custom language
			if (LanguagePatch.customLanguages.Count == 0)
			{
				// if last language was a custom then set current to fallback
				if ((int)Settings.Get().Language > LanguagePatch.OGLanguagesCount)
				{
					Settings.Get().Language = fallbackLanguage.Value;
					BoplTranslator.UpdateTexts();
				}
				yield break;
			}

			// idk why but without timeout it crashes
			yield return new WaitForSeconds(0.05f);

			// create button for custom language selector
			GameObject langMenu = GameObject.Find("LanguageMenu_leaveACTIVE");
			int lastOGLanguage = langMenu.transform.childCount - 1;
			GameObject langArrowsParent = Instantiate(GameObject.Find("Resolution"));
			GameObject lang = Instantiate(GameObject.Find("en"), langMenu.transform);

			// create arrows for button
			Button[] buttons = langArrowsParent.GetComponentsInChildren<Button>();
			lang.GetComponentInChildren<SelectionBorder>().ButtonsWithTextColors = buttons;
			foreach (Button button in buttons)
			{
				button.transform.SetParent(lang.transform);
				button.transform.localScale = new Vector3(1, 1, 1);
			}
			buttons[0].transform.localPosition = new Vector3(370, 35, 0);
			buttons[1].transform.localPosition = new Vector3(-300, 35, 0);
			Destroy(langArrowsParent);
			Destroy(lang.GetComponent<OptionsButton>());

			// add readed languages
			LanguageSelector selector = lang.AddComponent<LanguageSelector>();
			foreach (CustomLanguage customLang in LanguagePatch.customLanguages)
			{
				selector.languageNames.Add(customLang.Name);
			}

			if (lastCustomLanguageCode.Value == "")
				lastCustomLanguageCode.Value = selector.languageNames[0];

			selector.langMenu = langMenu;
			selector.Init();

			// adds button info into the menu, so it can animate it
			MainMenu menu = langMenu.GetComponent<MainMenu>();
			menu.ConfiguredMenuItems.Add(lang);
			Traverse menuTraverse = Traverse.Create(menu);
			menuTraverse.Field("Indices").GetValue<List<int>>().Add(0);
			menuTraverse.Field("MenuItemTransforms").GetValue<List<RectTransform>>().Add(lang.GetComponent<RectTransform>());
			menuTraverse.Field("MenuItems").GetValue<List<IMenuItem>>().Add(lang.GetComponent<OptionsButton>()); // bs but works

			List<Vector2> poses = menuTraverse.Field("originalMenuItemPositions").GetValue<List<Vector2>>();
			float diff = poses[0].y - poses[2].y;
			poses.Add(new Vector2(0, poses[lastOGLanguage].y - diff));

			lang.name = "Custom Languages";

			// modify events to work with new button
			CallOnHover call = lang.GetComponent<CallOnHover>();
			UnityEvent hover = call.onHover;
			hover.RemoveAllListeners();
			hover.AddListener(() =>
			{
				menu.Select(14);
				selector.InputActive = true;
			});
			call.onExitHover.AddListener(() =>
			{
				selector.InputActive = false;
			});
			UnityEvent click = call.onClick;
			click.RemoveAllListeners();
			click.AddListener(selector.Click);
		}
	}

	public class LanguageSelector : MonoBehaviour, IMenuItem
	{
		public bool InputActive { get; set; }
		private BindInputAxisToButton[] InputArrows;
		public int OptionIndex { get; private set; } = 0;
		public TextMeshProUGUI textMesh;
		public List<string> languageNames = [];
		public GameObject langMenu;

		private void TryFindLastLanguage()
		{
			int index = languageNames.IndexOf(Plugin.lastCustomLanguageCode.Value);
			if (index != -1)
			{
				OptionIndex = index;
				langMenu.GetComponent<LanguageMenu>().SetLanguage(index + LanguagePatch.OGLanguagesCount + 1);
				Plugin.logger.LogInfo($"Found last used language \"{Plugin.lastCustomLanguageCode.Value}\"");
			}
			else
			{
				langMenu.GetComponent<LanguageMenu>().SetLanguage((int)Language.EN);
				OptionIndex = 0;
				Plugin.lastCustomLanguageCode.Value = languageNames[0];
				Plugin.logger.LogError($"Couldn't find last used language \"{Plugin.lastCustomLanguageCode.Value}\"");
			}
		}

		public void Init()
		{
			OptionIndex = (int)Settings.Get().Language;
			if (OptionIndex <= LanguagePatch.OGLanguagesCount)
				OptionIndex = 0; // if last langauge was built-in, set button to first custom language
			else
			{
				OptionIndex -= LanguagePatch.OGLanguagesCount + 1; // button_text_index = current - og.length - 1
				if (OptionIndex >= languageNames.Count)
				{
					// last used langauge isnt present
					Plugin.logger.LogWarning($"Language number {OptionIndex} was selected, but no language with that number exists");
					TryFindLastLanguage();
				}
				else if (languageNames[OptionIndex] != Plugin.lastCustomLanguageCode.Value)
				{
					// last language name isnt the last that is saved in config -> languages were added/removed
					Plugin.logger.LogWarning($"last language wasn't \"{languageNames[OptionIndex]}\", it was \"{Plugin.lastCustomLanguageCode.Value}\"");
					TryFindLastLanguage();
				}

			}

			InputArrows = GetComponentsInChildren<BindInputAxisToButton>();
			textMesh = GetComponentInChildren<TextMeshProUGUI>();
			textMesh.font = LocalizedText.localizationTable.enFontAsset;

			// modify arrows event to switch between languages
			Button left = InputArrows[1].GetComponent<Button>();
			left.onClick.RemoveAllListeners();
			left.onClick.AddListener(Previous);

			Button right = InputArrows[0].GetComponent<Button>();
			right.onClick.RemoveAllListeners();
			right.onClick.AddListener(Next);
		}

		private void Update()
		{
			textMesh.text = languageNames[OptionIndex];
			for (int i = 0; i < InputArrows.Length; i++)
			{
				InputArrows[i].enabled = InputActive;
			}
		}

		public void Next()
		{
			OptionIndex = (OptionIndex + 1) % languageNames.Count;
			Update();
		}

		public void Previous()
		{
			OptionIndex = (OptionIndex - 1 + languageNames.Count) % languageNames.Count;
			Update();
		}

		public void Click()
		{
			if (!InputActive || !isActiveAndEnabled) return;

			langMenu.GetComponent<LanguageMenu>().SetLanguage(LanguagePatch.OGLanguagesCount + 1 + OptionIndex);
			GameObject.Find("mainMenu_leaveACTIVE").GetComponent<MainMenu>().EnableAll();
			langMenu.GetComponent<MainMenu>().DisableAll();
			AudioManager.Get().Play("return3");
			Plugin.lastCustomLanguageCode.Value = languageNames[OptionIndex];
		}
	}

	class AcceptableLanguageRange() : AcceptableValueRange<Language>((Language)Utils.MinOfEnum<Language>(), (Language)Utils.MaxOfEnum<Language>())
	{
		public override string ToDescriptionString()
		{
			return $"# Acceptable langauges: {string.Join(", ", Enum.GetNames(typeof(Language)))}";
		}
	}
}