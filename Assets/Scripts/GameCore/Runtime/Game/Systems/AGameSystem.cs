using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 由 GameManager 显式装配的运行时系统基类。
    /// 这里只保留系统自身的初始化和启停；地图、存档等领域通知统一直接使用 EventKit。
    /// </summary>
    public abstract class AGameSystem : MonoBehaviour
    {
        private static readonly Type[] EmptyStartupDependencies = Array.Empty<Type>();

        /// <summary>
        /// 当前系统进入初始化和启动阶段前必须已经就绪的其它系统类型。
        /// 这里只声明真实启动依赖，不用于表达运行期间的一般协作关系。
        /// </summary>
        public virtual IReadOnlyCollection<Type> StartupDependencies => EmptyStartupDependencies;

        public virtual void OnSystemInit() { }
        public virtual void OnSystemStart() { }
        public virtual void OnSystemStop() { }
        public virtual void OnSystemShutdown() { }
    }
}

