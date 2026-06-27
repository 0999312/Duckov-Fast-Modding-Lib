using FastModdingLib.Utils;
using ItemStatsSystem;

namespace FastModdingLib.Register
{
    public class RegistryManager: Singleton<RegistryManager>
    {
        public readonly NonAlterableSimpleRegistry<ERegistry> Registry = new NonAlterableSimpleRegistry<ERegistry>();

        public readonly NonAlterableSimpleRegistry<int> ItemID = new NonAlterableSimpleRegistry<int>();
        
        protected RegistryManager()
        {
            Registry.Set(new Identifier("fastmoddinglib", "itemid"), ItemID);
        }
    }
}