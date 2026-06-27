using FastModdingLib.Tests;
using HarmonyLib;

namespace FastModdingLib
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        public void Awake() {
            
        }
        protected override void OnAfterSetup()
        {
            var harmony = new Harmony("fastmoddinglib");
            harmony.PatchAll();
        }
    }
}