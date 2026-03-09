// SPDX-FileCopyrightText: 2025 Ilya246
//
// SPDX-License-Identifier: MPL-2.0

using Robust.Shared.Map.Components;
using System;

namespace Content.Shared._Mono.Detection;

/// <summary>
///     Handles the logic for grid and entity detection.
/// </summary>
public sealed class DetectionSystem : EntitySystem
{
    public DetectionLevel IsGridDetected(Entity<MapGridComponent?> grid, EntityUid byUid)
    {
        if (!Resolve(grid, ref grid.Comp))
            return DetectionLevel.Undetected;

        var comp = EnsureComp<DetectionRangeMultiplierComponent>(byUid);

        if (comp.AlwaysDetect)
            return DetectionLevel.Detected;

        var gridAABB = grid.Comp.LocalAABB;
        var gridDiagonal = MathF.Sqrt(gridAABB.Width * gridAABB.Width + gridAABB.Height * gridAABB.Height);
        var visualSig = gridDiagonal;
        var visualRadius = visualSig * comp.VisualMultiplier;

        var thermalSig = TryComp<ThermalSignatureComponent>(grid, out var sigComp) ? MathF.Max(sigComp.TotalHeat, 0f) : 0f;
        var thermalRadius = MathF.Sqrt(thermalSig) * comp.InfraredMultiplier;

        if (TryComp<DetectedAtRangeMultiplierComponent>(grid, out var compAt))
        {
            visualRadius *= compAt.VisualMultiplier;
            thermalRadius *= compAt.InfraredMultiplier;
            visualRadius += compAt.VisualBias;
        }

        var outlineRadius = thermalRadius * comp.InfraredOutlinePortion;
        outlineRadius = MathF.Max(outlineRadius, visualRadius);

        var level = DetectionLevel.Undetected;

        var xform = Transform(grid);
        var byXform = Transform(byUid);
        if (xform.Coordinates.TryDistance(EntityManager, byXform.Coordinates, out var distance))
        {
            if (distance <= outlineRadius) // accounts for visual radius
                level = DetectionLevel.Detected;
            else if (distance < thermalRadius)
                level = DetectionLevel.PartialDetected;
        }

        // maybe make this also take IFF being on into account?
        return level;
    }

    public DetectionLevel IsGridDetected(Entity<MapGridComponent?> grid, IEnumerable<EntityUid> byUids)
    {
        var bestLevel = DetectionLevel.Undetected;
        foreach (var uid in byUids)
        {
            var level = IsGridDetected(grid, uid);
            if (level == DetectionLevel.Detected)
                return level;

            if ((int)level < (int)bestLevel)
                bestLevel = level;
        }
        return bestLevel;
    }
}

public enum DetectionLevel : int
{
    Detected = 0,
    PartialDetected = 1,
    Undetected = 2
}
