// Les tâches d'IA (Behavior Designer) ont des caches par joueur (m_*) alloués pour 4 joueurs.
// Au 5e joueur, CanHearPlayer.IsPlayerCrawling(4) lève IndexOutOfRange et casse le tick de tout l'arbre :
// les ennemis n'entendent plus, ne frappent plus et ne réagissent plus à la lampe UV.
// On agrandit ces tableaux à Core.Max dès que la tâche est réveillée ou mise à jour.

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;

namespace DevourMorePlayers
{
    static class AIPlayerArrays
    {
        static readonly Type[] TaskTypes =
        {
            typeof(Il2CppBehaviorDesigner.Runtime.Tasks.Movement.CanHearPlayer),
            typeof(Il2CppBehaviorDesigner.Runtime.Tasks.Movement.CanHearOtherPlayer),
            typeof(Il2CppBehaviorDesigner.Runtime.Tasks.Movement.CanSeePlayer),
            typeof(Il2CppBehaviorDesigner.Runtime.Tasks.Unity.UnityGameObject.PlayerFlashlightOnMe),
            typeof(Il2CppBehaviorDesigner.Runtime.Tasks.Unity.UnityGameObject.PlayerTouchingMe),
        };

        static readonly Dictionary<Type, PropertyInfo[]> ArrayProps = new();
        static readonly HashSet<IntPtr> Done = new();
        static readonly HashSet<string> Logged = new();

        public static void Init(HarmonyLib.Harmony harmony)
        {
            var postfix = new HarmonyMethod(typeof(AIPlayerArrays), nameof(Postfix));
            var prefix = new HarmonyMethod(typeof(AIPlayerArrays), nameof(Prefix));
            foreach (var t in TaskTypes)
            {
                var props = Array.FindAll(t.GetProperties(BindingFlags.Instance | BindingFlags.Public),
                    p => p.Name.StartsWith("m_") && p.CanRead && p.CanWrite && p.PropertyType.IsGenericType &&
                         (p.PropertyType.Name.StartsWith("Il2CppStructArray") || p.PropertyType.Name.StartsWith("Il2CppReferenceArray")));
                ArrayProps[t] = props;
                try
                {
                    var awake = AccessTools.Method(t, "OnAwake");
                    if (awake != null) harmony.Patch(awake, postfix: postfix);
                    var update = AccessTools.Method(t, "OnUpdate");
                    if (update != null) harmony.Patch(update, prefix: prefix);
                    MelonLogger.Msg($"[IA] {t.Name} : {props.Length} tableaux par joueur surveillés");
                }
                catch (Exception e) { MelonLogger.Warning($"[IA] patch {t.Name} échoué : {e.Message}"); }
            }
        }

        static void Postfix(Il2CppObjectBase __instance) => Grow(__instance);
        static void Prefix(Il2CppObjectBase __instance) => Grow(__instance);

        static void Grow(Il2CppObjectBase instance)
        {
            if (instance == null) return;
            var ptr = instance.Pointer;
            if (ptr == IntPtr.Zero || Done.Contains(ptr)) return;

            Type type = null;
            foreach (var t in TaskTypes) if (t.IsInstanceOfType(instance)) { type = t; break; }
            if (type == null || !ArrayProps.TryGetValue(type, out var props)) return;

            int max = Core.Max;
            bool allReady = true;
            foreach (var p in props)
            {
                try
                {
                    var arr = p.GetValue(instance);
                    if (arr == null) { allReady = false; continue; }
                    int len = Convert.ToInt32(arr.GetType().GetProperty("Length").GetValue(arr));
                    if (len >= max) continue;

                    var bigger = Activator.CreateInstance(p.PropertyType, (long)max);
                    var item = arr.GetType().GetProperty("Item");
                    for (int i = 0; i < len; i++)
                        item.SetValue(bigger, item.GetValue(arr, new object[] { i }), new object[] { i });
                    p.SetValue(instance, bigger);

                    if (Logged.Add(type.Name + "." + p.Name))
                        MelonLogger.Msg($"[IA] {type.Name}.{p.Name} : {len} -> {max}");
                }
                catch (Exception e)
                {
                    if (Logged.Add("err:" + type.Name + "." + p.Name))
                        MelonLogger.Warning($"[IA] {type.Name}.{p.Name} : {e.GetType().Name} {e.Message}");
                }
            }
            if (allReady) Done.Add(ptr);
        }
    }
}
