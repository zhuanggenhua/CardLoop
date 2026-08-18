using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YokiFrame;

namespace Gameplay.Scenarios
{
    /// <summary>描述本次存档窗口是写入当前单局，还是读取已有单局。</summary>
    public enum ScenarioSavePanelMode
    {
        Save,
        Load
    }

    /// <summary>打开剧本存档窗口所需的唯一运行对象。</summary>
    public sealed class ScenarioSavePanelData : IUIData
    {
        public ScenarioDirector Director { get; }
        public ScenarioSavePanelMode Mode { get; }

        public ScenarioSavePanelData(ScenarioDirector director, ScenarioSavePanelMode mode)
        {
            Director = director ?? throw new ArgumentNullException(nameof(director));
            Mode = mode;
        }
    }

    /// <summary>
    /// 剧本单局的存档窗口。槽位事实每次直接来自 GameCore 存档容器，
    /// 保存、读取和结束单局全部交给同一个剧本导演。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScenarioSavePanel : UIPanel
    {
        [Header("面板组件")]
        [SerializeField, Tooltip("显示当前窗口是保存还是读取单局。")]
        private TMP_Text m_titleLabel;

        [SerializeField, Tooltip("承载按槽位编号排序的已有存档视图。")]
        private RectTransform m_slotRoot;

        [SerializeField, Tooltip("按已有存档动态复制的槽位视图模板。")]
        private ScenarioSaveSlotView m_slotTemplate;

        [SerializeField, Tooltip("没有任何存档时显示的空状态。")]
        private GameObject m_emptyState;

        [SerializeField, Tooltip("把当前单局写入第一个空闲槽位。")]
        private Button m_createSaveButton;

        [SerializeField, Tooltip("删除全部现有存档。")]
        private Button m_clearAllButton;

        [SerializeField, Tooltip("保存当前单局后结束本局并返回进入单局前的场景。")]
        private Button m_saveAndExitButton;

        [SerializeField, Tooltip("关闭存档窗口。")]
        private Button m_closeButton;

        private readonly List<ScenarioSaveSlotView> m_spawnedSlots = new();
        private ScenarioDirector m_director;
        private ScenarioSavePanelMode m_mode;
        private bool m_operationInProgress;

        public int DisplayedSlotCount => m_spawnedSlots.Count;

        protected override void OnInit(IUIData data = null)
        {
            if (m_titleLabel == null || m_slotRoot == null || m_slotTemplate == null ||
                m_emptyState == null || m_createSaveButton == null || m_clearAllButton == null ||
                m_saveAndExitButton == null || m_closeButton == null)
            {
                throw new InvalidOperationException("剧本存档窗口预制体缺少必要 UI 引用。");
            }

            m_createSaveButton.onClick.AddListener(CreateSave);
            m_clearAllButton.onClick.AddListener(ConfirmClearAll);
            m_saveAndExitButton.onClick.AddListener(SaveAndExit);
            m_closeButton.onClick.AddListener(CloseSelf);
        }

        protected override void OnOpen(IUIData data = null)
        {
            if (m_director != null)
            {
                throw new InvalidOperationException("剧本存档窗口尚未关闭，不能覆盖上一请求。");
            }
            if (data is not ScenarioSavePanelData panelData)
            {
                throw new ArgumentException(
                    "剧本存档窗口必须使用 ScenarioSavePanelData 打开。",
                    nameof(data));
            }

            m_director = panelData.Director;
            m_mode = panelData.Mode;
            m_titleLabel.text = m_mode == ScenarioSavePanelMode.Save ? "保存单局" : "读取单局";
            m_createSaveButton.gameObject.SetActive(m_mode == ScenarioSavePanelMode.Save);
            m_saveAndExitButton.gameObject.SetActive(m_mode == ScenarioSavePanelMode.Save);
            RefreshSlots();
        }

        protected override void OnClose()
        {
            ClearSpawnedSlots();
            m_director = null;
            m_operationInProgress = false;
        }

        protected override void ClearUIComponents()
        {
            m_createSaveButton?.onClick.RemoveListener(CreateSave);
            m_clearAllButton?.onClick.RemoveListener(ConfirmClearAll);
            m_saveAndExitButton?.onClick.RemoveListener(SaveAndExit);
            m_closeButton?.onClick.RemoveListener(CloseSelf);
            ClearSpawnedSlots();
            m_director = null;
        }

        public void RefreshSlots()
        {
            ClearSpawnedSlots();
            IReadOnlyList<SaveMeta> metadata = SaveSystem.GetAllSaveMetadata();
            for (int i = 0; i < metadata.Count; i++)
            {
                ScenarioSaveSlotView slot = Instantiate(m_slotTemplate, m_slotRoot);
                slot.gameObject.SetActive(true);
                slot.Bind(metadata[i], m_mode, HandlePrimary, ConfirmDelete);
                m_spawnedSlots.Add(slot);
            }

            bool hasSaves = metadata.Count > 0;
            m_emptyState.SetActive(!hasSaves);
            m_clearAllButton.interactable = hasSaves;
            m_createSaveButton.interactable =
                m_mode == ScenarioSavePanelMode.Save && TryFindFirstEmptySlot(out _);
            m_saveAndExitButton.interactable = m_createSaveButton.interactable;
        }

        private void HandlePrimary(int slotId)
        {
            RequireIdleOperation();
            if (m_mode == ScenarioSavePanelMode.Save)
            {
                SaveToSlot(slotId);
                return;
            }

            RunOperation(LoadSlotAsync(slotId));
        }

        private void CreateSave()
        {
            RequireIdleOperation();
            if (!TryFindFirstEmptySlot(out int slotId))
            {
                throw new InvalidOperationException("所有存档槽位都已占用，不能新建存档。");
            }

            SaveToSlot(slotId);
        }

        private void SaveAndExit()
        {
            RequireIdleOperation();
            if (!TryFindFirstEmptySlot(out int slotId))
            {
                throw new InvalidOperationException("所有存档槽位都已占用，不能保存并退出。");
            }

            RunOperation(SaveAndExitAsync(slotId));
        }

        private void ConfirmDelete(int slotId)
        {
            RequireIdleOperation();
            DialogConfig config = DialogConfig.Confirm(
                $"确定删除槽位 {slotId + 1:D2} 吗？删除后无法恢复。",
                "删除存档");
            config.OKText = "删除";
            UIKit.ShowDialog<ConfirmationDialogPanel>(
                config,
                result =>
                {
                    if (!result.IsConfirmed)
                    {
                        return;
                    }
                    if (!SaveSystem.DeleteSaveData(slotId))
                    {
                        throw new InvalidOperationException($"存档槽位 {slotId} 删除失败。");
                    }
                    RefreshSlots();
                });
        }

        private void ConfirmClearAll()
        {
            RequireIdleOperation();
            DialogConfig config = DialogConfig.Confirm(
                "确定删除全部存档吗？所有单局记录都会永久消失。",
                "清空存档");
            config.OKText = "全部删除";
            UIKit.ShowDialog<ConfirmationDialogPanel>(
                config,
                result =>
                {
                    if (!result.IsConfirmed)
                    {
                        return;
                    }
                    SaveSystem.DeleteAllSaveData();
                    RefreshSlots();
                });
        }

        private void SaveToSlot(int slotId)
        {
            RequireActiveDirector();
            if (!m_director.SaveActiveRunToSlot(slotId))
            {
                throw new InvalidOperationException($"当前单局写入槽位 {slotId} 失败。");
            }
            RefreshSlots();
        }

        private async UniTask LoadSlotAsync(int slotId)
        {
            await RequireDirector().LoadRunFromSlotAsync(slotId);
            CloseSelf();
        }

        private async UniTask SaveAndExitAsync(int slotId)
        {
            ScenarioDirector director = RequireActiveDirector();
            if (!director.SaveActiveRunToSlot(slotId))
            {
                throw new InvalidOperationException($"当前单局写入槽位 {slotId} 失败。");
            }
            await director.EndScenarioAsync();
            CloseSelf();
        }

        private void RunOperation(UniTask operation)
        {
            m_operationInProgress = true;
            SetControlsInteractable(false);
            CompleteOperationAsync(operation).Forget(Debug.LogException);
        }

        private async UniTask CompleteOperationAsync(UniTask operation)
        {
            try
            {
                await operation;
            }
            finally
            {
                m_operationInProgress = false;
                if (m_director != null)
                {
                    SetControlsInteractable(true);
                }
            }
        }

        private bool TryFindFirstEmptySlot(out int slotId)
        {
            IReadOnlyList<SaveMeta> metadata = SaveSystem.GetAllSaveMetadata();
            int metadataIndex = 0;
            int maximumSlots = SaveSystem.GetMaximumSaveSlots();
            for (int candidate = 0; candidate < maximumSlots; candidate++)
            {
                while (metadataIndex < metadata.Count && metadata[metadataIndex].SlotId < candidate)
                {
                    metadataIndex++;
                }
                if (metadataIndex >= metadata.Count || metadata[metadataIndex].SlotId != candidate)
                {
                    slotId = candidate;
                    return true;
                }
            }

            slotId = -1;
            return false;
        }

        private void SetControlsInteractable(bool interactable)
        {
            m_createSaveButton.interactable = interactable;
            m_clearAllButton.interactable = interactable && m_spawnedSlots.Count > 0;
            m_saveAndExitButton.interactable = interactable;
            m_closeButton.interactable = interactable;
        }

        private void RequireIdleOperation()
        {
            if (m_operationInProgress)
            {
                throw new InvalidOperationException("剧本存档窗口已有操作正在执行，不能同时发起第二个操作。");
            }
        }

        private ScenarioDirector RequireActiveDirector()
        {
            ScenarioDirector director = RequireDirector();
            if (!director.HasActiveScenario)
            {
                throw new InvalidOperationException("当前没有活动剧本，不能保存单局。");
            }
            return director;
        }

        private ScenarioDirector RequireDirector()
        {
            return m_director ??
                throw new InvalidOperationException("剧本存档窗口没有可用的剧本导演。");
        }

        private void ClearSpawnedSlots()
        {
            for (int i = 0; i < m_spawnedSlots.Count; i++)
            {
                if (m_spawnedSlots[i] != null)
                {
                    Destroy(m_spawnedSlots[i].gameObject);
                }
            }
            m_spawnedSlots.Clear();
        }
    }
}
