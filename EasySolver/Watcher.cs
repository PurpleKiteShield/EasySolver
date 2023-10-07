﻿using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.GeneratedSheets;
using EasySolver.Basic.Configuration;
using System.Text.RegularExpressions;

namespace EasySolver;

public static class Watcher
{
#if DEBUG
    private unsafe delegate bool OnUseAction(ActionManager* manager, ActionType actionType, uint actionID, ulong targetID, uint a4, uint a5, uint a6, void* a7);
    private static Dalamud.Hooking.Hook<OnUseAction> _useActionHook;
#endif

    public static ICallGateSubscriber<object, object> IpcSubscriber;

    public static void Enable()
    {
#if DEBUG
        unsafe
        {
            _useActionHook = Svc.Hook.HookFromSignature<OnUseAction>("E8 ?? ?? ?? ?? EB 64 B1 01", UseActionDetour);
            //_useActionHook.Enable();
        }
#endif
        IpcSubscriber = Svc.PluginInterface.GetIpcSubscriber<object, object>("PingPlugin.Ipc");
        IpcSubscriber.Subscribe(UpdateRTTDetour);

        ActionEffect.ActionEffectEvent += ActionFromEnemy;
        ActionEffect.ActionEffectEvent += ActionFromSelf;
    }

    public static void Disable()
    {
#if DEBUG
        _useActionHook?.Dispose();
#endif
        IpcSubscriber.Unsubscribe(UpdateRTTDetour);
        ActionEffect.ActionEffectEvent -= ActionFromEnemy;
        ActionEffect.ActionEffectEvent -= ActionFromSelf;
    }
#if DEBUG
    private static unsafe bool UseActionDetour(ActionManager* manager, ActionType actionType, uint actionID, ulong targetID, uint a4, uint a5, uint a6, void* a7)
    {
        try
        {
            Svc.Chat.Print($"Type: {actionType}, ID: {actionID}, Tar: {targetID}, 4: {a4}, 5: {a5}, 6: {a6}");
        }
        catch(Exception e)
        {
            Svc.Log.Warning(e, "Failed to detour actions");
        }
        return _useActionHook.Original(manager, actionType, actionID, targetID, a4, a5, a6, a7);
    }
#endif

    private static void UpdateRTTDetour(dynamic obj)
    {
        Svc.Log.Debug($"LastRTT:{obj.LastRTT}");
        DataCenter.RTT = (long)obj.LastRTT / 1000f;
    }

    public static string ShowStrSelf { get; private set; } = string.Empty;
    public static string ShowStrEnemy { get; private set; } = string.Empty;

    private static void ActionFromEnemy(ActionEffectSet set)
    {
        //Check Source.
        var source = set.Source;
        if (source == null) return;
        if (source is not BattleChara battle) return;
        if (battle is PlayerCharacter) return;
        if (battle.SubKind == 9) return; //Friend!
        if (Svc.Objects.SearchById(battle.ObjectId) is PlayerCharacter) return;

        var damageRatio = set.TargetEffects
            .Where(e => e.TargetID == Player.Object.ObjectId)
            .SelectMany(e => new EffectEntry[]
            {
                e[0], e[1], e[2], e[3], e[4], e[5], e[6], e[7]
            })
            .Where(e => e.type == ActionEffectType.Damage)
            .Sum(e => (float)e.value / Player.Object.MaxHp);

        DataCenter.AddDamageRec(damageRatio);

        ShowStrEnemy = $"Damage Ratio: {damageRatio}\n{set}";


        foreach (var effect in set.TargetEffects)
        {
            if (effect.TargetID != Player.Object.ObjectId) continue;
            if(effect.GetSpecificTypeEffect(ActionEffectType.Knockback, out var entry))
            {
                var knock = Svc.Data.GetExcelSheet<Knockback>()?.GetRow(entry.value);
                if(knock != null)
                {
                    DataCenter.KnockbackStart = DateTime.Now;
                    DataCenter.KnockbackFinished = DateTime.Now + TimeSpan.FromSeconds(knock.Distance / (float)knock.Speed);
                }
                break;
            }
        }

        if (set.Header.ActionType == ActionType.Action && DataCenter.PartyMembers.Count() >= 4 && set.Action.Cast100ms > 0)
        {
            var type = set.Action.GetActionCate();

            if(type is ActionCate.Spell or ActionCate.WeaponSkill or ActionCate.Ability)
            {
                if (set.TargetEffects.Count(e =>
                    DataCenter.PartyMembers.Any(p => p.ObjectId == e.TargetID)
                    && e.GetSpecificTypeEffect(ActionEffectType.Damage, out var effect)
                    && (effect.value > 0 || (effect.param0 & 6) == 6))
                    == DataCenter.PartyMembers.Count())
                {
                    if (Service.Config.GetValue(PluginConfigBool.RecordCastingArea))
                    {
                        OtherConfiguration.HostileCastingArea.Add(set.Action.RowId);
                        OtherConfiguration.SaveHostileCastingArea();
                    }
                }
            }
        }
    }

    private static void ActionFromSelf(ActionEffectSet set)
    {
        if (set.Source.ObjectId != Player.Object.ObjectId) return;
        if (set.Header.ActionType != ActionType.Action && set.Header.ActionType != ActionType.Item) return;
        if (set.Action == null) return;
        if ((ActionCate)set.Action.ActionCategory.Value.RowId == ActionCate.AutoAttack) return;

        var id = set.Action.RowId;
        if(!set.Action.IsRealGCD() && (set.Action.ClassJob.Row > 0 || Enum.IsDefined((ActionID)id)))
        {
            OtherConfiguration.AnimationLockTime[id] = set.Header.AnimationLockTime;
        }

        if (!set.TargetEffects.Any()) return;

        var action = set.Action;
        var tar = set.Target;

        if (tar == null || action == null) return;

        //Record
        DataCenter.AddActionRec(action);
        ShowStrSelf = set.ToString();

        DataCenter.HealHP = set.GetSpecificTypeEffect(ActionEffectType.Heal);
        DataCenter.ApplyStatus = set.GetSpecificTypeEffect(ActionEffectType.ApplyStatusEffectTarget);
        foreach ( var effect in set.GetSpecificTypeEffect(ActionEffectType.ApplyStatusEffectSource))
        {
            DataCenter.ApplyStatus[effect.Key] = effect.Value;
        }
        DataCenter.MPGain = (uint)set.GetSpecificTypeEffect(ActionEffectType.MpGain).Where(i => i.Key == Player.Object.ObjectId).Sum(i => i.Value);
        DataCenter.EffectTime = DateTime.Now;
        DataCenter.EffectEndTime = DateTime.Now.AddSeconds(set.Header.AnimationLockTime + 1);

        foreach (var effect in set.TargetEffects)
        {
            if (!effect.GetSpecificTypeEffect(ActionEffectType.Damage, out _)) continue;

            if (DataCenter.AttackedTargets.Any(i => i.id == effect.TargetID)) continue;

            if(DataCenter.AttackedTargets.Count >= DataCenter.ATTACKED_TARGETS_COUNT)
            {
                DataCenter.AttackedTargets.Dequeue();
            }
            DataCenter.AttackedTargets.Enqueue((effect.TargetID, DateTime.Now));
        }

        //Macro
        foreach (var item in Service.Config.GlobalConfig.Events)
        {
            if (!new Regex(item.Name).Match(action.Name).Success) continue;
            if (item.AddMacro(tar)) break;
        }
    }
}
