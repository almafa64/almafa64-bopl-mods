using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BoplModSyncer.Utils
{
	internal class PanelMaker
	{
		internal static GameObject currentPanel = null;

		public static GameObject GetTitle(GameObject panel = null) =>
			GetPanel(panel).Find("Title").gameObject;

		public static GameObject GetInfo(GameObject panel = null) =>
			GetPanel(panel).Find("Info").gameObject;

		public static GameObject GetTextArea(GameObject panel = null) =>
			GetPanel(panel).Find("Text").gameObject;

		public static GameObject GetProgressBar(GameObject panel = null) =>
			GetPanel(panel).Find("ProgressBar").gameObject;

		public static GameObject GetModList(GameObject panel = null) =>
			GetPanel(panel).Find("NeededModList").gameObject;

		public static GameObject GetOkButton(GameObject panel = null) =>
			GetPanel(panel).Find("OK Button").gameObject;

		public static GameObject GetCancelButton(GameObject panel = null) =>
			GetPanel(panel).Find("Cancel Button").gameObject;

		public static Slider GetProgressBarComp(GameObject panel = null) =>
			GetProgressBar(panel).GetComponent<Slider>();

		public static TextMeshProUGUI GetTitleText(GameObject panel = null) =>
			GetTitle(panel).GetComponent<TextMeshProUGUI>();

		public static TextMeshProUGUI GetInfoText(GameObject panel = null) =>
			GetInfo(panel).GetComponent<TextMeshProUGUI>();

		public static TextMeshProUGUI GetTextAreaText(GameObject panel = null) =>
			GetTextArea(panel).GetComponent<TextMeshProUGUI>();

		public static Button GetOkButtonComp(GameObject panel = null) =>
			GetOkButton(panel).GetComponent<Button>();

		public static Button GetCancelButtonComp(GameObject panel = null) =>
			GetCancelButton(panel).GetComponent<Button>();

		private static Transform GetPanel(GameObject panel) => 
			(panel ?? currentPanel ?? throw new System.NullReferenceException("panel parameter and current panel is null")).transform;

		private static GameObject CopyGeneric(GameObject genericPanel, string name)
		{
			GameObject panel = Object.Instantiate(genericPanel);
			panel.hideFlags = HideFlags.HideInHierarchy;
			Object.DontDestroyOnLoad(panel);
			panel.name = name;
			return panel;
		}

		private static void SetupButtons(GameObject panel, UnityAction onOkClick)
		{
			void close()
			{
				Object.Destroy(panel);
				currentPanel = null;
			};

			GetOkButtonComp(panel).onClick.AddListener(() =>
			{
				close();
				onOkClick?.Invoke();
			});
			GetCancelButtonComp(panel).onClick.AddListener(() =>
			{
				GameUtils.CancelSyncing();
				close();
			});
		}

		private static TMP_FontAsset GetDefaultFont() =>
			LocalizedText.localizationTable.GetFont(Language.EN, false);

		public static void MakeGenericPanel(ref GameObject genericPanel)
		{
			// add text components into panel
			TextMeshProUGUI title = GetTitle(genericPanel).AddComponent<TextMeshProUGUI>();
			TextMeshProUGUI info = GetInfo(genericPanel).gameObject.AddComponent<TextMeshProUGUI>();
			TextMeshProUGUI ok = genericPanel.transform.Find("OK Button/Text (TMP)").gameObject.AddComponent<TextMeshProUGUI>();
			TextMeshProUGUI cancel = genericPanel.transform.Find("Cancel Button/Text (TMP)").gameObject.AddComponent<TextMeshProUGUI>();
			TextMeshProUGUI text = GetTextArea(genericPanel).gameObject.AddComponent<TextMeshProUGUI>();

			// text mesh pro settings
			title.fontSize = 56;
			title.color = Color.black;
			title.font = GetDefaultFont();
			title.alignment = TextAlignmentOptions.Left;
			title.fontStyle = FontStyles.Bold;

			cancel.fontSize = ok.fontSize = 50;
			cancel.color = ok.color = Color.black;
			cancel.font = ok.font = GetDefaultFont();
			cancel.alignment = ok.alignment = TextAlignmentOptions.Center;
			cancel.fontStyle = ok.fontStyle = FontStyles.Bold;
			ok.text = "OK";
			cancel.text = "Cancel";

			info.fontSize = 50;
			info.color = Color.black;
			info.font = GetDefaultFont();
			info.alignment = TextAlignmentOptions.Left;
			info.fontStyle = FontStyles.Normal;

			text.fontSize = 50;
			text.color = Color.black;
			text.font = GetDefaultFont();
			text.alignment = TextAlignmentOptions.Left;
			text.fontStyle = FontStyles.Normal;
		}

		public static GameObject MakeMissingModsPanel(GameObject genericPanel)
		{
			GameObject panel = CopyGeneric(genericPanel, "MissingModsPanel");

			Object.Destroy(GetProgressBar(panel));
			Object.Destroy(GetTextArea(panel));

			TextMeshProUGUI rowLabel = panel.transform.Find("NeededModList/Viewport/Content/tmprow/Background/Label").gameObject.AddComponent<TextMeshProUGUI>();
			rowLabel.fontSize = 50;
			rowLabel.color = new Color32(50, 50, 50, 255);
			rowLabel.font = GetDefaultFont();
			rowLabel.alignment = TextAlignmentOptions.Left;
			rowLabel.raycastTarget = false;
	
			GetTitleText(panel).text = "Missing mods";
			GetInfoText(panel).text = "Install | Mod data";

			return panel;
		}

		public static GameObject MakeNoSyncerPanel(GameObject genericPanel)
		{
			GameObject panel = CopyGeneric(genericPanel, "HostMissingSyncer");

			Object.Destroy(GetProgressBar(panel));
			Object.Destroy(GetTextArea(panel));
			Object.Destroy(GetModList(panel));

			GetTitleText(panel).text = "Warning!";
			GetInfoText(panel).text = "Host's missing BoplModSyncer";

			return panel;
		}

		public static GameObject MakeInstallingPanel(GameObject genericPanel)
		{
			GameObject panel = CopyGeneric(genericPanel, "InstallingPanel");

			Object.Destroy(GetModList(panel));

			TextMeshProUGUI percentage = panel.transform.Find("ProgressBar/Percentage").gameObject.AddComponent<TextMeshProUGUI>();
			percentage.fontSize = 50;
			percentage.color = Color.black;
			percentage.font = GetDefaultFont();
			percentage.alignment = TextAlignmentOptions.Left;

			GetTitleText(panel).text = "Downloading!";

			GameObject continueButtonObject = Object.Instantiate(GetOkButtonComp(panel).gameObject, panel.transform);
			Button continueButton = continueButtonObject.GetComponent<Button>();

			continueButton.onClick.AddListener(() => continueButton.gameObject.SetActive(false));
			continueButtonObject.SetActive(false);
			continueButtonObject.name = "Continue Button";
			continueButtonObject.transform.Find("Text (TMP)").GetComponent<TextMeshProUGUI>().text = "Continue";

			return panel;
		}

		public static GameObject MakeRestartPanel(GameObject genericPanel)
		{
			GameObject panel = CopyGeneric(genericPanel, "RestartPanel");

			Object.Destroy(GetProgressBar(panel));
			Object.Destroy(GetTextArea(panel));
			Object.Destroy(GetModList(panel));

			GetTitleText(panel).text = "Warning!";
			GetInfoText(panel).text = "Game will be restarted!";

			return panel;
		}

		public static GameObject InstantiatePanel(GameObject panelToInstantiate, UnityAction onOkClick = null)
		{
			if (currentPanel != null) throw new System.Exception($"'{currentPanel.name}' panel is already open!");

			Transform canvas = PanelUtils.GetCanvas();
			currentPanel = Object.Instantiate(panelToInstantiate, canvas);
			SetupButtons(currentPanel, onOkClick);
			return currentPanel;
		}
	}

	internal class PanelUtils
	{
		public static Transform GetCanvas()
		{
			string sceneName = SceneManager.GetActiveScene().name;
			if (sceneName == "MainMenu") return GameObject.Find("Canvas (1)").transform;
			else if (sceneName.Contains("Select")) return GameObject.Find("Canvas").transform;

			GameObject go = GameObject.Find("Canvas");
			if(go != null) return go.transform;
			go = new GameObject("Canvas");
			go.AddComponent<Canvas>();
			return go.transform;
		}
	}
}