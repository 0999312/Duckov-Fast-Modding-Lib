using System;

namespace FastModdingLib.Entities
{
    /// <summary>
    /// 状态机中的一条转换规则。
    /// </summary>
    public struct Transition
    {
        /// <summary>目标状态名称（对应 <see cref="IStateConfig"/> 中的 const string state）。</summary>
        public string targetState;

        /// <summary>转换触发条件。返回 true 时触发到此状态的转换。</summary>
        public Func<bool> condition;

        /// <summary>优先级（数值大者优先评估）。</summary>
        public int priority;

        public Transition(string targetState, Func<bool> condition, int priority = 0)
        {
            this.targetState = targetState ?? throw new ArgumentNullException(nameof(targetState));
            this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
            this.priority = priority;
        }
    }

    /// <summary>
    /// 声明式敌人 AI 状态机接口（PLAN-EnemyUtils §2）。
    /// modder 实现此接口来描述状态、转换规则与每帧行为。
    /// FML 通过 <see cref="StateMachineToBT.Compile"/> 将实现编译为 NodeCanvas BehaviourTree。
    /// </summary>
    public interface IStateConfig
    {
        /// <summary>起始状态名称。</summary>
        string GetInitialState();

        /// <summary>进入某个状态时调用一次。</summary>
        void OnStateEnter(string state);

        /// <summary>每帧更新当前状态。</summary>
        void OnStateUpdate(string state, float deltaTime);

        /// <summary>离开某个状态时调用一次。</summary>
        void OnStateExit(string state);

        /// <summary>
        /// 获取从 <paramref name="currentState"/> 出发的所有可用转换。
        /// 返回的 Transition 按 priority 降序评估，条件满足时立即跳转。
        /// </summary>
        Transition[] GetTransitions(string currentState);
    }
}
