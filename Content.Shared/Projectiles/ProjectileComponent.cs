using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Projectiles;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ProjectileComponent : Component
{
    /// <summary>
    ///     The angle of the fired projectile.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Angle Angle;

    /// <summary>
    ///     The effect that appears when a projectile collides with an entity.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntProtoId? ImpactEffect;

    /// <summary>
    ///     User that shot this projectile.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Shooter;

    /// <summary>
    ///     Weapon used to shoot.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Weapon;

    /// <summary>
    ///     The projectile spawns inside the shooter most of the time, this prevents entities from shooting themselves.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IgnoreShooter = true;

    /// <summary>
    ///     The amount of damage the projectile will do.
    /// </summary>
    [DataField(required: true)] [ViewVariables(VVAccess.ReadWrite)]
    public DamageSpecifier Damage = new();

    /// <summary>
    ///     If the projectile should be deleted on collision.
    /// </summary>
    [DataField]
    public bool DeleteOnCollide = true;

    /// <summary>
    /// WWDP - If the projectile should change its Physics BodyStatus to OnGround on collision.
    /// </summary>
    [DataField]
    public bool StopFlyingOnImpact;

    /// <summary>
    ///     Ignore all damage resistances the target has.
    /// </summary>
    [DataField]
    public bool IgnoreResistances = false;

    /// <summary>
    ///     Get that juicy FPS hit sound.
    /// </summary>
    [DataField]
    public SoundSpecifier? SoundHit;

    /// <summary>
    ///     Force the projectiles sound to play rather than potentially playing the entity's sound.
    /// </summary>
    [DataField]
    public bool ForceSound = false;

    /// <summary>
    ///     Whether this projectile will only collide with entities if it was shot from a gun (if <see cref="Weapon"/> is not null).
    /// </summary>
    [DataField]
    public bool OnlyCollideWhenShot = false;

    /// <summary>
    ///     Whether this projectile has already damaged an entity.
    /// </summary>
    [DataField]
    public bool DamagedEntity;

    // Goobstation start
    [DataField]
    public bool Penetrate;

    /// <summary>
    ///     Collision mask of what not to penetrate if <see cref="Penetrate"/> is true.
    /// </summary>
    [DataField(customTypeSerializer: typeof(FlagSerializer<CollisionMask>))]
    public int NoPenetrateMask = 0;

    [NonSerialized]
    public List<EntityUid> IgnoredEntities = new();
    // Goobstation end
}
