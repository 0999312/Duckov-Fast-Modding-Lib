namespace FastModdingLib.Events.GameEvents
{
    /// <summary>
    /// 控制角色变更事件。桥接自游戏原生 <c>LevelManager.OnControllingCharacterChanged</c> 静态事件。
    /// 仅观察用途，不支持取消。
    /// </summary>
    public sealed class ControllingCharacterChangedEvent : Event
    {
        /// <summary>
        /// 变更前的控制角色。
        /// TODO: 确认 CharacterMainControl 命名空间后替换为强类型（当前 object 兜底保证编译）。
        /// </summary>
        public object? OldCharacter { get; }

        /// <summary>
        /// 变更后的控制角色。
        /// TODO: 确认 CharacterMainControl 命名空间后替换为强类型（当前 object 兜底保证编译）。
        /// </summary>
        public object? NewCharacter { get; }

        public ControllingCharacterChangedEvent(object? oldCharacter, object? newCharacter)
        {
            OldCharacter = oldCharacter;
            NewCharacter = newCharacter;
        }
    }
}
