using System.Text.Json.Nodes;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Wire;
using static OpenWinter.Net.Nwn.Messages.CameraMessages;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// The camera script functions, as OpenWinter's own server sends them: SetCameraFacing, SetCameraMode,
/// SetCameraHeight, SetCameraLimits, SetCameraFlags, AttachCamera and RestoreCameraFacing become "camera" notifies,
/// StoreCameraFacing "camera-store" and LockCameraPitch / Distance / Direction "camera-lock". Measured with the
/// camanim scene: the facing's angle comes in radians (the script's degrees converted), its pitch in degrees from
/// straight down and its distance in metres, each only when the script gave it (not -1); the transition is the
/// script's CAMERA_TRANSITION_TYPE_*; heights, modes, limits and flags are the script's values.
/// </summary>
public static class NwnCamera
{
    /// <summary>The server's id for the player's own creature (the notifies' subject); attaching the camera to it hands the camera back.</summary>
    const uint Self = 0x7FFFFFFF;
    const uint InvalidObject = GameObjUpdateMessages.InvalidObjectId;

    public static bool ToClient(NwnMessage message, Action<byte[]> emit)
    {
        void Camera(JsonObject json) => Notify(emit, "camera", json);
        switch (message)
        {
            case CameraChangeLocation m:
                Camera(new JsonObject
                {
                    ["op"] = "facing",
                    ["direction"] = m.Angle is { } angle ? Degrees(angle) : -1f,
                    ["distance"] = m.Distance ?? -1f,
                    ["pitch"] = m.Pitch ?? -1f,
                    ["transition"] = m.SmoothTranslation ?? 0,
                });
                return true;
            case CameraSetMode m:
                Camera(new JsonObject { ["op"] = "mode", ["mode"] = (int)m.Mode });
                return true;
            case CameraStore:
                Notify(emit, "camera-store", []);
                return true;
            case CameraRestore:
                Camera(new JsonObject { ["op"] = "restore" });
                return true;
            case CameraSetHeight m:
                Camera(new JsonObject { ["op"] = "height", ["height"] = m.Height });
                return true;
            case CameraLockPitch m:
                Notify(emit, "camera-lock", new JsonObject { ["which"] = "pitch", ["locked"] = m.Lock });
                return true;
            case CameraLockDist m:
                Notify(emit, "camera-lock", new JsonObject { ["which"] = "distance", ["locked"] = m.Lock });
                return true;
            case CameraLockYaw m:
                Notify(emit, "camera-lock", new JsonObject { ["which"] = "direction", ["locked"] = m.Lock });
                return true;
            case CameraSetLimits m:
                Camera(new JsonObject
                {
                    ["op"] = "limits", ["minPitch"] = m.MinPitch, ["maxPitch"] = m.MaxPitch, ["minDistance"] = m.MinDist, ["maxDistance"] = m.MaxDist,
                });
                return true;
            case CameraSetFlags m:
                Camera(new JsonObject { ["op"] = "flags", ["flags"] = m.Flags });
                return true;
            case CameraAttach m:
                Camera(new JsonObject
                {
                    ["op"] = "attach", ["target"] = m.Target == Self ? InvalidObject : m.Target, ["findClearView"] = m.FindClearView,
                });
                return true;
        }
        return false;
    }

    /// <summary>The wire's radians as the script's degrees, 0 to 360 anticlockwise from east.</summary>
    static float Degrees(float radians)
    {
        float degrees = radians * (180f / MathF.PI) % 360f;
        return degrees < 0 ? degrees + 360f : degrees;
    }

    static void Notify(Action<byte[]> emit, string kind, JsonObject json) =>
        emit(new NotifyMessage { Kind = kind, Subject = Self, Json = json.ToJsonString() }.Write());
}
