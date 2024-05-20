using BepInEx.Configuration;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine;
using System;

namespace BoplModSyncer
{
	public interface IModData
	{
		string Guid { get; }
		string Version { get; }
		string Link { get; }
		string Hash { get; }
	}

	public struct OnlineModData(string guid, string version, string link, string hash) : IModData
	{
		public string Guid { get; internal set; } = guid;
		public string Version { get; internal set; } = version;
		public string Link { get; internal set; } = link;
		public string Hash { get; internal set; } = hash;

		public bool DoInstall { get; internal set; } = false;

		public override readonly string ToString() =>
			$"version: '{Version}', link: '{Link}', guid: '{Guid}', hash: '{Hash}'";
	}

	public struct LocalModData(string link) : IModData
	{
		public string Guid { get; private set; }
		public string Version { get; private set; }
		public string Hash { get; internal set; }
		public string Link { get; internal set; } = link;

		public bool DoDelete { get; internal set; } = false;

		private BepInEx.PluginInfo _plugin;
		public BepInEx.PluginInfo Plugin
		{
			readonly get => _plugin;
			internal set
			{
				if (_plugin != null) throw new System.Exception("Plugin was already set!");
				_plugin = value;
				// ??= -> only assign right side if left side is null
				// ?. -> if object (here _plugin) is null then returns null
				// if not then continue the accessing (Metadata.GUID)
				Guid = _plugin?.Metadata.GUID;
				Version ??= _plugin?.Metadata.Version.ToString();
			}
		}

		private Manifest _manifest;
		public Manifest Manifest
		{
			readonly get => _manifest;
			internal set
			{
				if (_manifest != null) throw new System.Exception("Manifest was already set!");
				_manifest = value;
				if(_manifest != null) Version = _manifest.Version;
			}
		}

		public override readonly string ToString() =>
			$"name: '{Plugin.Metadata.Name}', version: '{Version}', link: '{Link}', guid: '{Guid}', hash: '{Hash}'";
	}

	public struct HostConfigEntry(ConfigDefinition definition, Type type, string value)
	{
		internal Type Type { get; private set; } = type;
		internal string Value { get; private set; } = value;
		internal ConfigDefinition Definition { get; private set; } = definition;
	}

	public class Manifest
	{
		public string Name { get; private set; }
		public string Version { get; private set; }
		public string Website { get; private set; }
		public string Description { get; private set; }
		public ReadOnlyCollection<string> Dependencies { get; private set; }

		public string Directory { get; private set; }

		internal Manifest(ManifestJSON json, string dir)
		{
			Name = json.name;
			Version = json.version_number;
			Website = json.website_url;
			Description = json.description;
			Dependencies = new(json.dependencies);
			Directory = dir;
		}
	}

	internal class ManifestJSON
	{
		public string name = null;
		public string version_number = null;
		public string website_url = null;
		public string description = null;
		public string[] dependencies = null;
	}

	public static class TypeExnteders
	{
		public static StringBuilder RemoveLast(this StringBuilder sb) => sb.Remove(sb.Length - 1, 1);

		public static string MyToString(this ConfigDefinition def) => def.Section + "=" + def.Key;
	}

	internal class LinkClicker : MonoBehaviour, IPointerClickHandler
	{
		public List<TextMeshProUGUI> textMeshes = [];
		public void OnPointerClick(PointerEventData eventData)
		{
			foreach (TextMeshProUGUI textMesh in textMeshes)
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
