using HarmonyLib;
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace TranspilerExplorer
{
    static class HarmonyPatch_Register_ReversePatches_Patch
    {
        public static ConcurrentBag<(MethodBase original, MethodInfo standin, MethodInfo transpiler, bool erroring)> reverseTranspilers = [];
        internal static void Finalizer(HarmonyMethod standin, MethodBase original, MethodInfo postTranspiler, Exception __exception)
        {
            if (postTranspiler == null)
            {
                return;
            }

            reverseTranspilers.Add((original, standin.method, postTranspiler, __exception != null));
        }
    }
}
