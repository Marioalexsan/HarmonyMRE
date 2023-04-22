using System.Reflection.Emit;
using System.Reflection;
using Microsoft.Extensions.Logging;
using HarmonyLib;
using HarmonyMRE;
using Microsoft.Extensions.Configuration;
using System.Drawing;
using System.Diagnostics;

namespace HarmonyMRE
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var harmony = new Harmony("HarmonyMRE");
            harmony.PatchAll(typeof(Program).Assembly);

            Console.WriteLine($"Patched {harmony.GetPatchedMethods().Count()} methods!");
            Console.WriteLine("Press Enter to exit.");
            Console.ReadLine();
        }
    }

    // Target method
    public class Target
    {
        // Code pulled from the game
        public void _Chat_ParseCommand(string sMessage, long iConnection)
        {
            try
            {
                int iIndex = sMessage.IndexOf(' ');
                bool bNoSecondPart = iIndex == -1;
                if (iIndex == -1)
                {
                    iIndex = sMessage.Length;
                }
                string sFirst = sMessage.Substring(0, iIndex);
                sMessage = sMessage.Substring(sMessage.IndexOf(' ') + 1);
                if (bNoSecondPart)
                {
                    sMessage = "";
                }
                sFirst.ToLowerInvariant();
                switch (sFirst)
                {
                    case "sethp":
                        // Command stuff
                        break;
                    case "appconfig":
                        // Command stuff
                        break;
                    case "trollpvp":
                        // Command stuff
                        break;
                    // More branches
                }
            }
            catch (Exception e)
            {
                // This branch would have logged to game chat
                Console.WriteLine(e.Message);
            }
        }
    }

    // Patch class
    [HarmonyPatch(typeof(Target), nameof(Target._Chat_ParseCommand))]
    static class Patch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code, ILGenerator gen)
        {
            var codeList = code.ToList();

            Label afterRet = gen.DefineLabel();

            MethodInfo target = typeof(string).GetMethod(nameof(string.ToLowerInvariant));
            MethodInfo implementerCall = SymbolExtensions.GetMethodInfo(() => ParseModCommands(default, default, default));

            var insert = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldloc_S, 2),
                new CodeInstruction(OpCodes.Ldarg_S, 1),
                new CodeInstruction(OpCodes.Ldarg_S, 2),
                new CodeInstruction(OpCodes.Call, implementerCall),
                new CodeInstruction(OpCodes.Brfalse, afterRet),
                new CodeInstruction(OpCodes.Ret),
                new CodeInstruction(OpCodes.Nop).WithLabels(afterRet)
            };

            return codeList.InsertAfterMethod(target, insert);
        }

        static bool ParseModCommands(string command, string message, int connection)
        {
            string[] words = command.Split(new[] { ':' }, 2);
            if (words.Length != 2)
                return false; // Is probably a vanilla command

            string target = words[0];
            string trueCommand = words[1];

            // More stuff that has mod code

            return true;
        }
    }

    // Helper method, pulled as-is from code for the time being
    public static class PatchUtils
    {
        public static List<CodeInstruction> InsertAfterMethod(this List<CodeInstruction> code, MethodInfo target, List<CodeInstruction> insertedCode, int methodIndex = 0, int startOffset = 0, bool editsReturnValue = false)
        {
            int counter = methodIndex + 1;
            bool noReturnValue = target.ReturnType == typeof(void);

            int index = 0;
            int codeCount = code.Count;

            // Search for the method
            while (!(index >= startOffset && code[index].Calls(target) && --counter == 0))
            {
                if (index == codeCount)
                {
                    throw new InvalidOperationException("Could not find the target method call.");
                }

                index += 1;
            }

            int stackDelta = noReturnValue || editsReturnValue ? 0 : 1;

            int firstIndex = index;

            // Find method end

            while (stackDelta > 0 && firstIndex < code.Count)
            {
                firstIndex += 1;
                stackDelta += s_stackDeltas[code[firstIndex].opcode.StackBehaviourPush];
                stackDelta += s_stackDeltas[code[firstIndex].opcode.StackBehaviourPop];

                if (stackDelta < 0)
                {
                    throw new InvalidOperationException("Instructions after the method have an invalid state.");
                }
            }

            if (stackDelta != 0)
            {
                throw new InvalidOperationException("Could not calculate insert position.");
            }

            // For methods that come right before scopes, shift labels and stuff

            insertedCode[insertedCode.Count - 1].WithLabels(code[firstIndex + 1].labels.ToArray());
            code[firstIndex + 1].labels.Clear();

            code.InsertRange(firstIndex + 1, insertedCode);

            return code;
        }

        private static readonly Dictionary<StackBehaviour, int> s_stackDeltas;

        static PatchUtils()
        {
            s_stackDeltas = new Dictionary<StackBehaviour, int>()
            {
                { StackBehaviour.Pop0, 0 },
                { StackBehaviour.Pop1, -1 },
                { StackBehaviour.Pop1_pop1, -2 },
                { StackBehaviour.Popi, -1 },
                { StackBehaviour.Popi_pop1, -2 },
                { StackBehaviour.Popi_popi, -2 },
                { StackBehaviour.Popi_popi_popi, -3 },
                { StackBehaviour.Popi_popi8, -2 },
                { StackBehaviour.Popi_popr4, -2 },
                { StackBehaviour.Popi_popr8, -2 },
                { StackBehaviour.Popref, -1 },
                { StackBehaviour.Popref_pop1, -2 },
                { StackBehaviour.Popref_popi, -2 },
                { StackBehaviour.Popref_popi_pop1, -3 },
                { StackBehaviour.Popref_popi_popi, -3 },
                { StackBehaviour.Popref_popi_popi8, -3 },
                { StackBehaviour.Popref_popi_popr4, -3 },
                { StackBehaviour.Popref_popi_popr8, -3 },
                { StackBehaviour.Popref_popi_popref, -3 },
                { StackBehaviour.Push0, 0 },
                { StackBehaviour.Push1, 1 },
                { StackBehaviour.Push1_push1, 2 },
                { StackBehaviour.Pushi, 1 },
                { StackBehaviour.Pushi8, 1 },
                { StackBehaviour.Pushr4, 1 },
                { StackBehaviour.Pushr8, 1 },
                { StackBehaviour.Pushref, 1 },
                { StackBehaviour.Varpop, -1 },
                { StackBehaviour.Varpush, 1 }
            };
        }
    }
}