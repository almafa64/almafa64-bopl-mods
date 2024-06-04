using System.Collections.Generic;
using System.Linq;

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
		public string Name { get; private set; }
		public GameFont Font { get; internal set; }
		public bool Stroke { get; internal set; }

		internal string[] translations; // ToDo remove this
		internal Dictionary<string, string> translationPairs = [];

		internal CustomLanguage(string name, string[] translations, GameFont font = GameFont.English, bool stroke = false)
		{
			if (LanguagePatch.languages.Any(e => e.Name == name))
				throw new System.ArgumentException($"Translation with '{name}' name already exists!");

			Name = name;
			Font = font;
			Stroke = stroke;
			this.translations = translations;
		}

		public CustomLanguage(string name) : this(name, []) { }

		public void AddText(string guid, string text)
		{
			translationPairs.Add(guid, text);
		}
	}
}
