using Content.Shared.Contests;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Weapons.Melee;

/// <summary>
/// When given to a mob lets them do unarmed attacks, or when given to an item lets someone wield it to do attacks.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), AutoGenerateComponentPause]
public sealed partial class MeleeWeaponComponent : Component
{
    // TODO: This is becoming bloated as shit.
    // This should just be its own component for alt attacks.
    /// <summary>
    /// Does this entity do a disarm on alt attack.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AltDisarm = true;

    /// <summary>
    /// Should the melee weapon's damage stats be examinable.
    /// </summary>
    [DataField]
    public bool Hidden;

    /// <summary>
    /// Next time this component is allowed to light attack. Heavy attacks are wound up and never have a cooldown.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    [AutoPausedField]
    public TimeSpan NextAttack;

    /// <summary>
    /// Starts attack cooldown when equipped if true.
    /// </summary>
    [DataField]
    public bool ResetOnHandSelected = true;

    /// <summary>
    ///   If true, swaps the keybinds for light attacks and heavy attacks.
    /// </summary>
    [DataField]
    public bool SwapKeys = false;

    /// <summary>
    ///   If true, disables heavy attacks for this weapon, and prevents the heavy damage values appearing
    ///   when the damage values are examined.
    /// </summary>
    [DataField]
    public bool DisableHeavy = false;

    /// <summary>
    ///   If true, disables single-target attacks for this weapon, and prevents the single-target damage values appearing
    ///   when the damage values are examined.
    /// </summary>
    [DataField]
    public bool DisableClick = false;

    /// <summary>
    ///   If true, when a light attack misses, the weapon will perform a power attack instead.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool HeavyOnLightMiss = false;

    /*
     * Melee combat works based around 2 types of attacks:
     * 1. Click attacks with left-click. This attacks whatever is under your mnouse
     * 2. Wide attacks with right-click + left-click. This attacks whatever is in the direction of your mouse.
     */

    /// <summary>
    /// How many seconds between attacks must you wait in order to swing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AttackRate = 1f;

    /// <summary>
    ///     When power attacking, the required cooldown between swings is multiplied by this amount.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float HeavyRateModifier = 1.2f;
    /// <summary>
    /// Are we currently holding down the mouse for an attack.
    /// Used so we can't just hold the mouse button and attack constantly.
    /// </summary>
    [AutoNetworkedField]
    public bool Attacking = false;

    /// <summary>
    /// If true, attacks will be repeated automatically without requiring the mouse button to be lifted.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AutoAttack;

    /// <summary>
    /// If true, attacks will bypass armor resistances.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public bool ResistanceBypass = false;

    /// <summary>
    /// Base damage for this weapon. Can be modified via heavy damage or other means.
    /// </summary>
    [DataField(required: true)]
    [AutoNetworkedField]
    public DamageSpecifier Damage = default!;

    [DataField, AutoNetworkedField]
    public FixedPoint2 BluntStaminaDamageFactor = FixedPoint2.New(1f);

    /// <summary>
    /// Multiplies damage by this amount for single-target attacks.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 ClickDamageModifier = FixedPoint2.New(1);

    /// <summary>
    ///     Part damage is multiplied by this amount for single-target attacks
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ClickPartDamageMultiplier = 1.00f;

    // TODO: Temporarily 1.5 until interactionoutline is adjusted to use melee, then probably drop to 1.2
    /// <summary>
    /// Nearest edge range to hit an entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Range = 1.5f;

    /// <summary>
    ///     Bonus range for disarm attacks, not currently used for melee weapons, and is instead only on innate melee.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DisarmRangeModifier = 1f;

    /// <summary>
    ///     Bonus attack range for light (direct click) attacks.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LightRangeModifier = 1f;

    /// <summary>
    ///     Attack range for heavy swings
    /// </summary>
    [DataField, AutoNetworkedField]
    public float HeavyRangeModifier = 1f;

    /// <summary>
    ///     Weapon damage is multiplied by this amount for heavy swings
    /// </summary>
    [DataField, AutoNetworkedField]
    public float HeavyDamageBaseModifier = 0.8f; // WD EDIT

    /// <summary>
    ///     Part damage is multiplied by this amount for heavy swings
    /// </summary>
    [DataField, AutoNetworkedField]
    public float HeavyPartDamageMultiplier = 0.5f;

    /// <summary>
    /// Total width of the angle for wide attacks.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Angle Angle = Angle.FromDegrees(70);

    [DataField, AutoNetworkedField]
    public EntProtoId Animation = "WeaponArcThrust";

    [DataField, AutoNetworkedField]
    public EntProtoId WideAnimation = "WeaponArcSlash";

    // WD EDIT START
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public EntProtoId DisarmAnimation = "WeaponArcDisarm";

    [DataField, AutoNetworkedField]
    public bool CanHeavyAttack = true;

    /// <summary>
    /// Rotation of the animation.
    /// 0 degrees means the top faces the attacker.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Angle AnimationRotation = Angle.Zero;
    // WD EDIT END

    /// <summary>
    /// Rotation of the animation.
    /// 0 degrees means the top faces the attacker.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Angle WideAnimationRotation = Angle.Zero;

    [DataField]
    public bool SwingLeft;

    [DataField, AutoNetworkedField]
    public float HeavyStaminaCost = 12f; // WD EDIT

    [DataField, AutoNetworkedField]
    public int MaxTargets = 3;

    // Sounds

    /// <summary>
    /// This gets played whenever a melee attack is done. This is predicted by the client.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier SoundSwing { get; set; } = new SoundPathSpecifier("/Audio/Weapons/punchmiss.ogg")
    {
        Params = AudioParams.Default.WithVolume(-3f).WithVariation(0.025f),
    };

    // We do not predict the below sounds in case the client thinks but the server disagrees. If this were the case
    // then a player may doubt if the target actually took damage or not.
    // If overwatch and apex do this then we probably should too.

    [DataField, AutoNetworkedField]
    public SoundSpecifier? SoundHit;

    /// <summary>
    /// Plays if no damage is done to the target entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier SoundNoDamage { get; set; } = new SoundCollectionSpecifier("WeakHit");

    /// <summary>
    ///     Arguments for the MeleeContestInteractions constructor
    /// </summary>
    [DataField]
    public ContestArgs ContestArgs = new ContestArgs
    {
        DoStaminaInteraction = true,
        StaminaDisadvantage = true,
        DoHealthInteraction = true,
    };

    // WD EDIT START
    [ViewVariables(VVAccess.ReadWrite), DataField, AutoNetworkedField]
    public bool CanBeBlocked = true;
    // WD EDIT END

    /// <summary>
    ///     If true, the weapon must be equipped for it to be used.
    ///     E.g boxing gloves must be equipped to your gloves,
    ///     not just held in your hand to be used.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool MustBeEquippedToUse = false;

    // WD EDIT START - just use CanHeavyAttack
    // Goobstation
    // [DataField, AutoNetworkedField]
    // public bool CanWideSwing = true;
    // WD EDIT END
}

/// <summary>
/// Event raised on entity in GetWeapon function to allow systems to manually
/// specify what the weapon should be.
/// </summary>
public sealed class GetMeleeWeaponEvent : HandledEntityEventArgs
{
    public EntityUid? Weapon;
}
