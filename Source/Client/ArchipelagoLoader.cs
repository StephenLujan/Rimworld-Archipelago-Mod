using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Helpers;
using Newtonsoft.Json;
using RimWorld;
using RimworldArchipelago.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verse;
using Verse.Noise;

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
            public string ExtendedLabel;
        }

        public class LocationResearchMetaData
        {
            public float x;
            public float y;
            public float cost;
            public long[] prerequisites;
        }

        public IDictionary<int, PlayerInfo> Players { get; private set; }

        public readonly IDictionary<long, Location> ResearchLocations = new ConcurrentDictionary<long, Location>();
        public readonly IDictionary<long, Location> CraftLocations = new ConcurrentDictionary<long, Location>();
        public readonly IDictionary<long, Location> PurchasLocations = new ConcurrentDictionary<long, Location>();
        public int CurrentPlayerId;
        private IDictionary<string, object> SlotData { get; set; }

        public readonly IDictionary<long, ResearchProjectDef> AddedResearchDefs = new Dictionary<long, ResearchProjectDef>();
        public readonly IDictionary<long, RecipeDef> AddedRecipeDefs = new Dictionary<long, RecipeDef>();


        private ArchipelagoSession Session => RimWorldArchipelagoMod.Session;

        private bool isArchipelagoLoaded = false;

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
                LoadCraftDefs();
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
            // TODO this doesn't work anyway
            DefDatabase<ResearchProjectDef>.Clear();
            DefDatabase<ResearchProjectDef>.ClearCachedData();

            // Empty out our data mappings and stuff
            ResearchLocations.Clear();
            CraftLocations.Clear();
            PurchasLocations.Clear();
            Players.Clear();
            SlotData.Clear();
            //RimWorldArchipelagoMod.ReceivedItems.Clear();
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
                            ExtendedLabel = $"{Players[item.Player].Name}'s {itemName}"
                        };
                        if (RimWorldArchipelagoMod.IsResearchLocation(locationId))
                        {
                            ResearchLocations[locationId] = location;
                        }
                        else if (RimWorldArchipelagoMod.IsCraftLocation(locationId))
                        {
                            CraftLocations[locationId] = location;
                        }
                        else if (RimWorldArchipelagoMod.IsPurchaseLocation(locationId))
                        {
                            PurchasLocations[locationId] = location;
                        }
                        else
                        {
                            Log.Error($"Unknown location id: {locationId}");
                        }
                    }
                    catch (Exception ex) { Log.Error(ex.Message + "\n" + ex.StackTrace); }
                });

            Log.Message(" Researches: " + JsonConvert.SerializeObject(ResearchLocations));
            Log.Message(" Crafts: " + JsonConvert.SerializeObject(CraftLocations));
            Log.Message(" Purchases: " + JsonConvert.SerializeObject(PurchasLocations));
        }

        /// <summary>
        /// seems clunky, but here we combine the research segment of Locations with the research-only metadata,
        /// output them as RimWorld ResearchProjectDefs, and add them to the Archipelago research tab
        /// </summary>
        private void LoadResearchDefs()
        {
            // get archipelago research tab
            var tab = DefDatabase<ResearchTabDef>.GetNamed("AD_Archipelago");
            var researchesBefore = DefDatabase<ResearchProjectDef>.DefCount;
            Log.Message($"number of researches before: {researchesBefore}");
            var techTree = JsonConvert.DeserializeObject<Dictionary<long, LocationResearchMetaData>>(SlotData["techTree"].ToString());
            
            foreach (var kvp in techTree)
            {
                var locationData = ResearchLocations[kvp.Key];
                var def = new ResearchProjectDef()
                {
                    baseCost = kvp.Value.cost,
                    defName = $"AP_{kvp.Key}",
                    description = locationData.ExtendedLabel + $" (AP_{kvp.Key})",
                    label = locationData.ExtendedLabel,
                    tab = tab,
                    researchViewX = kvp.Value.x,
                    researchViewY = kvp.Value.y
                };
                AddedResearchDefs.Add(kvp.Key, def);
                RimWorldArchipelagoMod.DefNameToArchipelagoId[def.defName] = kvp.Key;
            }
            foreach (var kvp in AddedResearchDefs)
            {
                kvp.Value.prerequisites = techTree[kvp.Key].prerequisites.Select(x => AddedResearchDefs[x]).ToList();
            }

            DefDatabase<ResearchProjectDef>.Add(AddedResearchDefs.Values);
            var researchesAfter = DefDatabase<ResearchProjectDef>.DefCount;
            Log.Message($"number of researches after: {researchesAfter}");
            ResearchProjectDef.GenerateNonOverlappingCoordinates();
        }

        private void LoadCraftDefs()
        {
            //get new archipelago crafting table and other constant recipe properties
            var recipeUsers = new List<ThingDef>(){ DefDatabase<ThingDef>.GetNamed("AD_ArchipelagoBench") };
            var workSkill = DefDatabase<SkillDef>.GetNamed("Crafting");
            var effectWorking = DefDatabase<EffecterDef>.GetNamed("Cook");
            var soundWorking = DefDatabase<SoundDef>.GetNamed("Recipe_Machining");
            var workSpeedStat = DefDatabase<StatDef>.GetNamed("GeneralLaborSpeed");

            /*
             * Log.Message(string.Join(", " , DefDatabase<ThingCategoryDef>.AllDefsListForReading.Select(x => x.defName).ToArray()));
             * Root, Foods, FoodMeals, FoodRaw, MeatRaw, PlantFoodRaw, AnimalProductRaw, EggsUnfertilized, EggsFertilized, Manufactured,
             * Textiles, Leathers, Wools, Medicine, Drugs, MortarShells, ResourcesRaw, PlantMatter, StoneBlocks, Items, Unfinished, Artifacts,
             * InertRelics, Neurotrainers, NeurotrainersPsycast, NeurotrainersSkill, Techprints, BodyParts, BodyPartsNatural, BodyPartsSimple,
             * BodyPartsProsthetic, BodyPartsBionic, BodyPartsUltra, BodyPartsArchotech, BodyPartsMechtech, ItemsMisc, Weapons, WeaponsMelee,
             * WeaponsMeleeBladelink, WeaponsRanged, Grenades, Apparel, Headgear, ApparelArmor, ArmorHeadgear, ApparelUtility, ApparelNoble,
             * HeadgearNoble, ApparelMisc, Buildings, BuildingsArt, BuildingsProduction, BuildingsFurniture, BuildingsPower, BuildingsSecurity,
             * BuildingsMisc, BuildingsJoy, BuildingsTemperature, BuildingsSpecial, Chunks, StoneChunks, Animals, Plants, Stumps, Corpses,
             * CorpsesHumanlike, CorpsesAnimal, CorpsesInsect, CorpsesMechanoid
             */

            Func<string, ThingFilter> makeFilter = (string s) => new ThingFilter()
            {
                DisplayRootCategory = DefDatabase<ThingCategoryDef>.GetNamed(s).treeNode
            };

            var leathersFilter = makeFilter("Leathers");
            var textilesFilter = makeFilter("Textiles");
            var woolsFilter = makeFilter("Textiles");
            var stoneBlocksFilter = makeFilter("StoneBlocks");
            var foodRawFilter = makeFilter("FoodRaw");
            var filters = new List<ThingFilter>() { leathersFilter, textilesFilter, woolsFilter, stoneBlocksFilter, foodRawFilter };

            Func<ThingFilter, float, IngredientCount> makeIngredientCount = (ThingFilter filter, float count) =>
            {
                var output = new IngredientCount()
                {
                    filter = filter,
                };
                output.SetBaseCount(count);
                return output;
            };

            foreach (var kvp in CraftLocations)
            {

                var filter = Verse.Rand.Element(leathersFilter, textilesFilter, woolsFilter, stoneBlocksFilter, foodRawFilter);
                var def = new RecipeDef()
                {
                    defName = $"AP_{kvp.Key}",
                    label = kvp.Value.ExtendedLabel,
                    description = kvp.Value.ExtendedLabel + $" (AP_{kvp.Key})",
                    recipeUsers = recipeUsers,
                    workerClass = typeof(ArchipelagoRecipeWorker),
                    workSkill = workSkill,
                    workAmount = 1000, //TODO configurable
                    workSpeedStat = workSpeedStat,
                    allowMixingIngredients = true,
                    effectWorking = effectWorking,
                    soundWorking = soundWorking,
                    ingredients = new List<IngredientCount>()
                    {
                        makeIngredientCount(filter, 2)
                    },
                    fixedIngredientFilter = filter,
                    defaultIngredientFilter = filter,
                    //targetCountAdjustment=50
                };
                AddedRecipeDefs.Add(kvp.Key, def);
                RimWorldArchipelagoMod.DefNameToArchipelagoId[def.defName] = kvp.Key;
            }
            DefDatabase<RecipeDef>.Add(AddedRecipeDefs.Values);
        }


        private void AddSessionHooks()
        {
            // Must go AFTER a successful connection attempt
            Session.Items.ItemReceived += (receivedItemsHelper) =>
            {
                var itemReceivedName = receivedItemsHelper.PeekItemName();
                Log.Message($"Received Item: {itemReceivedName}");
                var networkItem = receivedItemsHelper.DequeueItem();
                ArchipelagoWorldComp.ReceiveItem(networkItem.Item);
            };

            Session.MessageLog.OnMessageReceived += (message) =>
            {
                foreach (var part in message.Parts)
                {
                    //Find.LetterStack.ReceiveLetter(part.Text, part.Text, LetterDefOf.NeutralEvent);
                    Messages.Message(part.Text, MessageTypeDefOf.SilentInput, false);
                }
            };
        }


    }
}

