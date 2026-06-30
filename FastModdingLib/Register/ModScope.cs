using System;

namespace FastModdingLib.Register
{
    /// <summary>
    /// <see cref="RegistryManager.EnterModScope"/> 返回的作用域对象。
    /// 构造时保存进入前的 modid 并切换到新 modid；<see cref="Dispose"/> 还原。
    /// </summary>
    internal class ModScope : IDisposable
    {
        private readonly string? _previousModid;
        private bool _disposed;

        internal ModScope(string modid)
        {
            _previousModid = RegistryManager.CurrentModid;
            RegistryManager.SetCurrentModid(modid);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            RegistryManager.SetCurrentModid(_previousModid);
        }
    }
}