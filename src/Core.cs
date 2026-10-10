// DevourMorePlayers — lève la limite de 4 joueurs dans DEVOUR (côté hôte).
// Config : UserData/MelonPreferences.cfg -> [DevourMorePlayers] MaxPlayers = 8

using System;
using MelonLoader;

[assembly: MelonInfo(typeof(DevourMorePlayers.Core), "DevourMorePlayers", "0.7.1", "Hyverno")]
[assembly: MelonGame("Straight Back Games", "DEVOUR")]

namespace DevourMorePlayers
{
    public class Core : MelonMod
    {
        public static MelonPreferences_Entry<int> MaxPlayersEntry;
        public static int Max => Math.Max(2, Math.Min(250, MaxPlayersEntry.Value));

        private int _frame;
        private Il2CppHorror.Menu _menu;
        private bool _liveLimitTried;

        public override void OnInitializeMelon()
        {
            var cat = MelonPreferences.CreateCategory("DevourMorePlayers");
            MaxPlayersEntry = cat.CreateEntry("MaxPlayers", 8, description: "Nombre max de joueurs dans le lobby (vanilla = 4)");
            LoggerInstance.Msg($"DevourMorePlayers actif — limite lobby : {Max}");
            AIPlayerArrays.Init(HarmonyInstance);
            MultiInstance.Init(HarmonyInstance);
        }

        public override void OnUpdate()
        {
            if (++_frame % 30 != 0) return;
            MultiInstance.Tick(_frame);
            try { FixLiveConnectionLimit(); } catch { }
            try { KeepInviteButtonEnabled(); } catch { _menu = null; }
        }

        // Le jeu grise le bouton d'invitation à partir de 4 joueurs
        private void KeepInviteButtonEnabled()
        {
            if (_menu == null)
                _menu = UnityEngine.Object.FindObjectOfType<Il2CppHorror.Menu>();
            if (_menu == null) return;

            var btn = _menu.lobbyInviteButton;
            if (btn != null && !btn.interactable && Il2CppPhoton.Bolt.BoltNetwork.IsServer)
                btn.interactable = true;
        }

        // La config Bolt réellement utilisée par le serveur peut rester à 4 malgré les patchs de démarrage
        private void FixLiveConnectionLimit()
        {
            if (!Il2CppPhoton.Bolt.BoltNetwork.IsServer) { _liveLimitTried = false; return; }
            if (_liveLimitTried || Il2CppPhoton.Bolt.BoltNetwork.MaxConnections >= Max) return;
            _liveLimitTried = true;

            int before = Il2CppPhoton.Bolt.BoltNetwork.MaxConnections;
            Type[] types;
            try { types = typeof(Il2CppPhoton.Bolt.BoltNetwork).Assembly.GetTypes(); } catch { return; }
            var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            foreach (var t in types)
            {
                System.Reflection.PropertyInfo[] props;
                try { props = t.GetProperties(flags); } catch { continue; }
                foreach (var pr in props)
                {
                    if (pr.PropertyType != typeof(Il2CppPhoton.Bolt.BoltConfig) || pr.GetIndexParameters().Length != 0) continue;
                    try
                    {
                        var cfg = pr.GetValue(null) as Il2CppPhoton.Bolt.BoltConfig;
                        if (cfg == null || cfg.serverConnectionLimit >= Max) continue;
                        cfg.serverConnectionLimit = Max;
                        MelonLogger.Msg($"[Bolt] Config live {t.Name}.{pr.Name} : serverConnectionLimit -> {Max}");
                    }
                    catch { }
                }
            }
            MelonLogger.Msg($"[Bolt] MaxConnections live {before} -> {Il2CppPhoton.Bolt.BoltNetwork.MaxConnections}");
        }
    }
}
