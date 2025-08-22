using UnityEngine;

[ExecuteAlways]
public class CameraStackFollower : MonoBehaviour
{
    public Camera mainCam;          // 메인 카메라
    public Camera[] overlayCams;    // Fluid, Gas 카메라
    public bool copyZPosition = false; // 필요하면 z까지 동일화

    void Reset() { mainCam = Camera.main; }

    void LateUpdate()
    {
        if (!mainCam || overlayCams == null) return;

        var srcT = mainCam.transform;

        foreach (var cam in overlayCams)
        {
            if (!cam) continue;

            // 위치·회전만 동기화. 사이즈(FOV/OrthoSize), 클리핑, rect는 그대로 둠
            var t = cam.transform;
            var p = srcT.position;
            if (!copyZPosition) p.z = t.position.z;
            t.position = p;
            t.rotation = srcT.rotation;
        }
    }
}
