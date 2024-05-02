using TMPro;
using UnityEngine.EventSystems;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace BoplModSyncer
{
	internal class LinkClicker : MonoBehaviour, IPointerClickHandler
	{
		public List<TextMeshProUGUI> textMeshes = [];
		public void OnPointerClick(PointerEventData eventData)
		{
			foreach(TextMeshProUGUI textMesh in textMeshes)
			{
				// check if there is a link under clicking position
				int linkIndex = TMP_TextUtilities.FindIntersectingLink(textMesh, eventData.position, Camera.current);
				if (linkIndex == -1) continue;
				TMP_LinkInfo linkInfo = textMesh.textInfo.linkInfo[linkIndex];
				Application.OpenURL(linkInfo.GetLinkID());
				break;
			}
		}
	}
}
