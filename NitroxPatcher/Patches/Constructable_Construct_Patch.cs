﻿using Harmony;
using NitroxClient.MonoBehaviours;
using System;
using System.Reflection;
using System.Reflection.Emit;

namespace NitroxPatcher.Patches
{
    public class Constructable_Construct_Patch : NitroxPatch
    {
        public static readonly Type TARGET_CLASS = typeof(Constructable);
        public static readonly MethodInfo TARGET_METHOD = TARGET_CLASS.GetMethod("Construct");
        
        public static bool Prefix(Constructable __instance)
        {
            if (!__instance._constructed && __instance.constructedAmount < 1.0f)
            {
                Multiplayer.Logic.Building.ChangeConstructionAmount(__instance.gameObject, __instance.constructedAmount);
            }

            return true;
        }

        public static void Postfix(Constructable __instance, bool __result)
        {
            if (__result && __instance.constructedAmount >= 1.0f)
            {
                Multiplayer.Logic.Building.ConstructionComplete(__instance.gameObject);
            }
        }

        public override void Patch(HarmonyInstance harmony)
        {
            PatchMultiple(harmony, TARGET_METHOD, true, true, false);
        }
    }
}
