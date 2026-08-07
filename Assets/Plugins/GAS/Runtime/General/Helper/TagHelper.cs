using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime
{
    public static class TagHelper
    {
        private static Dictionary<int, GameplayTag> _tagMap;
        private static Dictionary<int, string> _tagCode2TagName;
        
        /// <summary>
        ///     初始化TagMap
        /// </summary>
        /// <param name="tagMap"></param>
        /// <param name="tagCode2TagName"></param>
        public static void InitTagMap(Dictionary<int, GameplayTag> tagMap,Dictionary<int, string> tagCode2TagName)
        {
            if (tagMap == null) throw new ArgumentNullException(nameof(tagMap));
            if (tagCode2TagName == null) throw new ArgumentNullException(nameof(tagCode2TagName));
            if (_tagMap != null || _tagCode2TagName != null)
                throw new InvalidOperationException("GameplayTag 图已经初始化，必须先完整关闭 EX-GAS 才能重新初始化。");
            if (GASManager.ExWorld is not { IsCreated: true })
                throw new InvalidOperationException("GameplayTag 图必须在 EX-GAS World 创建后初始化。");

            using (var query = GASManager.EntityManager.CreateEntityQuery(
                       ComponentType.ReadOnly<SingletonGameplayTagMap>()))
            {
                if (!query.IsEmptyIgnoreFilter)
                    throw new InvalidOperationException("EX-GAS World 中已经存在 GameplayTag 图单例。");
            }

            // ECS专用单例TagMap
            var map = new NativeHashMap<int, ComGameplayTag>(tagMap.Keys.Count, Allocator.Persistent);
            foreach (var p in tagMap)
                map.TryAdd(p.Key, new ComGameplayTag
                {
                    Code = p.Value.Code,
                    Children = new NativeArray<int>(p.Value.Children, Allocator.Persistent),
                    Parents = new NativeArray<int>(p.Value.Parents, Allocator.Persistent)
                });

            GASManager.EntityManager.CreateSingleton(new SingletonGameplayTagMap { Map = map });
            _tagMap = tagMap;
            _tagCode2TagName = tagCode2TagName;
        }

        /// <summary>
        /// 释放 GameplayTag 图持有的全部原生内存。只能由 EX-GAS 的正式关闭流程调用。
        /// </summary>
        internal static void DisposeTagMap()
        {
            if (GASManager.ExWorld is not { IsCreated: true })
            {
                if (_tagMap != null || _tagCode2TagName != null)
                    throw new InvalidOperationException("EX-GAS World 已失效，但 GameplayTag 图仍处于初始化状态。");
                return;
            }

            using var query = GASManager.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<SingletonGameplayTagMap>());
            bool hasSingleton = !query.IsEmptyIgnoreFilter;
            bool hasManagedMap = _tagMap != null || _tagCode2TagName != null;
            if (hasSingleton != hasManagedMap)
                throw new InvalidOperationException("GameplayTag 的托管表和 ECS 单例生命周期不一致。");
            if (!hasSingleton)
                return;

            Entity singletonEntity = query.GetSingletonEntity();
            SingletonGameplayTagMap singleton =
                GASManager.EntityManager.GetComponentData<SingletonGameplayTagMap>(singletonEntity);
            foreach (var pair in singleton.Map)
            {
                ComGameplayTag tag = pair.Value;
                if (tag.Parents.IsCreated) tag.Parents.Dispose();
                if (tag.Children.IsCreated) tag.Children.Dispose();
            }

            if (singleton.Map.IsCreated) singleton.Map.Dispose();
            GASManager.EntityManager.SetComponentData(singletonEntity, default(SingletonGameplayTagMap));
            GASManager.EntityManager.DestroyEntity(singletonEntity);
            _tagMap = null;
            _tagCode2TagName = null;
        }

        /// <summary>
        ///     TagA是否含有TagB
        /// </summary>
        /// <param name="tagA"></param>
        /// <param name="tagB"></param>
        /// <returns></returns>
        public static bool HasTag(int tagA, int tagB)
        {
            if (_tagMap.ContainsKey(tagA) && _tagMap.ContainsKey(tagB))
                return _tagMap[tagA].HasTag(_tagMap[tagB]);
            return false;
        }
        
        public static bool HasTemporaryTag(Entity asc,Entity source,int tag)
        {
            var temporaryTags = GASManager.EntityManager.GetBuffer<BTemporaryTag>(asc);
            foreach (var t in temporaryTags)
                if (t.source==source && HasTag(t.tag, tag))
                    return true;
            return false;
        }

        public static bool AddTemporaryTagTo(Entity ascTarget,Entity source,int tag)
        {
            var temporaryTags = GASManager.EntityManager.GetBuffer<BTemporaryTag>(ascTarget);
            if(HasTemporaryTag(ascTarget,source,tag))
                return false;
            temporaryTags.Add(new BTemporaryTag {source = source,tag = tag});
            return true;
        }
        
        public static string GetTagFullName(int tagCode)
        {
            if (_tagCode2TagName.TryGetValue(tagCode, out var tagName))
            {
                return tagName;
            }
            
#if UNITY_EDITOR
            Debug.LogError($"[GEN] 标签码[{tagCode}]不存在!   请检查代码生成器是否正确生成了标签码!");
#endif
            return null;
        }
        
        /// <summary>
        /// 过滤掉无效的标签，在当前注册map里不存在的就认为是无效的标签。
        /// </summary>
        /// <param name="tags"></param>
        /// <param name="filterTag"></param>
        /// <returns></returns>
        public static int[] FilterInvalidTags(int[] tags)
        {
            if (_tagMap== null) return tags;
 
            var validTags = new List<int>();
            foreach (var tag in tags)
                if (_tagMap.ContainsKey(tag))
                    validTags.Add(tag);
            return validTags.ToArray();
        }
        
        public static List<int> FilterInvalidTags(List<int> tags)
        {
            if (_tagMap== null) return tags;
            
            var validTags = new List<int>();
            foreach (var tag in tags)
                if (_tagMap.ContainsKey(tag))
                    validTags.Add(tag);
            return validTags;
        }
    }
}
