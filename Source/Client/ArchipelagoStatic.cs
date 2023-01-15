using HarmonyLib;
using System.Reflection;
using Verse;

namespace RimworldArchipelago.Client
{
    [StaticConstructorOnStartup]
    public static class ArchipelagoStatic
    {

        static ArchipelagoStatic()
        {
            Log.Message("Loading Archipelago mod.");
            //Log.Message($"Assembly.GetExecutingAssembly: {Assembly.GetExecutingAssembly()?.FullName}");
            //Log.Message($"Assembly.GetCallingAssembly: {Assembly.GetCallingAssembly()?.FullName}");
            //Log.Message($"Assembly.GetEntryAssembly: {Assembly.GetEntryAssembly()?.FullName}");
            //Harmony.DEBUG = true;
            RimWorldArchipelagoMod.Harmony = new Harmony("rimworld.mod.ad.archipelago");
            RimWorldArchipelagoMod.Harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.Message("Archipelago mod loaded.");
        }
    }
}
