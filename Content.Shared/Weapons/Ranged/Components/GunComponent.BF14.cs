namespace Content.Shared.Weapons.Ranged.Components;

public sealed partial class GunComponent
{
    /// <summary>
    ///     Multiplies the damage of every projectile this gun fires, as Monolith's did. BF14's snipers and heavy
    ///         revolvers fire the same rounds as other guns and hit harder through this.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DamageModifier = 1f;
}
