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

		public bool DoDelete { get; internal set; }

		private BepInEx.PluginInfo _plugin;
		public BepInEx.PluginInfo Plugin
		{
			readonly get => _plugin;
			internal set
			{
				_plugin = value;
				// ?. -> if object (here _plugin) is null then returns null, if not then continues the accessing (Metadata.GUID)
				Guid = _plugin?.Metadata.GUID;
				Version = _plugin?.Metadata.Version.ToString();
			}
		}

		public override readonly string ToString() =>
			$"name: '{Plugin.Metadata.Name}', version: '{Version}', link: '{Link}', guid: '{Guid}', hash: '{Hash}'";
	}
}
