using UnityEngine;

namespace Unice
{
    /// <summary>The orthographic camera: follows the view in a match, drifts slowly over the house behind the menus, shakes on a catch.</summary>
    sealed class CameraRig
    {
        public readonly Camera Cam;
        float camX = 860, camY = 590, shakeT, shakeMag;
        const float ShakeDur = .35f;

        public CameraRig()
        {
            var cam = Camera.main;
            if (cam == null) { cam = new GameObject("Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; }
            cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            cam.nearClipPlane = .1f; cam.farClipPlane = 50;
            if (Object.FindAnyObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            Cam = cam;
        }

        public void Shake(float mag) { shakeT = ShakeDur; shakeMag = mag; }

        public void Frame(float dt, bool follow, ViewInfo view)
        {
            var cam = Cam; float time = Time.unscaledTime;
            if (follow) { float k = Mathf.Min(1, dt * 6); camX += (view.x - camX) * k; camY += (view.y - 20 - camY) * k; }
            else { camX = 860 + Mathf.Sin(time * .1f) * 520; camY = 590 + Mathf.Sin(time * .16f) * 300; }
            cam.orthographicSize = Mathf.Max(250, 350 / Mathf.Max(.5f, cam.aspect)) / Art.PxPerUnit;
            cam.transform.position = Art.W(camX, camY, -10);
            if (shakeT > 0)
            {
                shakeT -= dt; float k = Mathf.Max(0, shakeT) / ShakeDur;
                cam.transform.position += (Vector3)(UnityEngine.Random.insideUnitCircle * (shakeMag * k) / Art.PxPerUnit);
            }
        }
    }
}
