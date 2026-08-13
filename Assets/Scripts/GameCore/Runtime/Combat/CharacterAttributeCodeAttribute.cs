using System;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 标记一个由 EX-GAS FightUnit 属性码填写的角色作者字段。
    /// 该标记只改变 Unity Inspector 的选择方式，不保存第二份属性身份。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class CharacterAttributeCodeAttribute : PropertyAttribute
    {
    }
}
