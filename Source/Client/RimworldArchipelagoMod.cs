using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using HarmonyLib;
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
    public class RimWorldArchipelagoMod : Mod
    {

        public static Harmony Harmony;
        public static ArchipelagoSession Session;

        public static string Address { get; private set; } = "127.0.0.1:38281";
        public static string PlayerSlot { get; private set; } = "";

        public static ArchipelagoLoader ArchipelagoLoader { get; private set; }

        public RimWorldArchipelagoMod(ModContentPack content) : base(content)
        {

        }

        public static bool Connect(string address, string playerSlot, string password = null)
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
                Log.Message("TODO");
                newSession = ArchipelagoSessionFactory.CreateSession(new Uri($"ws://{address}"));
            }
            else
                newSession = ArchipelagoSessionFactory.CreateSession(address);


            Log.Message("Session created.");

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
            Log.Message($"{loginSuccess}");
            Log.Message(JsonConvert.SerializeObject(loginSuccess));

            ArchipelagoLoader = new ArchipelagoLoader();
            _ = ArchipelagoLoader.Load();

            return true;
        }

        public static readonly IDictionary<string, long> DefNameToArchipelagoId = new ConcurrentDictionary<string, long>();

        public static readonly IDictionary<long, Tuple<string, string>> ArchipeligoIdToDef = new ConcurrentDictionary<long, Tuple<string, string>>();

        public static void SendLocationCheck(string defName)
        {
            Log.Message($"Sending completed location {defName} to Archipelago");
            Session.Locations.CompleteLocationChecks(DefNameToArchipelagoId[defName]);
        }
    }
}
