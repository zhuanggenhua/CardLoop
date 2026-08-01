using System;
using UnityEngine;
using UnityEngine.Serialization;
using MackySoft.SerializeReferenceExtensions;

namespace FantasyWord.GameCore
{
    [Serializable]
    public struct Loot
    {
        [SerializeReference, SubclassSelector] public ICondition condition;
        public Item item;
        public int quantity;
        public int dropRate;
        [FormerlySerializedAs("minimumMonsterLevel")]
        public int minimumDefeatedCharacterLevel;
        [FormerlySerializedAs("minimumPlayerLevel")]
        public int minimumReceiverLevel;

        public bool IsAvailable() => condition?.Evaluate() ?? true;
        public bool ResolveDrop() => UnityEngine.Random.Range(1, 101) <= dropRate;
    }
}

