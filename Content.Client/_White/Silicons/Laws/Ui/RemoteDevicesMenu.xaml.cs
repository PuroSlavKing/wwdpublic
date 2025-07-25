using Content.Shared._White.Silicons.Borgs.Components;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.XAML;
using FancyWindow = Content.Client.UserInterface.Controls.FancyWindow;

namespace Content.Client._White.Silicons.Laws.Ui;

[GenerateTypedNameReferences]
public sealed partial class RemoteDevicesMenu : FancyWindow
{
    public event Action<RemoteDeviceActionEvent>? OnRemoteDeviceAction;
    public RemoteDevicesMenu()
    {
        RobustXamlLoader.Load(this);
    }
    public void Update(EntityUid uid, RemoteDevicesBuiState state)
    {
        RemoteDevicesDisplayContainer.Children.Clear();

        foreach (var device in state.DeviceList)
        {
            var control = new RemoteDeviceDisplay(device.NetEntityUid, device.DisplayName);

            control.OnRemoteDeviceAction += (action) =>
            {
                OnRemoteDeviceAction?.Invoke(action);
            };

            RemoteDevicesDisplayContainer.AddChild(control);
        }
    }
}

