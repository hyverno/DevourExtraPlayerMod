// Patchs qui remplacent la limite de 4 joueurs par Core.Max (Steam, Photon, Bolt, lobby du jeu)

using System;
using HarmonyLib;
using MelonLoader;

namespace DevourMorePlayers
{
    [HarmonyPatch(typeof(Il2CppSteamworks.SteamMatchmaking), nameof(Il2CppSteamworks.SteamMatchmaking.CreateLobby))]
    static class Patch_SteamCreateLobby
    {
        static void Prefix(ref int cMaxMembers)
        {
            MelonLogger.Msg($"[Steam] CreateLobby maxMembers {cMaxMembers} -> {Core.Max}");
            cMaxMembers = Core.Max;
        }
    }

    [HarmonyPatch(typeof(Il2CppPhoton.Bolt.BoltLauncher), nameof(Il2CppPhoton.Bolt.BoltLauncher.StartServer),
        new Type[] { typeof(Il2CppPhoton.Bolt.BoltConfig), typeof(string) })]
    static class Patch_BoltStartServer_Config
    {
        static void Prefix(Il2CppPhoton.Bolt.BoltConfig config)
        {
            if (config == null) return;
            MelonLogger.Msg($"[Bolt] StartServer connLimit {config.serverConnectionLimit} -> {Core.Max}");
            config.serverConnectionLimit = Core.Max;
        }
    }

    [HarmonyPatch(typeof(Il2CppPhoton.Bolt.BoltLauncher), nameof(Il2CppPhoton.Bolt.BoltLauncher.StartServer),
        new Type[] { typeof(Il2CppUdpKit.UdpEndPoint), typeof(Il2CppPhoton.Bolt.BoltConfig), typeof(string) })]
    static class Patch_BoltStartServer_EndpointConfig
    {
        static void Prefix(Il2CppPhoton.Bolt.BoltConfig config)
        {
            if (config == null) return;
            MelonLogger.Msg($"[Bolt] StartServer(ep) connLimit {config.serverConnectionLimit} -> {Core.Max}");
            config.serverConnectionLimit = Core.Max;
        }
    }

    [HarmonyPatch(typeof(Il2CppPhoton.Bolt.BoltRuntimeSettings), nameof(Il2CppPhoton.Bolt.BoltRuntimeSettings.GetConfigCopy))]
    static class Patch_BoltGetConfigCopy
    {
        static void Postfix(Il2CppPhoton.Bolt.BoltConfig __result)
        {
            if (__result == null) return;
            MelonLogger.Msg($"[Bolt] GetConfigCopy connLimit {__result.serverConnectionLimit} -> {Core.Max}");
            __result.serverConnectionLimit = Core.Max;
        }
    }

    [HarmonyPatch(typeof(Il2CppPhoton.Realtime.LoadBalancingClient), nameof(Il2CppPhoton.Realtime.LoadBalancingClient.OpCreateRoom))]
    static class Patch_PhotonOpCreateRoom
    {
        static void Prefix(Il2CppPhoton.Realtime.EnterRoomParams enterRoomParams)
        {
            try
            {
                if (enterRoomParams?.RoomOptions == null) return;
                MelonLogger.Msg($"[Photon] OpCreateRoom MaxPlayers {enterRoomParams.RoomOptions.MaxPlayers} -> {Core.Max}");
                enterRoomParams.RoomOptions.MaxPlayers = Core.Max;
            }
            catch (Exception e) { MelonLogger.Warning($"[Photon] OpCreateRoom patch: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(Il2CppUdpKit.Platform.Photon.PhotonPoller), nameof(Il2CppUdpKit.Platform.Photon.PhotonPoller.SetHostInfo))]
    static class Patch_SetHostInfo
    {
        static bool _logged;

        static void Prefix(Il2CppUdpKit.UdpSession session)
        {
            try
            {
                var impl = session?.TryCast<Il2CppUdpKit.UdpSessionImpl>();
                if (impl == null) return;
                if (!_logged || impl.ConnectionsMax != Core.Max)
                {
                    MelonLogger.Msg($"[Session] SetHostInfo ConnectionsMax {impl.ConnectionsMax} -> {Core.Max} (current: {impl.ConnectionsCurrent})");
                    _logged = true;
                }
                impl.ConnectionsMax = Core.Max;
            }
            catch (Exception e) { MelonLogger.Warning($"[Session] SetHostInfo patch: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(Il2Cpp.SurvivalLobbyController), nameof(Il2Cpp.SurvivalLobbyController.LobbyIsFull))]
    static class Patch_LobbyIsFull
    {
        static void Postfix(Il2Cpp.SurvivalLobbyController __instance, ref bool __result)
        {
            if (!__result) return;
            try { __result = __instance.PlayerPrefabsAttached() >= Core.Max; }
            catch { __result = false; }
        }
    }
}
