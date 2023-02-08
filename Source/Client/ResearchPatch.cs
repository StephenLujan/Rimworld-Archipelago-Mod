using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimworldArchipelago.Client
{
    [HarmonyPatch(typeof(ResearchManager))]
    [HarmonyPatch(nameof(ResearchManager.FinishProject))]
    public static class ResearchPatch
    {
        //public void FinishProject(ResearchProjectDef proj, bool doCompletionDialog = false, Pawn researcher = null, bool doCompletionLetter = true)
        public static bool Prefix(ref ResearchProjectDef proj, ref bool doCompletionDialog, ref Pawn researcher, ref bool doCompletionLetter)
        {
            if (Main.Instance.DefNameToArchipelagoId.ContainsKey(proj.defName))
            {
                Main.Instance.SendLocationCheck(proj.defName);
            }
            return true;
        }
    }
}
