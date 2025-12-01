using System.IO;
using System.Text;
using UnityEngine;

namespace FastModdingLib
{
    public static class AssetUtil
    {
        public static AssetBundle? LoadBundle(string modPath, string bundleName)
        {
            string modDirectory = Path.GetDirectoryName(modPath);
            StringBuilder assetLoc = new StringBuilder($"assets/bundle/");
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
