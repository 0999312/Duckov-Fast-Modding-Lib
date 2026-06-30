using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FastModdingLib.Events
{
    /// <summary>
    /// 协程宿主。internal 仅供 <see cref="AsyncEventBus"/> 内部使用；
    /// 由 Bootstrap 在 <c>ModBehaviour.OnAfterSetup</c> 一次创建并 DontDestroyOnLoad。
    /// </summary>
    internal sealed class EventBusRunner : MonoBehaviour
    {
        private readonly List<IEnumerator> _active = new List<IEnumerator>();

        /// <summary>
        /// 启动一个协程并追踪，便于 <see cref="StopAll"/> 时统一停止。
        /// </summary>
        public void Run(IEnumerator coroutine)
        {
            _active.Add(coroutine);
            StartCoroutine(TrackAndRemove(coroutine));
        }

        /// <summary>
        /// 停止所有由 <see cref="Run"/> 启动的协程。mod 卸载时调用（PLAN §13 风险对策）。
        /// </summary>
        public void StopAll()
        {
            StopAllCoroutines();
            _active.Clear();
        }

        private IEnumerator TrackAndRemove(IEnumerator coroutine)
        {
            while (coroutine.MoveNext())
            {
                yield return coroutine.Current;
            }
            _active.Remove(coroutine);
        }
    }
}