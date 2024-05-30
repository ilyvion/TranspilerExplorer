using System;
using Verse;

namespace TranspilerExplorer
{
    [StaticConstructorOnStartup]
    [HotSwappable]
    static class TranspilerExplorer
    {
        static TranspilerExplorer()
        {
            TranspilerExplorerMod.harmony.PatchAll();
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class HotSwappableAttribute : Attribute
    {
    }
}
