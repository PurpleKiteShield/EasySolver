﻿using Dalamud.Interface.Utility;
using EasySolver.Localization;
using EasySolver.UI;

namespace EasySolver.ActionSequencer;

internal abstract class BaseCondition : ICondition
{
    public float DelayMin = 0;
    public float DelayMax = 0;

    RandomDelay _delay = default;

    [JsonIgnore]
    private const float MIN = 0, MAX = 60;

    public bool IsTrue(ICustomRotation rotation)
    {
        if(_delay.GetRange == null)
        {
            _delay = new(() => (DelayMin, DelayMax));
        }
        return _delay.Delay(IsTrueInside(rotation));
    }

    public abstract bool IsTrueInside(ICustomRotation rotation);

    public void Draw(ICustomRotation rotation)
    {
        BeforeDraw();
        ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
        if(ImGui.DragFloatRange2($"##Random Delay {GetHashCode()}", ref DelayMin, ref DelayMax, 0.1f, MIN, MAX))
        {
            DelayMin = Math.Max(Math.Min(DelayMin, DelayMax), MIN);
            DelayMax = Math.Min(Math.Max(DelayMin, DelayMax), MAX);
        }
        ImguiTooltips.HoveredTooltip(LocalizationManager.RightLang.ActionSequencer_Delay_Description);

        ImGui.SameLine();
        DrawInside(rotation);
    }

    public virtual void BeforeDraw()
    {

    }
    public abstract void DrawInside(ICustomRotation rotation);
}
