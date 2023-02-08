using Archipelago.MultiClient.Net.Helpers;
using HugsLib.Utils;
using Newtonsoft.Json;
using RimWorld.Planet;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimworldArchipelago.Client
{

    public static class ArchipelagoWorldComp
    {
        private static ModLogger Log => Main.Instance.Log;
        internal static HashSet<long> ItemsAwaitingReceipt = new HashSet<long>();

        private static ArchipelagoWorldData comp => Find.World?.GetComponent<ArchipelagoWorldData>();
        
        public static void ReceiveItem(long archipelagoItemId)
        {
            // check that we are actually ready to receive items
            if (Find.AnyPlayerHomeMap == null || comp == null)
            {
                ItemsAwaitingReceipt.Add(archipelagoItemId);
                Log.Message($"Could not yet receive Archipelago item {archipelagoItemId}");
                return;
            }
            comp.ReceiveItem(archipelagoItemId);
        }
    }

    public class ArchipelagoWorldData : RimWorld.Planet.WorldComponent
    {
        private static ModLogger Log => Main.Instance.Log;

        private HashSet<long> ReceivedItems = new HashSet<long>();

        public ArchipelagoWorldData(World world) : base(world)
        {

        }



        public void ReceiveItem(long archipelagoItemId)
        {
            if (ArchipelagoWorldComp.ItemsAwaitingReceipt.Contains(archipelagoItemId))
            {
                ArchipelagoWorldComp.ItemsAwaitingReceipt.Remove(archipelagoItemId);
            }
            ReceivedItems.Add(archipelagoItemId);

            if (Main.Instance.ArchipeligoItemIdToRimWorldDef.ContainsKey(archipelagoItemId))
            {
                var defMapping = Main.Instance.ArchipeligoItemIdToRimWorldDef[archipelagoItemId];
                var defName = defMapping.DefName;
                var defType = defMapping.DefType;

                // TODO something other than ResearchProjectDef
                if (defType == "ResearchProjectDef")
                {
                    var def = DefDatabase<ResearchProjectDef>.GetNamed(defName, true);
                    Find.ResearchManager.FinishProject(def);
                }
                else
                {
                    Log.Error($"Unrecognized RimWorld DefType {defType} associated with Archipelago item id {archipelagoItemId}");
                }
            }
            else
            {
                Log.Error($"Could not find RimWorld Def associated with Archipelago item id {archipelagoItemId}");
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref ReceivedItems, "Archipelago_ReceivedItems");
            Scribe_Collections.Look(ref ReceivedItems, "Archipelago_ItemsAwaitingReceipt");
        }

        public override void WorldComponentUpdate()
        {
            base.WorldComponentUpdate();
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
        }
    }
}
