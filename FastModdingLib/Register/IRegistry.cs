using FastModdingLib.Utils;

namespace FastModdingLib.Register
{
    public interface IRegistry<T>: ERegistry
    {
        public T this[Identifier id] { get; set; }
        public bool TryGet(Identifier id, out T value);
        public T Get(Identifier id);
        public void Set(Identifier id, T value);
    }
}