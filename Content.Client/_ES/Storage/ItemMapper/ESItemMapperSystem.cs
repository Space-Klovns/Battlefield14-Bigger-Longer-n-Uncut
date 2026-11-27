// BF14-BLU: ported from Battlefield14 (https://github.com/PunishedJoe/Battlefield14) at d24a6e8c2f5d995e6bdd0a2571e406923e53f883
using Content.Shared._ES.Storage.ItemMapper;
using Content.Shared._ES.Storage.ItemMapper.Components;
using Robust.Client.GameObjects;

namespace Content.Client._ES.Storage.ItemMapper;

public sealed partial class ESItemMapperSystem : ESSharedItemMapperSystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ESItemMapperComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(Entity<ESItemMapperComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
        {
            // BF14-BLU: logged, not thrown
            Log.Error($"{ToPrettyString(ent)} is missing {nameof(SpriteComponent)}!");
            return;
        }

        if (!Appearance.TryGetData(ent, ESItemMapperVisuals.Layers, out Dictionary<string, string?> layers, args.Component))
        {
            //Stopgap, throw replaced with Log.Warning and return. Prevents crash in debug mode when looking at lathe menu.
            //Someone more competent can come up with a more comprehensive fix.
            Log.Warning($"Couldn't find the necessary {nameof(ESItemMapperVisuals.Layers)} layer on {ToPrettyString(ent)}.");
            return;
        }

        foreach (var (key, state) in layers)
        {
            // BF14-BLU: a layer the sprite doesn't map is skipped, and a hidden layer keeps its last state
            if (!_sprite.LayerMapTryGet((ent, sprite), key, out var layer, false))
                continue;

            _sprite.LayerSetVisible((ent, sprite), layer, state != null);
            if (state != null)
                _sprite.LayerSetRsiState((ent, sprite), layer, state);
        }
    }
}
