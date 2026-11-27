using Content.Shared._KS14.Weapons.Ranged;
using Content.Shared.Examine;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared._KS14.GunDamageMultiplier;

public sealed partial class KsGunDamageMultiplierSystem : EntitySystem
{
    [Dependency] private EntityQuery<ProjectileComponent> _projectileQuery = default!;
    [Dependency] private EntityQuery<GunComponent> _gunQuery = default!;

    private const string ExamineColor = "yellow";

    // Not GunShotEvent: its ammo is the cartridges, and the bullets they spawn are separate entities.
    [SubscribeLocalEvent]
    private void OnFiredProjectiles(Entity<KsGunDamageMultiplierComponent> entity, ref KsGunFiredProjectilesEvent args)
    {
        foreach (var projectileUid in args.ProjectileUids)
        {
            if (!_projectileQuery.TryComp(projectileUid, out var projectileComponent))
                continue;

            projectileComponent.Damage *= entity.Comp.Multiplier;
        }
    }

    [SubscribeLocalEvent]
    private void OnExamined(Entity<KsGunDamageMultiplierComponent> entity, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || entity.Comp.Multiplier == 1f)
            return;

        if (_gunQuery.TryComp(entity, out var gunComponent) && !gunComponent.ShowExamineText)
            return;

        // The gun's own group, so the line sits with its fire mode and fire rate.
        using (args.PushGroup(nameof(GunComponent)))
        {
            args.PushMarkup(Loc.GetString("ks-gun-damage-multiplier-examine",
                ("color", ExamineColor),
                ("multiplier", $"{entity.Comp.Multiplier:0.##}")));
        }
    }
}
