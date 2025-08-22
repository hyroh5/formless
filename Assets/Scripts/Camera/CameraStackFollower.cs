using UnityEngine;

[ExecuteAlways]
public class CameraStackFollower : MonoBehaviour
{
    public Camera mainCam;          // ���� ī�޶�
    public Camera[] overlayCams;    // Fluid, Gas ī�޶�
    public bool copyZPosition = false; // �ʿ��ϸ� z���� ����ȭ

    void Reset() { mainCam = Camera.main; }

    void LateUpdate()
    {
        if (!mainCam || overlayCams == null) return;

        var srcT = mainCam.transform;

        foreach (var cam in overlayCams)
        {
            if (!cam) continue;

            // ��ġ��ȸ���� ����ȭ. ������(FOV/OrthoSize), Ŭ����, rect�� �״�� ��
            var t = cam.transform;
            var p = srcT.position;
            if (!copyZPosition) p.z = t.position.z;
            t.position = p;
            t.rotation = srcT.rotation;
        }
    }
}
