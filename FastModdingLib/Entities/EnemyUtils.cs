using Duckov.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FastModdingLib
{
    public static class EnemyUtils
    {
        public static CharacterRandomPreset GetPreset(string name)
        {
            return GameplayDataSettings.CharacterRandomPresetData.presets.Where(p => p.nameKey == name).First();
        }
    }
}
