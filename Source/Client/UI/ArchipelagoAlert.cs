using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimworldArchipelago.Client.UI
{
    public class ArchipelagoAlert : Alert
    {
        public ArchipelagoAlert(string label, string explanation)
        {
            defaultPriority = AlertPriority.Medium;
            defaultLabel = label;
            defaultExplanation = explanation;
            this.
        }

        public override AlertReport GetReport()
        {

            return AlertReport.CulpritsAre(new List<Thing>() { });
        }
    }
}
