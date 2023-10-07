﻿using Dalamud.Utility;
using EasySolver.Localization;
using EasySolver.UI;

namespace EasySolver.ActionSequencer;

internal class ActionCondition : BaseCondition
{
    private IBaseAction _action;

    public ActionID ID { get; set; } = ActionID.None;

    public ActionConditionType ActionConditionType = ActionConditionType.Elapsed;

    public bool Condition { get; set; }

    public int Param1;
    public int Param2;
    public float Time;

    public override bool IsTrueInside(ICustomRotation rotation)
    {
        if (!ConditionHelper.CheckBaseAction(rotation, ID, ref _action)) return false;

        var result = false;

        switch (ActionConditionType)
        {
            case ActionConditionType.Elapsed:
                result = _action.ElapsedOneChargeAfter(Time); // Bigger
                break;

            case ActionConditionType.ElapsedGCD:
                result = _action.ElapsedOneChargeAfterGCD((uint)Param1, Param2); // Bigger
                break;

            case ActionConditionType.Remain:
                result = !_action.WillHaveOneCharge(Time); //Smaller
                break;

            case ActionConditionType.RemainGCD:
                result = !_action.WillHaveOneChargeGCD((uint)Param1, Param2); // Smaller
                break;

            case ActionConditionType.CanUse:
                result = _action.CanUse(out _, (CanUseOption)Param1, (byte)Param2);
                break;

            case ActionConditionType.EnoughLevel:
                result = _action.EnoughLevel;
                break;

            case ActionConditionType.IsCoolDown:
                result = _action.IsCoolingDown;
                break;

            case ActionConditionType.CurrentCharges:
                result = _action.CurrentCharges > Param1;
                break;

            case ActionConditionType.MaxCharges:
                result = _action.MaxCharges > Param1;
                break;
        }

        return Condition ? !result : result;
    }

    private readonly CollapsingHeaderGroup _actionsList = new()
    {
        HeaderSize = 12,
    };

    public override void DrawInside(ICustomRotation rotation)
    {
        ConditionHelper.CheckBaseAction(rotation, ID, ref _action);

        var name = _action?.Name ?? string.Empty;

        var popUpKey = "Action Condition Pop Up" + GetHashCode().ToString();

        ConditionHelper.ActionSelectorPopUp(popUpKey, _actionsList, rotation, item => ID = (ActionID)item.ID);

        if (_action?.GetTexture(out var icon) ?? false || IconSet.GetTexture(4, out icon))
        {
            var cursor = ImGui.GetCursorPos();
            if (ImGuiHelper.NoPaddingNoColorImageButton(icon.ImGuiHandle, Vector2.One * ConditionHelper.IconSize, GetHashCode().ToString()))
            {
                if(!ImGui.IsPopupOpen(popUpKey)) ImGui.OpenPopup(popUpKey);
            }
            ImGuiHelper.DrawActionOverlay(cursor, ConditionHelper.IconSize, 1);
            ImguiTooltips.HoveredTooltip(name);
        }

        ImGui.SameLine();

        ConditionHelper.DrawByteEnum($"##Category{GetHashCode()}", ref ActionConditionType, EnumTranslations.ToName);

        var condition = Condition ? 1 : 0;
        var combos = Array.Empty<string>();
        switch (ActionConditionType)
        {
            case ActionConditionType.ElapsedGCD:
            case ActionConditionType.RemainGCD:
            case ActionConditionType.Elapsed:
            case ActionConditionType.Remain:
            case ActionConditionType.CurrentCharges:
            case ActionConditionType.MaxCharges:
                combos = new string[] { ">", "<=" };
                break;

            case ActionConditionType.CanUse:
                combos = new string[]
                {
                    LocalizationManager.RightLang.ActionSequencer_Can,
                    LocalizationManager.RightLang.ActionSequencer_Cannot,
                };
                break;

            case ActionConditionType.EnoughLevel:
            case ActionConditionType.IsCoolDown:
                combos = new string[]
                {
                    LocalizationManager.RightLang.ActionSequencer_Is,
                    LocalizationManager.RightLang.ActionSequencer_Isnot,
                };
                break;
        }
        ImGui.SameLine();

        if(ImGuiHelper.SelectableCombo($"##Comparation{GetHashCode()}", combos, ref condition))
        {
            Condition = condition > 0;
        }


        switch (ActionConditionType)
        {
            case ActionConditionType.Elapsed:
            case ActionConditionType.Remain:
                ConditionHelper.DrawDragFloat($"s##Seconds{GetHashCode()}", ref Time);
                break;

            case ActionConditionType.ElapsedGCD:
            case ActionConditionType.RemainGCD:
                if (ConditionHelper.DrawDragInt($"GCD##GCD{GetHashCode()}", ref Param1))
                {
                    Param1 = Math.Max(0, Param1);
                }
                if (ConditionHelper.DrawDragInt($"{LocalizationManager.RightLang.ActionSequencer_TimeOffset}##Ability{GetHashCode()}", ref Param2))
                {
                    Param2 = Math.Max(0, Param2);
                }
                break;

            case ActionConditionType.CanUse:
                var popUpId = "Can Use Id" + GetHashCode().ToString();
                var option = (CanUseOption)Param1;

                if (ImGui.Selectable($"{option}##CanUse{GetHashCode()}"))
                {
                    if (!ImGui.IsPopupOpen(popUpId)) ImGui.OpenPopup(popUpId);
                }

                if (ImGui.BeginPopup(popUpId))
                {
                    var showedValues = Enum.GetValues<CanUseOption>().Where(i => i.GetAttribute<JsonIgnoreAttribute>() == null);

                    foreach (var value in showedValues)
                    {
                        var b = option.HasFlag(value);
                        if(ImGui.Checkbox(value.ToString(), ref b))
                        {
                            option ^= value;
                            Param1 = (int)option;
                        }
                    }

                    ImGui.EndPopup();
                }

                if (ConditionHelper.DrawDragInt($"{LocalizationManager.RightLang.ActionSequencer_AOECount}##AOECount{GetHashCode()}", ref Param2))
                {
                    Param2 = Math.Max(0, Param2);
                }
                break;

            case ActionConditionType.CurrentCharges:
            case ActionConditionType.MaxCharges:
                if (ConditionHelper.DrawDragInt($"{LocalizationManager.RightLang.ActionSequencer_Charges}##Charges{GetHashCode()}", ref Param1))
                {
                    Param1 = Math.Max(0, Param1);
                }
                break;
        }
    }

}

public enum ActionConditionType : byte
{
    Elapsed,
    ElapsedGCD,
    Remain,
    RemainGCD,
    CanUse,
    EnoughLevel,
    IsCoolDown,
    CurrentCharges,
    MaxCharges,
}
