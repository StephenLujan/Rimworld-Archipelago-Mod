using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using HarmonyLib;
using HugsLib;
using HugsLib.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.PlayerLoop;
using Verse;

namespace RimworldArchipelago.Client
{

    public class Main : HugsLib.ModBase
    {
        public override string ModIdentifier => "Archipelago";
        public Main()
        {
            Instance = this;
        }
        internal static Main Instance { get; private set; }

        public ModLogger Log => base.Logger;

        //public Harmony Harmony;
        public ArchipelagoSession Session;

        public string Address { get; private set; } = "127.0.0.1:38281";
        public string PlayerSlot { get; private set; } = "Player";

        public ArchipelagoLoader ArchipelagoLoader { get; private set; }
        public struct RimWorldDef { public string DefName; public string DefType; public int Quantity; }

        public readonly IDictionary<string, long> DefNameToArchipelagoId = new ConcurrentDictionary<string, long>();
        public readonly IDictionary<long, RimWorldDef> ArchipeligoItemIdToRimWorldDef = new ConcurrentDictionary<long, RimWorldDef>();

        public static bool IsResearchLocation(long id) => id >= 11_000 && id < 12_000;
        public static bool IsCraftLocation(long id) => id >= 12_000 && id < 13_000;
        public static bool IsPurchaseLocation(long id) => id >= 13_000 && id < 14_000;

        public bool Connect(string address, string playerSlot, string password = null)
        {
            Log.Message("Connecting to Archipelago...");
            // store address & player slot even if invalid if there is no session yet
            if (Session == null)
            {
                Address = address;
                PlayerSlot = playerSlot;
            }
            ArchipelagoSession newSession;
            if (address.Contains(':'))
            {
                newSession = ArchipelagoSessionFactory.CreateSession(new Uri($"ws://{address}"));
            }
            else
                newSession = ArchipelagoSessionFactory.CreateSession(address);

            LoginResult result;
            try
            {
                result = newSession.TryConnectAndLogin("RimWorld", playerSlot, ItemsHandlingFlags.AllItems, password: password);

            }
            catch (Exception e)
            {
                result = new LoginFailure(e.GetBaseException().Message);
            }

            if (!result.Successful)
            {
                LoginFailure failure = (LoginFailure)result;
                string errorMessage = $"Failed to Connect to {address} as {playerSlot}:";
                foreach (string error in failure.Errors)
                {
                    errorMessage += $"\n    {error}";
                }
                foreach (ConnectionRefusedError error in failure.ErrorCodes)
                {
                    errorMessage += $"\n    {error}";
                }
                Log.Error(errorMessage);
                return false;
            }
            Address = address;
            PlayerSlot = playerSlot;
            Session = newSession;
            Log.Message("Successfully Connected.");
            var loginSuccess = (LoginSuccessful)result;
            Log.Trace($"{loginSuccess}");
            Log.Trace(JsonConvert.SerializeObject(loginSuccess));

            ArchipelagoLoader = new ArchipelagoLoader();
            _ = ArchipelagoLoader.Load();

            return true;
        }

        public void SendLocationCheck(string defName)
        {
            Log.Message($"Sending completed location {defName} to Archipelago");
            Session.Locations.CompleteLocationChecks(DefNameToArchipelagoId[defName]);
            DisableLocation(defName);
        }

        public void DisableLocation(string defName)
        {
            var id = DefNameToArchipelagoId[defName];
            if (IsResearchLocation(id))
            {
                var def = DefDatabase<ResearchProjectDef>.GetNamed(defName);
                //TODO? probably only here because research finished anyway
            }
            else if (IsCraftLocation(id))
            {
                var def = DefDatabase<RecipeDef>.GetNamed(defName);
                def.recipeUsers.RemoveAt(0); //TODO test that this removes recipe
            }
        }

    }
}
