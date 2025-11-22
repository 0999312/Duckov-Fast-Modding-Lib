using ItemStatsSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;

namespace FastModdingLib
{
    public static class AssetUtil
    {
        public static AssetBundle? LoadBundle(string modPath, string bundleName)
        {
            string modDirectory = Path.GetDirectoryName(modPath);
            StringBuilder assetLoc = new StringBuilder($"assets/bundle/");
            //string resourceName = "dragon";
            assetLoc.Append(bundleName);
            string fileLoc = Path.Combine(modDirectory, assetLoc.ToString());

            var assetBundle
                = AssetBundle.LoadFromFile(fileLoc);
            if (assetBundle == null)
            {
                Debug.Log($"Failed to load AssetBundle {bundleName}!");
            }
            return assetBundle;
        }

    }
}
