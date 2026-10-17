using UnityEngine;
public interface IMainCameraProvider
{
    Camera Camera { get; }
    bool TryGetCamera(out Camera camera);
}
