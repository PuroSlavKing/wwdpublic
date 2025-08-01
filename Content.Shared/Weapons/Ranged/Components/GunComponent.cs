using System.Numerics;
using Content.Shared._White.Weapons.Ranged.DualWield;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Weapons.Ranged.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class GunComponent : Component
{
    #region Sound

    /// <summary>
    /// The base sound to use when the gun is fired.
    /// </summary>
    [DataField]
    public SoundSpecifier? SoundGunshot = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/smg.ogg");

    /// <summary>
    /// The sound to use when the gun is fired.
    /// <seealso cref="GunRefreshModifiersEvent"/>
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier? SoundGunshotModified;

    [DataField]
    public SoundSpecifier? SoundEmpty = new SoundPathSpecifier("/Audio/Weapons/Guns/Empty/empty.ogg");

    /// <summary>
    /// Sound played when toggling the <see cref="SelectedMode"/> for this gun.
    /// </summary>
    [DataField]
    public SoundSpecifier? SoundMode = new SoundPathSpecifier("/Audio/Weapons/Guns/Misc/selector.ogg");

    #endregion

    #region Recoil

    // These values are very small for now until we get a debug overlay and fine tune it

    /// <summary>
    /// The base scalar value applied to the vector governing camera recoil.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CameraRecoilScalar = 1f;

    /// <summary>
    /// A scalar value applied to the vector governing camera recoil.
    /// If 0, there will be no camera recoil.
    /// <seealso cref="GunRefreshModifiersEvent"/>
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float CameraRecoilScalarModified = 1f;

    /// <summary>
    /// Last time the gun fired.
    /// Used for recoil purposes.
    /// WWDP rename: formely "LastFire".
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public TimeSpan CurrentAngleLastUpdate = TimeSpan.Zero;

    /// <summary>
    /// What the current spread is for shooting. This gets changed every time the gun fires.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public Angle CurrentAngle;

    // WWDP EDIT START
    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public Angle BonusAngle;

    [AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan BonusAngleLastUpdate;

    [DataField]
    [AutoNetworkedField]
    public Angle BonusAngleDecay = Angle.FromDegrees(40);

    [DataField]
    [AutoNetworkedField]
    public Angle MaxBonusAngle = Angle.FromDegrees(30);

    [ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public Angle BonusAngleDecayModified;

    [ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public Angle MaxBonusAngleModified;

    /// <summary>
    /// Bonus spread angle increase per tile traversed (assuming zero travel time)
    /// </summary>
    [DataField, AutoNetworkedField]
    public Angle BonusAngleIncreaseMove = Angle.FromDegrees(20);

    /// <summary>
    /// Bonus spread angle increase per angle turned (assuming instantaneous turn)
    /// </summary>
    [DataField, AutoNetworkedField]
    public Angle BonusAngleIncreaseTurn = Angle.FromDegrees(0.25);
    // WWDP EDIT END

    /// <summary>
    /// The base value for how much the spread increases every time the gun fires.
    /// </summary>
    [DataField]
    public Angle AngleIncrease = Angle.FromDegrees(0.5);

    /// <summary>
    /// How much the spread increases every time the gun fires.
    /// <seealso cref="GunRefreshModifiersEvent"/>
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public Angle AngleIncreaseModified;

    /// <summary>
    /// The base value for how much the <see cref="CurrentAngle"/> decreases per second.
    /// </summary>
    [DataField]
    public Angle AngleDecay = Angle.FromDegrees(4);

    /// <summary>
    /// How much the <see cref="CurrentAngle"/> decreases per second.
    /// <seealso cref="GunRefreshModifiersEvent"/>
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public Angle AngleDecayModified;

    /// <summary>
    /// The base value for the maximum angle allowed for <see cref="CurrentAngle"/>
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public Angle MaxAngle = Angle.FromDegrees(2);

    /// <summary>
    /// The maximum angle allowed for <see cref="CurrentAngle"/>
    /// <seealso cref="GunRefreshModifiersEvent"/>
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public Angle MaxAngleModified;

    /// <summary>
    /// The base value for the minimum angle allowed for <see cref="CurrentAngle"/>
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public Angle MinAngle = Angle.FromDegrees(1);

    /// <summary>
    ///  The minimum angle allowed for <see cref="CurrentAngle"/>.
    /// <seealso cref="GunRefreshModifiersEvent"/>
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public Angle MinAngleModified;

    #endregion

    /// <summary>
    /// Whether this gun is shot via the use key or the alt-use key.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool UseKey = true;

    /// <summary>
    /// Where the gun is being requested to shoot.
    /// </summary>
    [ViewVariables]
    public EntityCoordinates? ShootCoordinates = null;

    /// <summary>
    /// Who the gun is being requested to shoot at directly.
    /// </summary>
    [ViewVariables]
    public EntityUid? Target = null;

    /// <summary>
    ///     The base value for how many shots to fire per burst.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int ShotsPerBurst = 3;

    /// <summary>
    ///     How many shots to fire per burst.
    /// <seealso cref="GunRefreshModifiersEvent"/>
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int ShotsPerBurstModified = 3;

    /// <summary>
    /// How long time must pass between burstfire shots.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BurstCooldown = 0.25f;

    /// <summary>
    /// The fire rate of the weapon in burst fire mode.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BurstFireRate = 8f;

    /// <summary>
    /// Whether the burst fire mode has been activated.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public bool BurstActivated = false;

    /// <summary>
    /// The burst fire bullet count.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int BurstShotsCount = 0;

    /// <summary>
    /// Used for tracking semi-auto / burst
    /// </summary>
    [ViewVariables]
    [AutoNetworkedField]
    public int ShotCounter = 0;

    /// <summary>
    /// The base value for how many times it shoots per second.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float FireRate = 8f;

    /// <summary>
    /// How many times it shoots per second.
    /// <seealso cref="GunRefreshModifiersEvent"/>
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float FireRateModified = 1;

    /// <summary>
    /// Starts fire cooldown when equipped if true.
    /// </summary>
    [DataField]
    public bool ResetOnHandSelected = true;

    /// <summary>
    /// The base value for how fast the projectile moves.
    /// </summary>
    [DataField]
    public float ProjectileSpeed = 25f;

    /// <summary>
    /// How fast the projectile moves.
    /// <seealso cref="GunRefreshModifiersEvent"/>
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float ProjectileSpeedModified;

    /// <summary>
    /// When the gun is next available to be shot.
    /// Can be set multiple times in a single tick due to guns firing faster than a single tick time.
    /// </summary>
    [DataField(customTypeSerializer:typeof(TimeOffsetSerializer))]
    [AutoNetworkedField]
    [AutoPausedField]
    public TimeSpan NextFire = TimeSpan.Zero;

    /// <summary>
    ///   After dealing a melee attack with this gun, the minimum cooldown in seconds before the gun can shoot again.
    /// </summary>
    [DataField]
    public float MeleeCooldown = 0.528f;

    /// <summary>
    /// What firemodes can be selected.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public SelectiveFire AvailableModes = SelectiveFire.SemiAuto;

    /// <summary>
    /// What firemode is currently selected.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public SelectiveFire SelectedMode = SelectiveFire.SemiAuto;

    /// <summary>
    /// Whether or not information about
    /// the gun will be shown on examine.
    /// </summary>
    [DataField]
    public bool ShowExamineText = true;

    /// <summary>
    /// Whether or not someone with the
    /// clumsy trait can shoot this
    /// </summary>
    [DataField]
    public bool ClumsyProof = false;

    /// <summary>
    /// Firing direction for an item not being held (e.g. shuttle cannons, thrown guns still firing).
    /// </summary>
    [DataField]
    public Vector2 DefaultDirection = new Vector2(0, -1);

    /// <summary>
    ///     The percentage chance of a given gun to accidentally discharge if violently thrown into a wall or person
    /// </summary>
    [DataField]
    public float FireOnDropChance = 0.1f;

    /// <summary>
    ///     If this weapon is using any kind of "Shotgun-like" ammunition, this applies as a multiplier on the spread arc.
    //      EG: 1.5 with standard buckshot gives a shotgun arc of 22.5 degrees.
    /// </summary>
    [DataField]
    public float ShotgunSpreadMultiplier = 1f;

    /// <summary>
    ///     This multiplier will apply per projectile fired by the weapon.
    /// </summary>
    [DataField]
    public float DamageModifier = 1f;

    /// <summary>
    ///     This multiplier increases the amount of projectiles fired by a shotgun.
    /// </summary>
    [DataField]
    public float ShotgunProjectileCountModifier = 1f;

    /// <summary>
    ///     If this weapon is using any kind of "Shotgun-like" ammunition, setting this to true makes it use the
    ///     classic style of "uniform" spread. Whereas when left off, each pellet fires in a uniform arc.
    /// </summary>
    [DataField]
    public bool UniformSpread;

    /// <summary>
    ///     The amount of Force (in Newtons) to eject spent cartridges with.
    /// </summary>
    [DataField]
    public float EjectionForce = 0.04f;

    [DataField]
    public float EjectionSpeed = 20f;

    [DataField]
    public float EjectAngleOffset = 3.7f;

    // WD EDIT START
    [DataField]
    public Angle? ThrowAngle;

    /// <summary>
    /// Designates this weapon a naval cannon. All projectiles shot from this weapon will be added to PVS ignore list (see Server.PvsOverrideSystem)
    /// to ensure their visibility on clients.
    /// </summary>
    [DataField]
    public bool ShipWeapon = false;

    [DataField]
    public bool ForceShootForward = false;
    // WD EDIT END

}

[Flags]
public enum SelectiveFire : byte
{
    Invalid = 0,
    // Combat mode already functions as the equivalent of Safety
    SemiAuto = 1 << 0,
    Burst = 1 << 1,
    FullAuto = 1 << 2, // Not in the building!
}
