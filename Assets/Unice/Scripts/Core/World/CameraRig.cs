using UnityEngine;

namespace Unice
{
    /// <summary>The orthographic camera: follows the view in a match, drifts slowly over the house behind the menus, shakes on a catch.</summary>
    sealed class CameraRig
    {
        const float ShakeDuration = .35f;
        /// <summary>How fast the camera catches up with the view (per second).</summary>
        const float FollowSpeed = 6;
        /// <summary>The camera looks a little above the player's feet.</summary>
        const float LookAboveFeet = 20;
        /// <summary>Behind the menus the camera wanders around this point, this far.</summary>
        const float IdleCenterX = 860, IdleCenterY = 590, IdleRangeX = 520, IdleRangeY = 300;
        /// <summary>At least this many house pixels show vertically (more on narrow screens, so the width fits ~700).</summary>
        const float MinHalfHeight = 250, HalfWidth = 350;

        public readonly Camera Camera;
        float lookX = IdleCenterX, lookY = IdleCenterY, shakeTimeLeft, shakeMagnitude;

        public CameraRig()
        {
            var camera = Camera.main;
            if (camera == null) { camera = new GameObject("Camera").AddComponent<Camera>(); camera.tag = "MainCamera"; }
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.nearClipPlane = .1f; camera.farClipPlane = 50;
            if (Object.FindAnyObjectByType<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();
            Camera = camera;
        }

        /// <param name="magnitude">How far it shakes at first, in house pixels.</param>
        public void Shake(float magnitude) { shakeTimeLeft = ShakeDuration; shakeMagnitude = magnitude; }

        /// <summary>Every frame. <paramref name="followView"/>: in a match (otherwise it drifts over the house).</summary>
        public void Follow(float dt, bool followView, ViewInfo view)
        {
            float time = Time.unscaledTime;
            if (followView)
            {
                float blend = Mathf.Min(1, dt * FollowSpeed);
                lookX += (view.x - lookX) * blend; lookY += (view.y - LookAboveFeet - lookY) * blend;
            }
            else
            {
                lookX = IdleCenterX + Mathf.Sin(time * .1f) * IdleRangeX;
                lookY = IdleCenterY + Mathf.Sin(time * .16f) * IdleRangeY;
            }
            Camera.orthographicSize = Mathf.Max(MinHalfHeight, HalfWidth / Mathf.Max(.5f, Camera.aspect)) / Art.PxPerUnit;
            Camera.transform.position = Art.ToWorld(lookX, lookY, -10);
            if (shakeTimeLeft > 0)
            {
                shakeTimeLeft -= dt;
                float strength = Mathf.Max(0, shakeTimeLeft) / ShakeDuration;
                Camera.transform.position += (Vector3)(Random.insideUnitCircle * (shakeMagnitude * strength) / Art.PxPerUnit);
            }
        }
    }
}
