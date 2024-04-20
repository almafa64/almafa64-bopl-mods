using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BoplModSyncer
{
	internal class PanelMaker
	{
		private static GameObject CopyGeneric(GameObject genericPanel)
		{
			GameObject panel = Object.Instantiate(genericPanel);
			panel.hideFlags = HideFlags.HideInHierarchy;
			Object.DontDestroyOnLoad(panel);
			return panel;
		}
		
		public static TextMeshProUGUI GetTitleText(GameObject panel) =>
			panel.transform.Find("Title").GetComponent<TextMeshProUGUI>();

		public static TextMeshProUGUI GetInfoText(GameObject panel) =>
			panel.transform.Find("Info").GetComponent<TextMeshProUGUI>();

		public static GameObject GetTextArea(GameObject panel) =>
			panel.transform.Find("Text").gameObject;

		public static GameObject GetProgressBar(GameObject panel) =>
			panel.transform.Find("ProgressBar").gameObject;

		public static GameObject GetModList(GameObject panel) =>
			panel.transform.Find("NeededModList").gameObject;

		public static Button GetButton(GameObject panel) =>
			panel.transform.Find("OK Button").GetComponent<Button>();

		public static void MakeCloseButton(GameObject panel)
		{
			GetButton(panel).onClick.AddListener(() => Object.Destroy(panel));
		}

		private static TMP_FontAsset GetDefaultFont() =>
			LocalizedText.localizationTable.GetFont(Language.EN, false);

		public static void MakeGenericPanel(ref GameObject genericPanel)
		{
			// add text components into panel
			TextMeshProUGUI title = genericPanel.transform.Find("Title").gameObject.AddComponent<TextMeshProUGUI>();
			TextMeshProUGUI info = genericPanel.transform.Find("Info").gameObject.AddComponent<TextMeshProUGUI>();
			TextMeshProUGUI ok = genericPanel.transform.Find("OK Button/Text (TMP)").gameObject.AddComponent<TextMeshProUGUI>();

			// text mesh pro settings
			title.fontSize = 56;
			title.color = Color.black;
			title.font = GetDefaultFont();
			title.alignment = TextAlignmentOptions.BaselineLeft;
			title.fontStyle = FontStyles.Bold;

			ok.fontSize = 50;
			ok.color = Color.black;
			ok.font = GetDefaultFont();
			ok.alignment = TextAlignmentOptions.Center;
			ok.fontStyle = FontStyles.Bold;
			ok.text = "OK";

			info.fontSize = 50;
			info.color = Color.black;
			info.font = GetDefaultFont();
			info.alignment = TextAlignmentOptions.BaselineLeft;
			info.fontStyle = FontStyles.Normal;
		}

		public static GameObject MakeMissingModsPanel(GameObject genericPanel)
		{
			GameObject panel = CopyGeneric(genericPanel);

			Object.Destroy(GetProgressBar(panel));
			Object.Destroy(GetTextArea(panel));

			TextMeshProUGUI rowLabel = panel.transform.Find("NeededModList/Viewport/Content/Toggle/Background/Label").gameObject.AddComponent<TextMeshProUGUI>();
			rowLabel.fontSize = 50;
			rowLabel.color = new Color32(50, 50, 50, 255);
			rowLabel.font = GetDefaultFont();
			rowLabel.alignment = TextAlignmentOptions.MidlineLeft;
			rowLabel.raycastTarget = false;
	
			GetTitleText(panel).text = "Missing mods";
			GetInfoText(panel).text = "Install | Mod data";

			return panel;
		}

		public static GameObject MakeNoSyncerPanel(GameObject genericPanel)
		{
			GameObject panel = CopyGeneric(genericPanel);

			Object.Destroy(GetProgressBar(panel));
			Object.Destroy(GetTextArea(panel));
			Object.Destroy(GetModList(panel));

			GetTitleText(panel).text = "Warning!";
			GetInfoText(panel).text = "Host's missing BoplModSyncer";

			return panel;
		}

		public static GameObject MakeInstallingPanel(GameObject genericPanel)
		{
			GameObject panel = CopyGeneric(genericPanel);

			Object.Destroy(GetTextArea(panel));
			Object.Destroy(GetModList(panel));

			TextMeshProUGUI percentage = panel.transform.Find("ProgressBar/Percentage").gameObject.AddComponent<TextMeshProUGUI>();
			percentage.fontSize = 50;
			percentage.color = Color.black;
			percentage.font = GetDefaultFont();
			percentage.alignment = TextAlignmentOptions.MidlineLeft;

			GetTitleText(panel).text = "Installing!";

			return panel;
		}

		public static GameObject MakeRestartPanel(GameObject genericPanel)
		{
			GameObject panel = CopyGeneric(genericPanel);

			Object.Destroy(GetProgressBar(panel));
			Object.Destroy(GetTextArea(panel));
			Object.Destroy(GetModList(panel));

			GetTitleText(panel).text = "Warning!";
			GetInfoText(panel).text = "Game will be restarted!";

			return panel;
		}
	}
}