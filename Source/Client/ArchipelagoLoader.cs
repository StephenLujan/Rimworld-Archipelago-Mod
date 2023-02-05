using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
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
using UnityEngine;
using Verse;
using static RimworldArchipelago.ArchipelagoLoader;

namespace RimworldArchipelago
{
    /// <summary>
    /// Loads the initial data for archipelago locations and items
    /// </summary>
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
        public int CurrentPlayerId;
        private IDictionary<string, object> SlotData { get; set; }


        private ArchipelagoSession Session => RimWorldArchipelagoMod.Session;

        private bool isArchipelagoLoaded = false;
        private IEnumerable<ResearchProjectDef> originalResearchProjectDefs;

        public ArchipelagoLoader()
        {

        }


        public async Task Load()
        {
            Log.Message("ArchipelagoLoader started...");
            try
            {
                System.Diagnostics.Debug.Assert(Session != null);
                if (isArchipelagoLoaded)
                    Unload();

                Players = Session.Players.AllPlayers.ToDictionary(x => x.Slot);
                CurrentPlayerId = Players.First(kvp => kvp.Value.Name == RimWorldArchipelagoMod.PlayerSlot).Key;
                SlotData = await Session.DataStorage.GetSlotDataAsync(CurrentPlayerId);
                LoadRimworldDefMaps();
                await LoadLocationDictionary();
                LoadResearchDefs();
                AddSessionHooks();

                isArchipelagoLoaded = true;
            }
            catch (Exception ex) { Log.Error(ex.Message + "\n" + ex.StackTrace); }
        }

        /// <summary>
        /// Only really needed if we want to connect to another archipelago session with different settings without just reloading the whole game
        /// </summary>
        public void Unload()
        {
            // unload stuff added to rimworld
            // is there no better way than this? :-/
            DefDatabase<ResearchProjectDef>.ClearCachedData();
            DefDatabase<ResearchProjectDef>.Clear();
            DefDatabase<ResearchProjectDef>.Add(originalResearchProjectDefs);

            // Empty out our data mappings and stuff
            Researches.Clear();
            Crafts.Clear();
            Purchases.Clear();
            Players.Clear();
            SlotData.Clear();
            RimWorldArchipelagoMod.ReceivedItems.Clear();
            isArchipelagoLoaded = false;
        }

        /// <summary>
        /// Build the mappings from numerical archipelago ids to rimworld string def names. 
        /// They have to be filled in from info received from Archipelago in SlotData.
        /// </summary>
        private void LoadRimworldDefMaps()
        {
            var defNameMap = JsonConvert.DeserializeObject<Dictionary<long, string[]>>(SlotData["defNameMap"].ToString());
            foreach (var kvp in defNameMap)
            {
                RimWorldArchipelagoMod.ArchipeligoItemIdToRimWorldDef[kvp.Key] = new RimWorldArchipelagoMod.RimWorldDef()
                {
                    DefName = kvp.Value[0],
                    DefType = kvp.Value[1]
                };
            }
        }

        /// <summary>
        /// Fill the locations dictionary, which maps the archipelago's numeric location ids to data we can use internally
        /// </summary>
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
            originalResearchProjectDefs = DefDatabase<ResearchProjectDef>.AllDefs.ToList();
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
                RimWorldArchipelagoMod.ReceiveItem(networkItem.Item);
            };

            Session.MessageLog.OnMessageReceived += (message) =>
            {
                foreach (var part in message.Parts)
                {
                    //TODO alert?
                }
            };
        }


    }
}

