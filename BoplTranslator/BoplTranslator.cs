using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace BoplTranslator
{
	public enum GameFont
	{
		English,
		Japan,
		Korean,
		Russian,
		Chinese,
		Poland
	}

	public class CustomLanguage
	{
		public string Name { get; internal set; }
		public GameFont Font { get; internal set; }

		internal Dictionary<string, string> translationPairs = [];

		internal CustomLanguage(string name, GameFont font, bool copyFallback)
		{
			if (LanguagePatch.customLanguages.Any(e => e.Name == name))
				throw new System.ArgumentException($"Translation with '{name}' name already exists!");

			Name = name.ToLower();
			Font = font;

			if (!copyFallback) return;
			CustomLanguage fallback = BoplTranslator.GetCustomLanguage(Plugin.fallbackLanguage.Value);
			translationPairs = new Dictionary<string, string>(fallback.translationPairs)
			{
				["menu_language"] = Name
			};
		}

		/// <summary>
		/// Creates a new <see cref="CustomLanguage"/> with fallback language (default english) values
		/// </summary>
		/// <param name="name"></param>
		/// <param name="font"></param>
		/// <exception cref="System.ArgumentException"></exception>
		public CustomLanguage(string name, GameFont font = GameFont.English) : this(name, font, true) { }

		/// <summary>
		/// Adds a new translation
		/// </summary>
		/// <param name="guid">translation guid</param>
		/// <param name="text">translation text</param>
		/// <returns>true if run without exceptions</returns>
		public bool AddText(string guid, string text)
		{
			try
			{
				translationPairs.Add(guid.ToLower(), text.ToLower());
				return true;
			}
			catch (ArgumentException) { return false; }
		}

		/// <summary>
		/// Adds multiple translation with a guid-translation dictionary
		/// </summary>
		/// <param name="guidTextPair">guid-translation dictionary</param>
		/// <returns>true if run without exceptions</returns>
		public bool AddTexts(Dictionary<string, string> guidTextPair)
		{
			try
			{
				foreach (KeyValuePair<string, string> pair in guidTextPair)
				{
					translationPairs.Add(pair.Key.ToLower(), pair.Value.ToLower());
				}
				return true;
			}
			catch (ArgumentException) { return false; }
		}

		/// <summary>
		/// Edits a translation by <paramref name="guid"/>
		/// </summary>
		/// <param name="guid">translation guid</param>
		/// <param name="newText">translation text</param>
		/// <returns>true if run without exceptions</returns>
		public bool EditText(string guid, string newText)
		{
			try
			{
				translationPairs[guid.ToLower()] = newText.ToLower();
				return true;
			}
			catch (ArgumentException) { return false; }
		}

		/// <summary>
		/// Edits multiple translation with a guid-translation dictionary
		/// </summary>
		/// <param name="guidTextPair">guid-translation dictionary</param>
		/// <returns>true if run without exceptions</returns>
		public bool EditTexts(Dictionary<string, string> guidTextPair)
		{
			try
			{
				foreach (KeyValuePair<string, string> pair in guidTextPair)
				{
					translationPairs[pair.Key.ToLower()] = pair.Value.ToLower();
				}
				return true;
			}
			catch (ArgumentException) { return false; }
		}
	}

	public static class BoplTranslator
	{
		/// <summary>
		/// Updates all LocalizedTexts
		/// </summary>
		public static void UpdateTexts()
		{
			foreach (var text in Resources.FindObjectsOfTypeAll<LocalizedText>())
			{
				if (LanguagePatch.textField.GetValue(text) as string != null)
					text.UpdateText();
			}
		}

		/// <summary>
		/// Checks if <paramref name="lang"/> is built-in or not
		/// </summary>
		/// <param name="lang">(can be bigger than max of <see cref="Language"/>)</param>
		/// <returns>false if built-in, true otherwise</returns>
		public static bool IsCustomLanguage(this Language lang) => (int)lang > LanguagePatch.OGLanguagesCount;

		/// <summary>
		/// Gets <see cref="CustomLanguage"/> associated with <paramref name="lang"/>
		/// </summary>
		/// <param name="lang">(can be bigger than max of <see cref="Language"/>)</param>
		public static CustomLanguage GetCustomLanguage(Language lang) =>
			lang.IsCustomLanguage() ? LanguagePatch.GetCustomLanguage(lang) : LanguagePatch.ogLanguages[(int)lang];

		/// <summary>
		/// Gets currently selected <see cref="CustomLanguage"/>
		/// </summary>
		public static CustomLanguage GetCurrentLanguage() => GetCustomLanguage(Settings.Get().Language);

		/// <summary>
		/// Attaches <see cref="LocalizedText"/> to <paramref name="textObject"/>
		/// </summary>
		/// <param name="textObject">the <see cref="GameObject"/> to use</param>
		/// <param name="translationGUID">the guid of the translation</param>
		/// <param name="stroke">should text have an outline</param>
		/// <exception cref="ArgumentNullException"></exception>
		public static void AttachLocalizedText(GameObject textObject, string translationGUID, bool stroke = false)
		{
			if (textObject == null) throw new ArgumentNullException(nameof(textObject));
			
			LocalizedText old = textObject.GetComponent<LocalizedText>();

			LocalizedText localizedText = old ?? textObject.AddComponent<LocalizedText>();
			Traverse traverse = new(localizedText);

			LanguagePatch.textField.SetValue(localizedText, translationGUID);
			localizedText.useFontWithStroke = stroke;
			traverse.Field("textToLocalize").SetValue(textObject.GetComponent<TextMeshProUGUI>());
			traverse.Field("textToLocalize2").SetValue(textObject.GetComponent<TextMesh>());

			if (old) old.UpdateText();
		}
	}
}
