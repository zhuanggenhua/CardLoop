using System;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [Serializable]
    public class DataBlock
    {
        public T As<T>() where T : DataBlock
        {
            Debug.Assert(this is T, $"Expected block to be of type {typeof(T).Name}, but got {GetType().Name}");
            return (T)this;
        }
    }
}

