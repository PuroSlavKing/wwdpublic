using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    protected override void InitializeBallistic()
    {
        base.InitializeBallistic();
        SubscribeLocalEvent<BallisticAmmoProviderComponent, UpdateAmmoCounterEvent>(OnBallisticAmmoCount);
    }

    private void OnBallisticAmmoCount(EntityUid uid, BallisticAmmoProviderComponent component, UpdateAmmoCounterEvent args)
    {
        if (args.Control is DefaultStatusControl control)
        {
            control.Update(GetBallisticShots(component), component.Capacity);
        }
    }

    protected override void Cycle(EntityUid uid, BallisticAmmoProviderComponent? component, MapCoordinates coordinates, GunComponent? gunComponent)
    {
        if (!Timing.IsFirstTimePredicted)
            return;

        EntityUid? ent = null;

        // TODO: Combine with TakeAmmo
        if (component != null && component.Entities.Count > 0) // WWDP
        {
            var existing = component.Entities[^1];
            component.Entities.RemoveAt(component.Entities.Count - 1);

            Containers.Remove(existing, component.Container);
            EnsureShootable(existing);
        }
        else if (component != null && component.UnspawnedCount > 0) // WWDP
        {
            component.UnspawnedCount--;
            ent = Spawn(component.Proto, coordinates);
            EnsureShootable(ent.Value);
        }

        if (ent != null && IsClientSide(ent.Value))
            Del(ent.Value);

        var cycledEvent = new GunCycledEvent();
        RaiseLocalEvent(uid, ref cycledEvent);
    }

    // WWDP extract round
    protected override void Extract(EntityUid uid, MapCoordinates coordinates, BallisticAmmoProviderComponent component,
        EntityUid user)
    {
        if (!Timing.IsFirstTimePredicted)
            return;

        EntityUid entity;

        if (component.Entities.Count > 0)
        {
            entity = component.Entities[^1];
            component.Entities.RemoveAt(component.Entities.Count - 1);
            EnsureShootable(entity);
        }
        else if (component.UnspawnedCount > 0)
        {
            component.UnspawnedCount--;
            entity = Spawn(component.Proto, coordinates);
            EnsureShootable(entity);
        }
        else
        {
            Popup(Loc.GetString("gun-ballistic-empty"), uid, user);
            return;
        }

        if (IsClientSide(entity))
            Del(entity);
    }
    // WWDP end
}
