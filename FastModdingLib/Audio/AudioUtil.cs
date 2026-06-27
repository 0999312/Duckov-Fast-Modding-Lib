using System.Collections.Generic;
using FastModdingLib.Register;
using FastModdingLib.Utils;

namespace FastModdingLib.Audio
{
    public class AudioUtil: Singleton<AudioUtil>
    {
        private Dictionary<string, Identifier> mapdata;
        public SimpleRegistry<AudioData> dataRegistry;
        
        private AudioUtil()
        {
            dataRegistry = new SimpleRegistry<AudioData>();
            RegistryManager.Instance.Registry.Set(new Identifier("fastmoddinglib", "audio"), dataRegistry);
            mapdata = new Dictionary<string, Identifier>();
        }

        public void RegisterAudio(Identifier id, AudioData data)
        {
            dataRegistry.Set(id, data);
            mapdata[data.Eventname] = id;
        }

        public Identifier? GetIdentifier(string path)
        {
            mapdata.TryGetValue(path, out var v);
            return v;
        }
    }
}