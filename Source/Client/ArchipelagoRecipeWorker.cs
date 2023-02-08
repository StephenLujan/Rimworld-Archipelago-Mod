using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimworldArchipelago.Client
{
    public class ArchipelagoRecipeWorker : Verse.RecipeWorker
    {

        public override void Notify_IterationCompleted(Pawn billDoer, List<Thing> ingredients)
        {
            base.Notify_IterationCompleted(billDoer, ingredients);
            RimWorldArchipelagoMod.SendLocationCheck(recipe.defName);
        }
    }
}
