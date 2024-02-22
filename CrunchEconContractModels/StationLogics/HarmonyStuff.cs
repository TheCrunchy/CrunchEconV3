using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using CrunchEconV3;
using HarmonyLib;
using NLog.Fluent;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using VRage.Game;
using VRage.Network;

public static class MyStoreBlockPatch
{
    public static void Patch(PatchContext ctx)
    {
        var _harmony = new Harmony("crunch.harmony");
        MyStoreBlockPatch.Apply(_harmony);
        _harmony.PatchAll();
    }

    public static void Apply(Harmony harmony)
    {
        var originalMethod = typeof(MyStoreBlock).GetMethod("GetStoreItems", BindingFlags.NonPublic | BindingFlags.Instance);
        var transpiler = new HarmonyMethod(typeof(MyStoreBlockPatch).GetMethod(nameof(GetStoreItemsTranspiler)));

        harmony.Patch(originalMethod, transpiler: transpiler);
    }
    public static IEnumerable<CodeInstruction> GetStoreItemsTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> instructionList = new List<CodeInstruction>(instructions);
        bool foundOriginalLine = false;

        for (int i = 0; i < instructionList.Count; i++)
        {
            if (!foundOriginalLine && instructionList[i].opcode == OpCodes.Call && instructionList[i].operand.ToString().Contains("MyMultiplayer.RaiseEvent"))
            {
                foundOriginalLine = true;
                while (instructionList[i].opcode != OpCodes.Ldc_R4 || Convert.ToSingle(instructionList[i].operand) != 1f)
                    i++; // Skip until we find the original Ldc_R4 1f instruction
                i += 6; // Skip the original instructions
                continue;
            }
            else if (foundOriginalLine && instructionList[i].opcode == OpCodes.Call && instructionList[i].operand.ToString().Contains("MyMultiplayer.RaiseEvent"))
            {
                // Replace the original line with the desired logic
                yield return new CodeInstruction(OpCodes.Ldarg_0); // Load 'this' onto the stack
                yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MyStoreBlock), "_economyComponent")); // Load '_economyComponent' field onto the stack
                yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MyStoreBlock), "_lastEconomyTick")); // Load '_lastEconomyTick' field onto the stack
                yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MyStoreBlock), "_ticks")); // Load '_ticks' field onto the stack
                yield return new CodeInstruction(OpCodes.Ldc_R4, 1f); // Load 1f onto the stack
                yield return new CodeInstruction(OpCodes.Stloc_1); // Store 1f into num1 variable
                yield return new CodeInstruction(OpCodes.Ldc_R4, 1f); // Load 1f onto the stack
                yield return new CodeInstruction(OpCodes.Stloc_2); // Store 1f into num2 variable

                yield return new CodeInstruction(OpCodes.Ldarg_0); // Load 'this' onto the stack
                yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MyStoreBlock), "_session")); // Load '_session' field onto the stack
                yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MySession), "Static")); // Load 'Static' field onto the stack
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MySession), "get_Factions")); // Call 'get_Factions' method
                yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MyFactionCollection), "Static")); // Load 'Static' field onto the stack
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(MyFactionCollection), "GetRelationBetweenPlayerAndFaction")); // Call 'GetRelationBetweenPlayerAndFaction' method
                yield return new CodeInstruction(OpCodes.Ldarg_0); // Load 'this' onto the stack
                yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MyStoreBlock), "_session")); // Load '_session' field onto the stack
                yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MySession), "Static")); // Load 'Static' field onto the stack
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MySession), "get_Players")); // Call 'get_Players' method
                yield return new CodeInstruction(OpCodes.Ldarg, 7); // Load 'sender' argument onto the stack
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Dictionary<long, MyPlayer>), "TryGetIdentityId")); // Call 'TryGetIdentityId' method
                yield return new CodeInstruction(OpCodes.Ldc_I4_0); // Load 0 onto the stack
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MyRelationsBetweenFactions), "op_Equality")); // Call 'op_Equality' method
                yield return new CodeInstruction(OpCodes.Brfalse_S, instructionList[i + 1].operand); // Branch if false to the next instruction
                yield return new CodeInstruction(OpCodes.Pop); // Discard the result of the previous comparison
                yield return new CodeInstruction(OpCodes.Ldloc_1); // Load 'num1' variable onto the stack
                yield return new CodeInstruction(OpCodes.Ldloc_2); // Load 'num2' variable onto the stack
                yield return new CodeInstruction(OpCodes.Ldstr, "{num1},{num2}"); // Load the format string onto the stack
                yield return new CodeInstruction(OpCodes.Ldloc_1); // Load 'num1' variable onto the stack
                yield return new CodeInstruction(OpCodes.Ldloc_2); // Load 'num2' variable onto the stack
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Log), "Info", new Type[] { typeof(string), typeof(float), typeof(float) })); // Call Core.Log.Info method with parameters

                i += 7; // Skip the remaining original instructions
                continue;
            }

            yield return instructionList[i];
        }
    }
}

