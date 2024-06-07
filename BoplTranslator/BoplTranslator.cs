using HarmonyLib;
using System;
using System.Collections.Generic;
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
		public GameFont Font { get; private set; }
		public bool IsReferenced { get; private set; } = false;

		/// <summary>
		/// Creates a new <see cref="CustomLanguage"/> with fallback language (default english) translations
		/// If language is already present than it'll reference it while discarding parameters
		/// </summary>
		/// <param name="name">name of langauge. Will be converted to lowercase</param>
		/// <param name="font"></param>
		public CustomLanguage(string name, GameFont font = GameFont.English) : this(name, font, true) { }

		/// <summary>
		/// Get a translation
		/// </summary>
		/// <param name="guid">translation guid</param>
		/// <returns>translation text</returns>
		public string GetTranslation(string guid) => translationPairs[guid];

		/// <summary>
		/// Edit/Add a translation
		/// </summary>
		/// <param name="guid">translation guid</param>
		/// <param name="newTranslation">translation text. Will be converted to lowercase</param>
		public void EditTranslation(string guid, string newTranslation)
		{
			translationPairs[guid] = newTranslation.ToLower();
		}

		public string this[string guid]
		{
			get => GetTranslation(guid);
			set => EditTranslation(guid, value);
		}

		/// <summary>
		/// Edit multiple translation with a guid-translation dictionary
		/// </summary>
		/// <param name="guidTranslationPair">guid-translation dictionary</param>
		public void EditTranslations(Dictionary<string, string> guidTranslationPair)
		{
			foreach (KeyValuePair<string, string> pair in guidTranslationPair)
			{
				translationPairs[pair.Key] = pair.Value.ToLower();
			}
		}

		internal Dictionary<string, string> translationPairs = [];

		internal CustomLanguage(string name, GameFont font, bool copyFallback)
		{
			name = name.ToLower();

			CustomLanguage language = BoplTranslator.GetCustomLanguage(name);
			if (language != null)
			{
				Name = language.Name;
				Font = language.Font;
				translationPairs = language.translationPairs;
				IsReferenced = true;
				return;
			}

			Name = name;
			Font = font;

			// should only run if user creates new language with API
			if (!copyFallback) return;
			CustomLanguage fallback = LanguagePatch.fallbackLanguage;
			translationPairs = new Dictionary<string, string>(fallback.translationPairs)
			{
				["menu_language"] = name
			};

			LanguagePatch.customLanguages.Add(this);
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
		/// Gets <see cref="CustomLanguage"/> associated with <paramref name="langName"/>
		/// </summary>
		/// <param name="langName">name of the language</param>
		public static CustomLanguage GetCustomLanguage(string langName)
		{
			langName = langName.ToLower();

			CustomLanguage og = LanguagePatch.ogLanguages.Find(e => e.Name == langName);
			if(og != null) return og;

			return LanguagePatch.customLanguages.Find(e => e.Name == langName);
		}

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
