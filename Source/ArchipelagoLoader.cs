using Archipelago.MultiClient.Net.Helpers;
using Newtonsoft.Json;
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

                Log.Message("Researches: " + JsonConvert.SerializeObject(Researches));
                Log.Message("Crafts: " + JsonConvert.SerializeObject(Crafts));
                Log.Message("Purchases: " + JsonConvert.SerializeObject(Purchases));
            }
            catch (Exception ex) { Log.Error(ex.Message + "\n" + ex.StackTrace); }
        }
    }
}
