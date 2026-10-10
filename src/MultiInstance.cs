// Mode test multi-instance : plusieurs DEVOUR sur le même PC et le même compte Steam.
// Actif seulement si le jeu est lancé avec --devour-instance=N (1 = hôte, 2..8 = clients).
// Options : --devour-sound (garder le son des clients).

using System;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace DevourMorePlayers
{
    static class MultiInstance
    {
        public static int Instance;
        public static bool Enabled => Instance > 0;
        static bool _keepSound;

        public static void Init(HarmonyLib.Harmony harmony)
        {
            foreach (var a in Environment.GetCommandLineArgs())
            {
                if (a.StartsWith("--devour-instance=") && int.TryParse(a.Substring(18), out var n)) Instance = n;
                else if (a == "--devour-sound") _keepSound = true;
            }
            if (!Enabled) return;

            MelonLogger.Msg($"[Multi] Mode test actif : instance #{Instance}{(Instance == 1 ? " (hôte)" : "")}");

            if (Instance == 1)
            {
                // Photon refuse un 2e joueur avec le même UserId (même compte Steam) si CheckUserOnJoin est actif
                foreach (var m in AccessTools.GetDeclaredMethods(typeof(Il2CppPhoton.Realtime.LoadBalancingPeer)))
                    if (m.Name == "RoomOptionsToOpParameters") TryPatch(harmony, m, nameof(RoomOptionsPostfix));
            }
            else
            {
                TryPatch(harmony, AccessTools.Method(typeof(Il2CppSteamworks.SteamFriends), "GetPersonaName"), nameof(PersonaPostfix));
                // L'hôte refuse un playerId non numérique, et kicke un uniqueId (ID matériel du PC) déjà présent
                TryPatch(harmony, AccessTools.Method(typeof(Il2CppHorror.Menu), "GetPlayerId"), nameof(PlayerIdPostfix));
                TryPatch(harmony, AccessTools.Method(typeof(Il2CppHorror.ServerConnectToken), "Write"), nameof(ConnectTokenWritePrefix), prefix: true);
                TryPatch(harmony, AccessTools.PropertyGetter(typeof(SystemInfo), nameof(SystemInfo.deviceUniqueIdentifier)), nameof(DeviceIdPostfix));
            }
        }

        public static void Tick(int frame)
        {
            if (!Enabled || frame % 300 != 30) return;
            Application.runInBackground = true;
            if (Instance >= 2)
            {
                Application.targetFrameRate = 30;
                if (!_keepSound) AudioListener.volume = 0f;
            }
        }

        static void TryPatch(HarmonyLib.Harmony h, System.Reflection.MethodBase target, string patch, bool prefix = false)
        {
            if (target == null) { MelonLogger.Warning($"[Multi] cible introuvable pour {patch}"); return; }
            try
            {
                var hm = new HarmonyMethod(typeof(MultiInstance), patch);
                if (prefix) h.Patch(target, prefix: hm); else h.Patch(target, postfix: hm);
                MelonLogger.Msg($"[Multi] patch {target.DeclaringType?.Name}.{target.Name} OK");
            }
            catch (Exception e) { MelonLogger.Warning($"[Multi] patch {target.Name} échoué : {e.Message}"); }
        }

        // ---- Clients ----

        static void PersonaPostfix(ref string __result)
        {
            if (__result != null && !__result.EndsWith($" #{Instance}")) __result += $" #{Instance}";
        }

        static string _rawPlayerId, _shiftedPlayerId;

        // Décale le SteamID64 de (Instance - 1) : reste un ID numérique valide mais différent de l'hôte
        static string ShiftPlayerId(string raw)
        {
            if (raw == null || raw == _shiftedPlayerId) return raw;
            if (!ulong.TryParse(raw, out var v)) return raw;
            return (v + (ulong)(Instance - 1)).ToString();
        }

        static void PlayerIdPostfix(ref string __result)
        {
            var shifted = ShiftPlayerId(__result);
            if (_rawPlayerId == null)
            {
                _rawPlayerId = __result;
                _shiftedPlayerId = shifted;
                MelonLogger.Msg($"[Multi] playerId '{__result}' -> '{shifted}'");
            }
            __result = shifted;
        }

        // Le jeu peut avoir mis le playerId en cache avant le patch : on le force dans le token envoyé à l'hôte
        static void ConnectTokenWritePrefix(Il2CppHorror.ServerConnectToken __instance)
        {
            try
            {
                var pid = ShiftPlayerId(__instance.playerId);
                if (pid != __instance.playerId) __instance.playerId = pid;
            }
            catch (Exception e) { MelonLogger.Warning($"[Multi] ServerConnectToken.Write : {e.Message}"); }
        }

        static string _rawDeviceId;

        // Change les 2 derniers caractères hexadécimaux (XOR instance) : ID différent par instance, stable entre lancements
        static void DeviceIdPostfix(ref string __result)
        {
            if (string.IsNullOrEmpty(__result) || __result.Length < 2) return;
            if (!int.TryParse(__result.Substring(__result.Length - 2), System.Globalization.NumberStyles.HexNumber, null, out var last)) return;
            var shifted = __result.Substring(0, __result.Length - 2) + ((last ^ Instance) & 0xFF).ToString("x2");
            if (_rawDeviceId == null)
            {
                _rawDeviceId = __result;
                MelonLogger.Msg($"[Multi] deviceUniqueIdentifier '{__result}' -> '{shifted}'");
            }
            __result = shifted;
        }

        // ---- Hôte ----

        const byte CheckUserOnJoinCode = 232; // Photon ParameterCode.CheckUserOnJoin
        const byte RoomOptionFlagsCode = 191; // Photon ParameterCode.RoomOptionFlags (RoomOptionBit.CheckUserOnJoin = 1)

        static void RoomOptionsPostfix(object[] __args)
        {
            foreach (var a in __args)
            {
                if (!(a is Il2CppSystem.Collections.Generic.Dictionary<byte, Il2CppSystem.Object> dict)) continue;
                try
                {
                    if (dict.Remove(CheckUserOnJoinCode))
                        MelonLogger.Msg("[Multi] CheckUserOnJoin retiré de la room");
                    if (dict.ContainsKey(RoomOptionFlagsCode))
                        ClearCheckUserBitInPlace(dict[RoomOptionFlagsCode]);
                }
                catch (Exception e) { MelonLogger.Warning($"[Multi] CheckUserOnJoin : {e.Message}"); }
                return;
            }
        }

        // Modifie la valeur dans la boîte existante (en-tête Il2CppObject de 16 octets en x64).
        // Réécrire l'entrée du dictionnaire avec une nouvelle boîte fait crasher le jeu.
        static unsafe void ClearCheckUserBitInPlace(Il2CppSystem.Object boxed)
        {
            if (boxed == null || boxed.Pointer == IntPtr.Zero) return;
            string type = boxed.GetIl2CppType().Name;
            byte* data = (byte*)boxed.Pointer + 16;
            if (type == "Int32")
            {
                int v = *(int*)data;
                if (v != boxed.Unbox<int>()) { MelonLogger.Warning("[Multi] RoomOptionFlags : valeur inattendue, rien modifié"); return; }
                *(int*)data = v & ~1;
                MelonLogger.Msg($"[Multi] RoomOptionFlags {v} -> {v & ~1}");
            }
            else if (type == "Byte")
            {
                byte v = *data;
                if (v != boxed.Unbox<byte>()) { MelonLogger.Warning("[Multi] RoomOptionFlags : valeur inattendue, rien modifié"); return; }
                *data = (byte)(v & ~1);
                MelonLogger.Msg($"[Multi] RoomOptionFlags {v} -> {v & ~1}");
            }
            else MelonLogger.Warning($"[Multi] RoomOptionFlags de type {type}, rien modifié");
        }
    }
}
