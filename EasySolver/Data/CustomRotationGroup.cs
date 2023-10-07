using ECommons.ExcelServices;

namespace EasySolver.Data
{
    internal record CustomRotationGroup(Job JobId, Job[] ClassJobIds, ICustomRotation[] Rotations);
}
