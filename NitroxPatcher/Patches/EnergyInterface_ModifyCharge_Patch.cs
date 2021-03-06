﻿using Harmony;
using NitroxClient.MonoBehaviours;
using System;
using System.Reflection;

namespace NitroxPatcher.Patches
{
    public class EnergyInterface_ModifyCharge_Patch : NitroxPatch
    {
        public static readonly Type TARGET_CLASS = typeof(EnergyInterface);
        public static readonly MethodInfo TARGET_METHOD = TARGET_CLASS.GetMethod("ModifyCharge", BindingFlags.Public | BindingFlags.Instance);

        public static void Postfix(EnergyInterface __instance, float amount, float __result)
        {
            PowerMonitor powerMonitor = __instance.gameObject.GetComponent<PowerMonitor>();

            if (powerMonitor == null)
            {
                powerMonitor = __instance.gameObject.AddComponent<PowerMonitor>();
            }

            powerMonitor.ChargeChanged(__result, powerMonitor.gameObject);
        }

        public override void Patch(HarmonyInstance harmony)
        {
            PatchPostfix(harmony, TARGET_METHOD);
        }
    }
}
