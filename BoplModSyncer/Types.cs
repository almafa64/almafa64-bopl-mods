namespace BoplModSyncer
{
	public struct ModNetData(string guid, string version, string link, string hash)
	{
		public string guid = guid;
		public string version = version;
		public string link = link;
		public string hash = hash;
		public bool doInstall = false;
	}

	public struct Mod(string link)
	{
		public string Link { get; internal set; } = link;

		public string Hash { get; internal set; }
		public BepInEx.PluginInfo Plugin { get; internal set; }

		public override readonly string ToString() =>
			$"name: '{Plugin.Metadata.Name}', version: '{Plugin.Metadata.Version}', link: '{Link}', guid: '{Plugin.Metadata.GUID}', hash: '{Hash}'";
	}
}
