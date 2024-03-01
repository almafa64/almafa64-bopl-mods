using BepInEx;

namespace BoplBattleTemplate
{
    [BepInPlugin("com.CREATOR_NAME.PLUGIN_NAME", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Logger.LogMessage($"guid: {Info.Metadata.GUID}, name: {Info.Metadata.Name}, version: {Info.Metadata.Version}");
        }
    }
}