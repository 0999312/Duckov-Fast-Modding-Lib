using System;

namespace FastModdingLib.Utils
{
    public abstract class Singleton<T> where T : Singleton<T>
    {
        private static readonly Lazy<T> _instance = new Lazy<T>(() =>
        {
            T instance = (T)Activator.CreateInstance(typeof(T), true)!;
            return instance;
        });

        public static T Instance
        {
            get => _instance.Value;
        }

        protected Singleton()
        {
        }
    }
}