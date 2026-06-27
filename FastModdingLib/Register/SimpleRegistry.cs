using System;
using System.Collections.Generic;
using FastModdingLib.Utils;

namespace FastModdingLib.Register
{
    public class SimpleRegistry<T>: IRegistry<T>
    {
        protected Dictionary<Identifier, T> dict;

        public SimpleRegistry()
        {
            dict = new Dictionary<Identifier, T>();
        }

        public virtual T this[Identifier id]
        {
            get => dict[id];
            set => dict[id] = value;
        }

        public virtual bool TryGet(Identifier id, out T value)
        {
            bool ret = dict.TryGetValue(id, out value);
            return ret;
        }

        public virtual T Get(Identifier id)
        {
            T var;
            bool ret = dict.TryGetValue(id, out var);
            if (!ret) throw new IndexOutOfRangeException("Key not exist.");
            return var;
        }

        public virtual void Set(Identifier id, T value)
        {
            dict[id] = value;
        }
    }
}