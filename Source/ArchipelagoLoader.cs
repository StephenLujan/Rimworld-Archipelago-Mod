using Archipelago.MultiClient.Net.Helpers;
using Newtonsoft.Json;
using RimWorld;
using RimworldArchipelago.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using static RimworldArchipelago.ArchipelagoLoader;

namespace RimworldArchipelago
{
    public class ArchipelagoLoader
    {
        public class Location
        {
            public string Name;
            public long ItemId;
            public string ItemName;
            public int Player;
            public string ExtendedItemName;
        }
        public IDictionary<int, PlayerInfo> Players { get; private set; }

        public readonly IDictionary<long, Location> Researches = new ConcurrentDictionary<long, Location>();
        public readonly IDictionary<long, Location> Crafts = new ConcurrentDictionary<long, Location>();
        public readonly IDictionary<long, Location> Purchases = new ConcurrentDictionary<long, Location>();

        public ArchipelagoLoader()
        {

        }

        public async Task Load()
        {
            Log.Message("ArchipelagoLoader started...");
            try
            {
                Debug.Assert(RimworldArchipelagoMod.Session != null);
                await LoadLocationDictionary();
                LoadResearchDefs();
            }
            catch (Exception ex) { Log.Error(ex.Message + "\n" + ex.StackTrace); }
        }

        public async Task LoadLocationDictionary()
        {
            var sess = RimworldArchipelagoMod.Session;
            Players = sess.Players.AllPlayers.ToDictionary(x => x.Slot);
            var allLocations = sess.Locations.AllLocations.ToArray();
            var items = (await sess.Locations.ScoutLocationsAsync(false, allLocations)).Locations;
            //Log.Message("allLocations: "+JsonConvert.SerializeObject(allLocations));
            //Log.Message("items: " + JsonConvert.SerializeObject(items));

            Parallel.ForEach(
                items,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) * 2.0))
                },
                item =>
                {
                    try
                    {
                        var locationId = item.Location;
                        var itemName = sess.Items.GetItemName(item.Item);
                        var locationName = sess.Locations.GetLocationNameFromId(locationId);
                        var location = new Location()
                        {
                            ItemId = item.Item,
                            ItemName = itemName,
                            Name = locationName,
                            Player = item.Player,
                            ExtendedItemName = $"{Players[item.Player].Name}'s {itemName}"
                        };
                        if (locationId >= 11_000 && locationId < 12_000)
                        {
                            Researches[locationId] = location;
                        }
                        else if (locationId >= 12_000 && locationId < 13_000)
                        {
                            Crafts[locationId] = location;
                        }
                        else if (locationId >= 13_000 && locationId < 14_000)
                        {
                            Purchases[locationId] = location;
                        }
                        else
                        {
                            Log.Error($"Unknown location id: {locationId}");
                        }
                    }
                    catch (Exception ex) { Log.Error(ex.Message + "\n" + ex.StackTrace); }
                });

            Log.Message(" Researches: " + JsonConvert.SerializeObject(Researches));
            Log.Message(" Crafts: " + JsonConvert.SerializeObject(Crafts));
            Log.Message(" Purchases: " + JsonConvert.SerializeObject(Purchases));
        }
        public void LoadResearchDefs()
        {
            JsonSerializerSettings sets = new JsonSerializerSettings
            {
                PreserveReferencesHandling = PreserveReferencesHandling.Objects
            };
            var tab = DefDatabase<ResearchTabDef>.GetNamed("AD_Archipelago");
            //Log.Message($"research tab: {JsonConvert.SerializeObject(tab, sets)}");
            //using rdb = DefDatabase<ResearchProjectDef>;
            Log.Message($"research tab.generated: {JsonConvert.SerializeObject(tab.generated)}");
            var researchesBefore = DefDatabase<ResearchProjectDef>.DefCount;
            Log.Message($"number of researches before: {researchesBefore}");
            //Log.Message($"{ JsonConvert.SerializeObject(DefDatabase<ResearchProjectDef>.AllDefsListForReading, sets)}");
            int iter = -1;
            int rows = 5;
            var newResearches = Researches.Select(kvp =>
            {
                iter++;
                return new ResearchProjectDef()
                {
                    baseCost = 10,
                    defName = $"AP_{kvp.Key}",
                    description = kvp.Value.ExtendedItemName + $" (AP_{kvp.Key})",
                    label = kvp.Value.ExtendedItemName,
                    tab = tab,
                    researchViewX = iter / rows,
                    researchViewY = iter % rows,
                };
            });
            DefDatabase<ResearchProjectDef>.Add(newResearches);
            var researchesAfter = DefDatabase<ResearchProjectDef>.DefCount;
            Log.Message($"number of researches after: {researchesAfter}");
            ResearchProjectDef.GenerateNonOverlappingCoordinates();

            //Log.Message($"{ JsonConvert.SerializeObject(DefDatabase<ResearchProjectDef>.AllDefsListForReading, sets)}");
        }
    }
}
