using Content.Shared._Goobstation.TableSlam; // Goobstation - Table SLam
using Content.Shared.Alert;
using Content.Shared.Movement.Pulling.Systems; // Goobstation
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Movement.Pulling.Components;

/// <summary>
/// Specifies an entity as being pullable by an entity with <see cref="PullerComponent"/>
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(Systems.PullingSystem), typeof(TableSlamSystem))]
public sealed partial class PullableComponent : Component
{
    /// <summary>
    /// The current entity pulling this component.
    /// </summary>
    [AutoNetworkedField, DataField]
    public EntityUid? Puller;

    /// <summary>
    /// The pull joint.
    /// </summary>
    [AutoNetworkedField, DataField]
    public string? PullJointId;

    public bool BeingPulled => Puller != null;

    /// <summary>
    /// If the physics component has FixedRotation should we keep it upon being pulled
    /// </summary>
    [Access(typeof(Systems.PullingSystem), Other = AccessPermissions.ReadExecute)]
    [ViewVariables(VVAccess.ReadWrite), DataField("fixedRotation")]
    public bool FixedRotationOnPull;

    /// <summary>
    /// What the pullable's fixedrotation was set to before being pulled.
    /// </summary>
    [Access(typeof(Systems.PullingSystem), Other = AccessPermissions.ReadExecute)]
    [AutoNetworkedField, DataField]
    public bool PrevFixedRotation;

    [DataField]
    public Dictionary<GrabStage, short> PulledAlertAlertSeverity = new()
    {
        { GrabStage.No, 0 },
        { GrabStage.Soft, 1 },
        { GrabStage.Hard, 2 },
        { GrabStage.Suffocate, 3 },
    };

    // WWDP edit start
    [AutoNetworkedField, DataField]
    public float EscapeAttemptCooldown = 1; // In seconds

    [AutoNetworkedField]
    public bool DisplayedCooldownPopup = true;
    // WWDP edit end

    [AutoNetworkedField, DataField]
    public GrabStage GrabStage = GrabStage.No;

    [AutoNetworkedField, DataField]
    public float GrabEscapeChance = 1f;

    [AutoNetworkedField]
    public TimeSpan NextEscapeAttempt = TimeSpan.Zero;

    /// <summary>
    /// If this pullable being tabled.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool BeingTabled = false;

    /// <summary>
    /// Constant for tabling throw math
    /// </summary>
    [DataField]
    public float BasedTabledForceSpeed = 5f;

    /// <summary>
    ///  Stamina damage. taken on tabled
    /// </summary>
    [DataField]
    public float TabledStaminaDamage = 40f;

    /// <summary>
    /// Damage taken on being tabled.
    /// </summary>
    [DataField]
    public float TabledDamage = 5f;
    // Goobstation end

    /// <summary>
    ///     Whether the entity is currently being actively pushed by the puller.
    ///     If true, the entity will be able to enter disposals upon colliding with them, and the like.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool BeingActivelyPushed = false;
    [DataField]
    public ProtoId<AlertPrototype> PulledAlert = "Pulled";

}

public sealed partial class StopBeingPulledAlertEvent : BaseAlertEvent;
