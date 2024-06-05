using System.Collections.Generic;
using System.Linq;
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

		public CustomLanguage(string name, GameFont font = GameFont.English)
		{
			if (LanguagePatch.customLanguages.Any(e => e.Name == name))
				throw new System.ArgumentException($"Translation with '{name}' name already exists!");

			Name = name;
			Font = font;
		}

		/// <summary>
		/// Adds a new translation for language
		/// </summary>
		/// <param name="guid"></param>
		/// <param name="text"></param>
		public void AddText(string guid, string text)
		{
			translationPairs.Add(guid, text);
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
		/// <param name="lang"></param>
		/// <returns>true if built-in, false otherwise</returns>
		public static bool IsCustomLanguage(this Language lang) => (int)lang > LanguagePatch.OGLanguagesCount;

		/// <summary>
		/// Gets <see cref="CustomLanguage"/> associated with <paramref name="lang"/>
		/// </summary>
		/// <param name="lang"></param>
		/// <returns></returns>
		public static CustomLanguage GetCustomLanguage(Language lang) =>
			lang.IsCustomLanguage() ? LanguagePatch.GetCustomLanguage(lang) : LanguagePatch.ogLanguages[(int)lang];

		public static CustomLanguage GetCurrentLanguage() => GetCustomLanguage(Settings.Get().Language);
	}
}
