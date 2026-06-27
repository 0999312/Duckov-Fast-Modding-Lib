using System;
using FastModdingLib.Utils;

namespace FastModdingLib.Register
{
    public class NonAlterableSimpleRegistry<T>: SimpleRegistry<T>
    {
        public override T this[Identifier id]
        {
            get => dict[id];
            set => dict.TryAdd(id, value);
        }

        public override void Set(Identifier id, T value)
        {
            if (!dict.TryAdd(id, value))
            {
                throw new ArgumentException("Key already exists!");
            }
        }
    }
}