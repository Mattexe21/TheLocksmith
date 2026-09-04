using BepInEx;
using BepInEx.Logging;
using TheLocksmith.Client.Patches;

namespace TheLocksmith.Client;

[BepInPlugin("Mattexe.TheLocksmith.Client", "TheLocksmith.Client", "1.0.1")]
public class Plugin : BaseUnityPlugin
{
    public static ManualLogSource? LogSource;

    private void Awake()
    {
        LogSource = Logger;
        try
        {
            new ContainerSchemePatch().Enable();
            new ContainerTransparencyPatch().Enable();
            new InteriorItemLockPatch().Enable();
            LogSource.LogInfo("TheLocksmith.Client: loaded, trader-sold filled containers patched.");
        }
        catch (System.Exception e)
        {
            LogSource.LogError("TheLocksmith.Client: patch failed to apply: " + e);
        }
    }
}
