using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace GameCore
{
    /// <summary>
    /// 可刷出的角色及其权重配置。
    /// 所有条目的 Rate 总和必须为 100。
    /// </summary>
    [Serializable]
    public struct CharacterSpawn
    {
        [InspectorName("角色 Prefab")]
        [Tooltip("要通过 PersistenceSystem 实例化的正式 CharacterActor Prefab。")]
        [SerializeField] private GameObject m_prefab;

        [InspectorName("权重")]
        [Tooltip("刷出概率权重；同一刷怪器下所有条目之和必须为 100。")]
        [SerializeField] private int m_rate;

        public GameObject Prefab => m_prefab;
        public int Rate => m_rate;
    }

    /// <summary>
    /// 角色刷怪器存档数据。
    /// FormerlySerializedAs 保留旧 monster 字段兼容，当前语义已经统一为 character。
    /// </summary>
    [Serializable]
    public class CharacterSpawnerDataBlock : PersistableDataBlock
    {
        [FormerlySerializedAs("monsters")]
        public CharacterActorRuntimeStateData[] characters;

        [FormerlySerializedAs("monsterIndices")]
        public int[] characterIndices;

        public float spawnTimer;

        [FormerlySerializedAs("totalSpawnedMonsterCount")]
        public int totalSpawnedCharacterCount;
    }

    /// <summary>
    /// 刷怪器当前已生成角色的运行时记录。
    /// Index 指回 m_characters，便于存档后按原配置恢复。
    /// </summary>
    internal struct SpawnedCharacter
    {
        public CharacterActor Character { get; set; }
        public int Index { get; set; }
    }

    /// <summary>
    /// 角色刷怪器基类。
    /// 子类只负责提供刷出位置；本类负责权重、数量限制、存档恢复和死亡后移出活体计数。
    /// </summary>
    public abstract class ACharacterSpawner : Persistable
    {
        [Header("通用设置")]
        [InspectorName("角色池")]
        [FormerlySerializedAs("m_monsters")]
        [SerializeField] private CharacterSpawn[] m_characters = Array.Empty<CharacterSpawn>();
        [InspectorName("最小等级")]
        [SerializeField, Range(Constants.MinLevel, Constants.MaxLevel)] private int m_minLevel = Constants.MinLevel;
        [InspectorName("最大等级")]
        [SerializeField, Range(Constants.MinLevel, Constants.MaxLevel)] private int m_maxLevel = Constants.MaxLevel;

        [Header("刷出设置")]
        [InspectorName("刷出冷却")]
        [SerializeField] private float m_spawnCooldown = 5.0f;
        [InspectorName("预刷角色数")]
        [FormerlySerializedAs("m_monstersToPrespawn")]
        [SerializeField] private int m_charactersToPrespawn = 4;
        [InspectorName("最大同时存活数")]
        [FormerlySerializedAs("m_maxSimulatenousMonsterCount")]
        [SerializeField] private int m_maxSimultaneousCharacterCount = 4;

        [Header("刷出限制")]
        [InspectorName("限制总刷出数量")]
        [FormerlySerializedAs("m_limitMonsterCount")]
        [SerializeField] private bool m_limitCharacterCount = false;
        [InspectorName("最大总刷出数量")]
        [FormerlySerializedAs("m_maxMonsterCount")]
        [SerializeField, Min(1)] private int m_maxCharacterCount = 1;

        private readonly HashSet<SpawnedCharacter> m_spawnedCharacters = new();
        private float m_spawnTimer;
        private bool m_valid;
        private int m_totalSpawnedCharacterCount;
        private bool m_isFirstUpdate = true;
        private bool m_disablePrespawn;

        protected abstract Vector2 FindSpawnLocation();

        private bool Validate()
        {
            if (m_characters == null || m_characters.Length == 0)
            {
                return m_valid = false;
            }

            int rateSum = 0;
            foreach (CharacterSpawn character in m_characters)
            {
                rateSum += character.Rate;
            }

            return m_valid = rateSum == 100;
        }

        private void Prespawn()
        {
            for (int i = 0; i < m_charactersToPrespawn; ++i)
            {
                TrySpawn();
            }
        }

        private void ResetSpawnTimer()
        {
            m_spawnTimer = m_spawnCooldown;
        }

        private void Update()
        {
            if (m_isFirstUpdate)
            {
                bool isValid = Validate();
                Debug.Assert(isValid, "CharacterSpawner validation failed. Make sure the total spawn rate is equal to 100.");
                Array.Sort(m_characters, (left, right) => left.Rate.CompareTo(right.Rate));
                if (!m_disablePrespawn)
                {
                    Prespawn();
                }

                m_isFirstUpdate = false;
            }

            if (CanSpawn())
            {
                m_spawnTimer -= Time.deltaTime;
                if (m_spawnTimer <= 0.0f)
                {
                    TrySpawn();
                    ResetSpawnTimer();
                }
            }
            else
            {
                ResetSpawnTimer();
            }
        }

        private int FindCharacterIndexToSpawn()
        {
            int randomNumber = UnityEngine.Random.Range(0, 100);
            for (int i = 0; i < m_characters.Length; ++i)
            {
                CharacterSpawn character = m_characters[i];
                if (randomNumber <= character.Rate)
                {
                    return i;
                }

                randomNumber -= character.Rate;
            }

            return -1;
        }

        private bool CanSpawn()
        {
            // 统一角色死亡后会保留尸体并继续参与存档，但尸体不能占用存活角色的并发刷出名额。
            int livingCharacterCount = m_spawnedCharacters.Count(
                spawned => spawned.Character != null && !spawned.Character.dead);
            bool simultaneousLimitReached = livingCharacterCount >= m_maxSimultaneousCharacterCount;
            bool totalLimitReached = m_limitCharacterCount && m_totalSpawnedCharacterCount >= m_maxCharacterCount;
            return m_valid && !simultaneousLimitReached && !totalLimitReached;
        }

        private void TrySpawn()
        {
            if (CanSpawn())
            {
                Spawn();
            }
        }

        private void SpawnBack(CharacterActorRuntimeStateData runtimeState, int characterIndex)
        {
            CharacterSpawn characterSpawn = m_characters[characterIndex];
            CharacterActor character = Spawn(
                characterSpawn.Prefab,
                characterIndex,
                runtimeState.position,
                runtimeState.rotation);
            character.LoadActorRuntimeState(runtimeState);
        }

        private CharacterActor Spawn(GameObject prefab, int characterIndex, Vector3 position, Quaternion rotation)
        {
            CharacterActor character =
                GameManager.PersistenceSystem.InstantiateCustom<CharacterActor>(prefab, position, rotation);

            character.AddDestroyedListener(
                () => m_spawnedCharacters.RemoveWhere(spawned => spawned.Character == character));

            m_spawnedCharacters.Add(new SpawnedCharacter
            {
                Character = character,
                Index = characterIndex
            });

            return character;
        }

        private void Spawn()
        {
            Vector2 position = FindSpawnLocation();
            int characterIndex = FindCharacterIndexToSpawn();
            if (characterIndex < 0)
            {
                Debug.LogError("Couldn't find a character to spawn. Check that spawn rates sum to 100.", this);
                return;
            }

            CharacterActor character = Spawn(
                m_characters[characterIndex].Prefab,
                characterIndex,
                position,
                Quaternion.identity);
            ++m_totalSpawnedCharacterCount;
            character.SetLevel(UnityEngine.Random.Range(m_minLevel, m_maxLevel + 1));
        }

        protected override Type GetDataBlockType() => typeof(CharacterSpawnerDataBlock);

        protected override void OnLoad(PersistableDataBlock block)
        {
            base.OnLoad(block);
            var dataBlock = block.As<CharacterSpawnerDataBlock>();

            m_disablePrespawn = true;
            m_spawnTimer = dataBlock.spawnTimer;

            int characterCount = Math.Min(
                dataBlock.characters?.Length ?? 0,
                dataBlock.characterIndices?.Length ?? 0);
            for (int i = 0; i < characterCount; ++i)
            {
                SpawnBack(dataBlock.characters[i], dataBlock.characterIndices[i]);
            }

            m_totalSpawnedCharacterCount = dataBlock.totalSpawnedCharacterCount;
        }

        protected override void OnSave(PersistableDataBlock block)
        {
            base.OnSave(block);
            var dataBlock = block.As<CharacterSpawnerDataBlock>();
            dataBlock.characters = m_spawnedCharacters
                .Select(spawned => spawned.Character.CreateActorRuntimeState())
                .ToArray();
            dataBlock.characterIndices = m_spawnedCharacters
                .Select(spawned => spawned.Index)
                .ToArray();
            dataBlock.spawnTimer = m_spawnTimer;
            dataBlock.totalSpawnedCharacterCount = m_totalSpawnedCharacterCount;
        }
    }
}
