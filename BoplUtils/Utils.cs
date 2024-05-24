using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BoplUtils
{
	public class Utils
	{
		public static GameObject GetCanvas()
		{
			string sceneName = SceneManager.GetActiveScene().name;
			if (sceneName == "MainMenu") return GameObject.Find("Canvas (1)");

			GameObject canvas = GameObject.Find("Canvas");
			if (canvas != null) return canvas;
			
			canvas = new GameObject("Canvas");
			canvas.AddComponent<Canvas>();
			CanvasScaler scaler = canvas.AddComponent<CanvasScaler>();
			scaler.referenceResolution = new(4096, 2160);
			scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

			return canvas;
		}
	}
}