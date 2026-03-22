using Content.Client.Atmos.Components;
using Robust.Client.GameObjects;
using Content.Shared.Atmos.Piping;

namespace Content.Client.Atmos.EntitySystems;

public sealed class PipeColorVisualizerSystem : VisualizerSystem<PipeColorVisualsComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, PipeColorVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null
            || !AppearanceSystem.TryGetData<Color>(uid, PipeColorVisuals.Color, out var color, args.Component)
            || !args.Sprite.LayerMapTryGet(PipeVisualLayers.Pipe, out var layer)
            || !args.Sprite.TryGetLayer(layer, out var spriteLayer))
        {
            return;
        }

        // T-ray scanner / sub floor runs after this visualizer. Lets not bulldoze transparency.
        args.Sprite.LayerSetColor(layer, color.WithAlpha(spriteLayer.Color.A));
    }
}

public enum PipeVisualLayers : byte
{
    Pipe,
}
