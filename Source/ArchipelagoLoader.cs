using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Helpers;
using Newtonsoft.Json;
using RimWorld;
using RimworldArchipelago.Client;
using Steamworks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
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

        public class LocationResearchMetaData
        {
            public float x;
            public float y;
            public float cost;
            public long[] prerequisites;
        }

        public IDictionary<int, PlayerInfo> Players { get; private set; }

        public readonly IDictionary<long, Location> Researches = new ConcurrentDictionary<long, Location>();
        public readonly IDictionary<long, Location> Crafts = new ConcurrentDictionary<long, Location>();
        public readonly IDictionary<long, Location> Purchases = new ConcurrentDictionary<long, Location>();
        public IDictionary<string, object> SlotData { get; private set; }
        public int CurrentPlayerId;

        public ArchipelagoSession Session => RimWorldArchipelagoMod.Session;

        public ArchipelagoLoader()
        {

        }


        public async Task Load()
        {
            Log.Message("ArchipelagoLoader started...");
            try
            {
                Debug.Assert(Session != null);
                Players = Session.Players.AllPlayers.ToDictionary(x => x.Slot);
                CurrentPlayerId = Players.First(kvp => kvp.Value.Name == RimWorldArchipelagoMod.PlayerSlot).Key;
                SlotData = await Session.DataStorage.GetSlotDataAsync(CurrentPlayerId);


                LoadRimworldDefMaps();
                await LoadLocationDictionary();
                LoadResearchDefs();
                AddSessionHooks();
            }
            catch (Exception ex) { Log.Error(ex.Message + "\n" + ex.StackTrace); }
        }
        private void LoadRimworldDefMaps()
        {
            var defNameMap = JsonConvert.DeserializeObject<Dictionary<long, string[]>>(SlotData["defNameMap"].ToString());
            foreach (var kvp in defNameMap)
            {
                RimWorldArchipelagoMod.ArchipeligoIdToDef[kvp.Key] = Tuple.Create(kvp.Value[0], kvp.Value[1]);
            }
        }
        private async Task LoadLocationDictionary()
        {
            var hints = await Session.DataStorage.GetHintsAsync();

            var allLocations = Session.Locations.AllLocations.ToArray();
            var items = (await Session.Locations.ScoutLocationsAsync(false, allLocations)).Locations;


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
                        var itemName = Session.Items.GetItemName(item.Item);
                        var locationName = Session.Locations.GetLocationNameFromId(locationId);
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

        /// <summary>
        /// seems clunky, but here we combine the research segment of Locations with the research-only metadata,
        /// output them as RimWorld ResearchProjectDefs, and add them to the Archipelago research tab
        /// </summary>
        private void LoadResearchDefs()
        {
            JsonSerializerSettings sets = new JsonSerializerSettings
            {
                PreserveReferencesHandling = PreserveReferencesHandling.Objects
            };
            var tab = DefDatabase<ResearchTabDef>.GetNamed("AD_Archipelago");
            var researchesBefore = DefDatabase<ResearchProjectDef>.DefCount;
            Log.Message($"number of researches before: {researchesBefore}");


            var techTree = JsonConvert.DeserializeObject<Dictionary<long, LocationResearchMetaData>>(SlotData["techTree"].ToString());
            var newResearchDefs = new Dictionary<long, ResearchProjectDef>();
            foreach (var kvp in techTree)
            {
                var locationData = Researches[kvp.Key];
                var def = new ResearchProjectDef()
                {
                    baseCost = kvp.Value.cost,
                    defName = $"AP_{kvp.Key}",
                    description = locationData.ExtendedItemName + $" (AP_{kvp.Key})",
                    label = locationData.ExtendedItemName,
                    tab = tab,
                    researchViewX = kvp.Value.x,
                    researchViewY = kvp.Value.y,

                };
                newResearchDefs.Add(kvp.Key, def);
                RimWorldArchipelagoMod.DefNameToArchipelagoId[def.defName] = kvp.Key;
            }
            foreach (var kvp in newResearchDefs)
            {
                kvp.Value.prerequisites = techTree[kvp.Key].prerequisites.Select(x => newResearchDefs[x]).ToList();
            }

            DefDatabase<ResearchProjectDef>.Add(newResearchDefs.Values);
            var researchesAfter = DefDatabase<ResearchProjectDef>.DefCount;
            Log.Message($"number of researches after: {researchesAfter}");
            ResearchProjectDef.GenerateNonOverlappingCoordinates();
        }

        private void AddSessionHooks()
        {
            // Must go AFTER a successful connection attempt
            Session.Items.ItemReceived += (receivedItemsHelper) =>
            {
                var itemReceivedName = receivedItemsHelper.PeekItemName();
                Log.Message($"Received Item: {itemReceivedName}");

                var networkItem = receivedItemsHelper.DequeueItem();
                if (RimWorldArchipelagoMod.ArchipeligoIdToDef.ContainsKey(networkItem.Item))
                {
                    var defMapping = RimWorldArchipelagoMod.ArchipeligoIdToDef[networkItem.Item];
                    var defName = defMapping.Item1;
                    var defType = defMapping.Item2;
                    // TODO something other than ResearchTabDef
                    var def = DefDatabase<ResearchProjectDef>.GetNamed(defName, true);
                    Find.ResearchManager.FinishProject(def);
                }
                else
                {
                    Log.Error($"Could not find RimWorld DefName associated with Archipelago item id {networkItem.Item}");
                }
            };
        }



    }
}

