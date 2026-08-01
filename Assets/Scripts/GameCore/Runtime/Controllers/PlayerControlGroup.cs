using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 本地玩家控制组输入目标。
    /// 当前正式拥有“成员列表 + 主控成员”这两层真相；它只负责把同一份本地玩家命令分发给已可控成员，
    /// 当前只收口最小的可排队移动订单，不承担完整框选、阵型或网络 ownership。
    /// </summary>
    public sealed class PlayerControlGroup : IPlayerInputTarget
    {
        private readonly List<CharacterBase> m_members = new();
        private readonly Queue<PlayerOrderRequest> m_pendingOrders = new();
        private CharacterBase m_primaryMember = null;
        private PlayerOrderRequest? m_activeQueuedOrder = null;

        public PlayerControlGroup()
        {
        }

        public PlayerControlGroup(params CharacterBase[] members)
        {
            ReplaceMembers(members);
        }

        public PlayerControlGroup(CharacterBase primaryMember, params CharacterBase[] members)
        {
            ReplaceMembers(primaryMember, members);
        }

        public CharacterBase PrimaryMember => GetPrimaryControlledCharacter();

        public int MemberCount => m_members.Count;
        public int PendingOrderCount => m_pendingOrders.Count;

        public void ReplaceMembers(params CharacterBase[] members)
        {
            ReplaceMembers(m_primaryMember, members);
        }

        public void ReplaceMembers(CharacterBase primaryMember, params CharacterBase[] members)
        {
            m_members.Clear();
            m_primaryMember = null;
            m_pendingOrders.Clear();
            m_activeQueuedOrder = null;

            if (members == null || members.Length == 0)
            {
                return;
            }

            foreach (CharacterBase member in members)
            {
                if (TryResolveControllableMemberInputTarget(member, out _))
                {
                    TryAddMember(member);
                }
            }

            SetPrimaryMember(primaryMember ?? m_members[0]);
        }

        public bool TryAddMember(CharacterBase member, bool makePrimary = false)
        {
            if (!TryResolveControllableMemberInputTarget(member, out _))
            {
                return false;
            }

            foreach (CharacterBase existingMember in m_members)
            {
                if (existingMember == member)
                {
                    if (makePrimary)
                    {
                        SetPrimaryMember(member);
                    }

                    return false;
                }
            }

            m_members.Add(member);
            if (m_primaryMember == null || makePrimary)
            {
                m_primaryMember = member;
            }

            return true;
        }

        public bool RemoveMember(CharacterBase member)
        {
            if (!member)
            {
                return false;
            }

            bool removed = m_members.Remove(member);
            if (!removed)
            {
                return false;
            }

            if (m_primaryMember == member)
            {
                m_primaryMember = null;
                EnsurePrimaryMember();
            }

            return true;
        }

        public bool ContainsMember(CharacterBase member)
        {
            if (!member)
            {
                return false;
            }

            foreach (CharacterBase existingMember in m_members)
            {
                if (existingMember == member)
                {
                    return true;
                }
            }

            return false;
        }

        public CharacterBase GetPrimaryControlledCharacter()
        {
            EnsurePrimaryMember();
            return m_primaryMember;
        }

        public bool TrySetPrimaryMember(CharacterBase member)
        {
            if (!TryResolveControllableMemberInputTarget(member, out _))
            {
                return false;
            }

            if (!ContainsMember(member))
            {
                m_members.Add(member);
            }

            SetPrimaryMember(member);
            return true;
        }

        public bool TryGetControlledCharacter(out CharacterBase character)
        {
            character = GetPrimaryControlledCharacter();
            return character != null;
        }

        public CharacterBase[] CreateControlledCharacterSnapshot()
        {
            List<CharacterBase> snapshot = new();
            CharacterBase primaryMember = GetPrimaryControlledCharacter();
            if (TryResolveControllableMemberInputTarget(primaryMember, out _))
            {
                snapshot.Add(primaryMember);
            }

            foreach (CharacterBase member in m_members)
            {
                if (member == primaryMember)
                {
                    continue;
                }

                if (TryResolveControllableMemberInputTarget(member, out _))
                {
                    snapshot.Add(member);
                }
            }

            return snapshot.ToArray();
        }

        public PlayerControlGroupSnapshot CreateSnapshot()
        {
            CharacterBase[] members = CreateControlledCharacterSnapshot();
            return new PlayerControlGroupSnapshot(members);
        }

        public void Tick()
        {
            if (m_activeQueuedOrder.HasValue && !HasActiveQueuedMovementOrder())
            {
                m_activeQueuedOrder = null;
            }

            if (m_activeQueuedOrder.HasValue || m_pendingOrders.Count == 0)
            {
                return;
            }

            while (m_pendingOrders.Count > 0 && !m_activeQueuedOrder.HasValue)
            {
                PlayerOrderRequest queuedOrder = m_pendingOrders.Dequeue();
                PlayerOrderResult dispatchedResult = ExecuteImmediateOrder(queuedOrder);
                if (!dispatchedResult.Succeeded)
                {
                    continue;
                }

                if (queuedOrder.IsQueueableMovementOrder && HasActiveQueuedMovementOrder())
                {
                    m_activeQueuedOrder = queuedOrder;
                }
            }
        }

        public void ClearQueuedOrders()
        {
            m_pendingOrders.Clear();
            m_activeQueuedOrder = null;
        }

        public PlayerCommandResult ExecutePlayerCommand(PlayerCommandRequest request)
        {
            if (!IsActorWithinGroup(request.Actor))
            {
                return PlayerCommandResult.Failed(request, EPlayerCommandFailureReason.ActorMismatch);
            }

            PlayerOrderResult orderResult = SubmitPlayerOrder(PlayerOrderRequest.FromCommandRequest(request));
            return orderResult.LastCommandResult;
        }

        public PlayerOrderResult SubmitPlayerOrder(PlayerOrderRequest orderRequest)
        {
            if (orderRequest.IsStopOrder || orderRequest.QueueMode == EPlayerOrderQueueMode.StopCurrent)
            {
                m_pendingOrders.Clear();
                m_activeQueuedOrder = null;
                return ExecuteImmediateOrder(orderRequest);
            }

            if (orderRequest.QueueMode == EPlayerOrderQueueMode.ReplaceCurrent)
            {
                m_pendingOrders.Clear();
                m_activeQueuedOrder = null;
                return ExecuteImmediateOrder(orderRequest);
            }

            if (orderRequest.QueueMode == EPlayerOrderQueueMode.Append &&
                orderRequest.IsQueueableMovementOrder &&
                HasActiveQueuedMovementOrder())
            {
                m_pendingOrders.Enqueue(orderRequest);
                return PlayerOrderResult.Queued(orderRequest, m_pendingOrders.Count);
            }

            return ExecuteImmediateOrder(orderRequest);
        }

        private PlayerOrderResult ExecuteImmediateOrder(PlayerOrderRequest orderRequest)
        {
            PlayerOrderResult result = orderRequest.TargetScope switch
            {
                EPlayerOrderTargetScope.ControlledGroup => ExecuteForAllMembers(orderRequest),
                _ => ExecuteForPrimaryMember(orderRequest)
            };

            if (result.Succeeded && orderRequest.IsQueueableMovementOrder)
            {
                m_activeQueuedOrder = orderRequest;
            }

            return result;
        }

        private void SetPrimaryMember(CharacterBase member)
        {
            if (!TryResolveControllableMemberInputTarget(member, out _))
            {
                m_primaryMember = null;
                EnsurePrimaryMember();
                return;
            }

            m_primaryMember = member;
        }

        private void EnsurePrimaryMember()
        {
            if (TryResolveControllableMemberInputTarget(m_primaryMember, out _))
            {
                return;
            }

            foreach (CharacterBase member in m_members)
            {
                if (TryResolveControllableMemberInputTarget(member, out _))
                {
                    m_primaryMember = member;
                    return;
                }
            }

            m_primaryMember = null;
        }

        private bool IsActorWithinGroup(CharacterBase actor)
        {
            if (!actor)
            {
                return true;
            }

            foreach (CharacterBase member in CreateControlledCharacterSnapshot())
            {
                if (member == actor)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasActiveQueuedMovementOrder()
        {
            if (!m_activeQueuedOrder.HasValue)
            {
                return false;
            }

            foreach (CharacterBase member in CreateControlledCharacterSnapshot())
            {
                if (member != null && member.HasMoveOrder())
                {
                    return true;
                }
            }

            return false;
        }

        private PlayerOrderResult ExecuteForPrimaryMember(PlayerOrderRequest orderRequest)
        {
            if (!TryGetControlledCharacter(out CharacterBase primaryMember))
            {
                PlayerCommandResult failedResult = PlayerCommandResult.Failed(
                    orderRequest.CommandRequest,
                    EPlayerCommandFailureReason.InvalidControlledCharacter);
                return PlayerOrderResult.Failed(orderRequest, 0, failedResult);
            }

            PlayerCommandResult memberResult = ExecuteForMember(primaryMember, orderRequest);
            return memberResult.Succeeded
                ? PlayerOrderResult.Success(orderRequest, 1, memberResult)
                : PlayerOrderResult.Failed(orderRequest, 1, memberResult);
        }

        private PlayerOrderResult ExecuteForAllMembers(PlayerOrderRequest orderRequest)
        {
            CharacterBase[] members = CreateControlledCharacterSnapshot();
            if (members.Length == 0)
            {
                PlayerCommandResult failedResult = PlayerCommandResult.Failed(
                    orderRequest.CommandRequest,
                    EPlayerCommandFailureReason.InvalidControlledCharacter);
                return PlayerOrderResult.Failed(orderRequest, 0, failedResult);
            }

            PlayerCommandResult lastResult = PlayerCommandResult.Failed(
                orderRequest.CommandRequest,
                EPlayerCommandFailureReason.InvalidControlledCharacter);

            bool anySucceeded = false;
            int dispatchedMemberCount = 0;
            for (int memberIndex = 0; memberIndex < members.Length; memberIndex++)
            {
                CharacterBase member = members[memberIndex];
                Vector2? memberWorldPosition = ResolveMemberWorldPosition(orderRequest, memberIndex, members.Length);
                PlayerCommandResult memberResult = ExecuteForMember(member, orderRequest, memberWorldPosition);
                dispatchedMemberCount++;
                if (memberResult.Succeeded)
                {
                    anySucceeded = true;
                    lastResult = memberResult;
                    continue;
                }

                lastResult = memberResult;
            }

            if (anySucceeded)
            {
                return PlayerOrderResult.Success(orderRequest, dispatchedMemberCount, lastResult);
            }

            return PlayerOrderResult.Failed(orderRequest, dispatchedMemberCount, lastResult);
        }

        private static PlayerCommandResult ExecuteForMember(
            CharacterBase member,
            PlayerOrderRequest orderRequest,
            Vector2? overriddenWorldPosition = null)
        {
            if (!TryResolveControllableMemberInputTarget(member, out IPlayerInputTarget inputTarget))
            {
                return PlayerCommandResult.Failed(orderRequest.CommandRequest, EPlayerCommandFailureReason.InvalidControlledCharacter);
            }

            PlayerCommandRequest memberRequest = new(
                GameCommandContext.Recreate(
                    orderRequest.CommandContext.IssuerKind,
                    member,
                    orderRequest.CommandContext.IssuerId),
                orderRequest.Kind,
                direction: orderRequest.Direction,
                worldPosition: overriddenWorldPosition ?? orderRequest.WorldPosition,
                abilityIndex: orderRequest.AbilityIndex,
                targetCharacter: orderRequest.TargetCharacter,
                interactionTarget: orderRequest.InteractionTarget);

            PlayerOrderRequest memberOrderRequest = new(
                memberRequest,
                EPlayerOrderTargetScope.PrimaryMemberOnly,
                orderRequest.QueueMode);
            return inputTarget.SubmitPlayerOrder(memberOrderRequest).LastCommandResult;
        }

        private static bool TryResolveControllableMemberInputTarget(CharacterBase member, out IPlayerInputTarget inputTarget)
        {
            inputTarget = null;
            return member != null &&
                member.TryResolvePlayerInputTarget(out inputTarget);
        }

        private static Vector2? ResolveMemberWorldPosition(
            PlayerOrderRequest orderRequest,
            int memberIndex,
            int memberCount)
        {
            if (!orderRequest.HasWorldPosition)
            {
                return null;
            }

            Vector2 anchor = orderRequest.WorldPosition ?? Vector2.zero;
            if (!orderRequest.UsesDistributedWorldPositions || memberCount <= 1)
            {
                return anchor;
            }

            return orderRequest.SpatialContract.Policy switch
            {
                EPlayerOrderSpatialPolicy.DistributedRing => ResolveDistributedRingPosition(
                    anchor,
                    orderRequest.SpatialContract.Spacing,
                    memberIndex,
                    memberCount),
                _ => anchor
            };
        }

        /// <summary>
        /// 当前最小正式编队落点：主点周围按环形扩散。
        /// 这不是最终大队形系统，但它把“批量移动不能全塞同一点”的真相收在运行时里，而不是散在 UI 偏移魔法数里。
        /// </summary>
        private static Vector2 ResolveDistributedRingPosition(
            Vector2 anchor,
            float spacing,
            int memberIndex,
            int memberCount)
        {
            if (memberCount <= 1 || memberIndex <= 0)
            {
                return anchor;
            }

            int remainingIndex = memberIndex - 1;
            int ring = 1;
            int slotsInRing = 6;
            while (remainingIndex >= slotsInRing)
            {
                remainingIndex -= slotsInRing;
                ring++;
                slotsInRing = math.max(6, ring * 6);
            }

            float angle = (math.PI * 2f * remainingIndex) / slotsInRing;
            float radius = spacing * ring;
            Vector2 offset = new(math.cos(angle), math.sin(angle));
            return anchor + offset * radius;
        }
    }
}
