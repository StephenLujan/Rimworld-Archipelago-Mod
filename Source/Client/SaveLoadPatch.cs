using HarmonyLib;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RimworldArchipelago.Client
{
    [HarmonyPatch(typeof(World), nameof(World.ExposeComponents))]
    static class SaveLoadPatch
    {
        //TODO testing
        static void Postfix(World __instance)
        {
            if (RimWorldArchipelagoMod.Session == null) return;

            if (Scribe.mode is LoadSaveMode.Saving)
            {
                var received = RimWorldArchipelagoMod.ReceivedItems.ToList();
                Scribe_Deep.Look(ref received, "ArchipelagoReceviedItems", __instance);
            }
            else if (Scribe.mode is LoadSaveMode.LoadingVars)
            {
                var received = new List<long>();
                Scribe_Deep.Look(ref received, "ArchipelagoReceviedItems", __instance);
                RimWorldArchipelagoMod.ReceivedItems.Clear();
                foreach (var item in received)
                {
                    RimWorldArchipelagoMod.ReceivedItems.Add(item);
                }
            }
        }
    }
}
