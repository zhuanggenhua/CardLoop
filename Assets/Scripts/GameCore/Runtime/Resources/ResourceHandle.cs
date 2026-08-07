using System;
using Cysharp.Threading.Tasks;
using UnityEngine.Assertions;

namespace GameCore
{
    /// <summary>
    /// 项目资源句柄的共享状态。结构体句柄被复制后仍指向同一状态，释放操作只会执行一次。
    /// </summary>
    internal abstract class ResourceOperationState
    {
        private bool m_released;

        protected ResourceOperationState(string address, byte operationType)
        {
            Address = address;
            OperationType = operationType;
        }

        public string Address { get; }
        public byte OperationType { get; }
        public abstract string PackageName { get; }
        public abstract bool UsesPackage(string packageName);
        public bool IsValid => !m_released && IsOperationValid;
        public bool IsDone => IsValid && IsOperationDone;
        public object Result => IsValid ? GetResult() : null;

        protected abstract bool IsOperationValid { get; }
        protected abstract bool IsOperationDone { get; }
        protected abstract object GetResult();
        protected abstract void WaitForCompletionCore();
        protected abstract UniTask<object> AwaitResultCore();
        protected abstract void RegisterCallbackCore(Action<object> callback);
        protected abstract void ReleaseCore();

        public void WaitForCompletion()
        {
            EnsureValid();
            WaitForCompletionCore();
        }

        public UniTask<object> AwaitResultAsync()
        {
            EnsureValid();
            return AwaitResultCore();
        }

        public void RegisterCallback(Action<object> callback)
        {
            Assert.IsNotNull(callback);
            EnsureValid();
            RegisterCallbackCore(callback);
        }

        public void Release()
        {
            if (m_released)
            {
                return;
            }

            m_released = true;
            try
            {
                ReleaseCore();
            }
            finally
            {
                ResourceSystem.NotifyReleased(this);
            }
        }

        private void EnsureValid()
        {
            if (!IsValid)
            {
                throw new InvalidOperationException($"资源句柄已经释放或失效，地址：{Address}");
            }
        }
    }

    /// <summary>
    /// YooAsset 资源句柄。未类型化入口主要用于通用释放和编辑器诊断。
    /// </summary>
    public readonly struct ResourceHandle : IEquatable<ResourceHandle>, IDisposable
    {
        internal readonly ResourceOperationState State;

        internal ResourceHandle(ResourceOperationState state)
        {
            State = state;
        }

        public object Result => State?.Result;

        public void RegisterCallback(Action<UnityEngine.Object> callback)
        {
            Assert.IsNotNull(callback);
            State?.RegisterCallback(result => callback(result as UnityEngine.Object));
        }

        public UnityEngine.Object WaitForCompletion()
        {
            State?.WaitForCompletion();
            return Result as UnityEngine.Object;
        }

        public ResourceHandle<T> Convert<T>()
        {
            return new ResourceHandle<T>(State);
        }

        public bool Equals(ResourceHandle other)
        {
            return ReferenceEquals(State, other.State);
        }

        public override bool Equals(object obj)
        {
            return obj is ResourceHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return State?.GetHashCode() ?? 0;
        }

        public void Dispose()
        {
            State?.Release();
        }
    }

    /// <summary>
    /// 类型化 YooAsset 资源句柄，负责把底层结果转换为调用方请求的类型。
    /// </summary>
    public readonly struct ResourceHandle<T> : IEquatable<ResourceHandle<T>>, IDisposable
    {
        internal readonly ResourceOperationState State;

        internal ResourceHandle(ResourceOperationState state)
        {
            State = state;
        }

        public T Result => State == null || State.Result == null ? default : (T)State.Result;

        public static implicit operator ResourceHandle(ResourceHandle<T> handle)
        {
            return new ResourceHandle(handle.State);
        }

        public static implicit operator ResourceHandle<T>(ResourceHandle handle)
        {
            return new ResourceHandle<T>(handle.State);
        }

        public ResourceHandle<TNew> Convert<TNew>()
        {
            return new ResourceHandle<TNew>(State);
        }

        public void RegisterCallback(Action<T> callback)
        {
            Assert.IsNotNull(callback);
            State?.RegisterCallback(result => callback(result == null ? default : (T)result));
        }

        public void RegisterCallback(Action callback)
        {
            Assert.IsNotNull(callback);
            State?.RegisterCallback(_ => callback());
        }

        public T WaitForCompletion()
        {
            State?.WaitForCompletion();
            return Result;
        }

        public bool Equals(ResourceHandle<T> other)
        {
            return ReferenceEquals(State, other.State);
        }

        public override bool Equals(object obj)
        {
            return obj is ResourceHandle<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            return State?.GetHashCode() ?? 0;
        }

        public void Dispose()
        {
            State?.Release();
        }
    }
}
