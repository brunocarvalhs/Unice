using UnityEngine;

namespace Unice
{
    /// <summary>A hider hears (and sees, as a red vignette) their heart beat faster the closer the Unice gets.</summary>
    sealed class Heartbeat
    {
        readonly App app;
        float beatTimer;

        /// <summary>0 (far or not hunting) .. 1 (the Unice is right here).</summary>
        public float Intensity { get; private set; }
        /// <summary>1 on each beat, fading to 0; drives the vignette's throb.</summary>
        public float Pulse { get; private set; }

        public Heartbeat(App app) { this.app = app; }

        public void Stop() => Intensity = 0;

        /// <summary>Per step while a round is live and not paused.</summary>
        public void Tick(float dt)
        {
            var game = app.game; var me = app.myCharacter; var uniceCharacter = game.unice;
            bool canFeelIt = game.phase == Phase.Hunt && me != null && !me.IsUnice && !me.outOfRound && uniceCharacter != null && uniceCharacter.x > 0;
            if (!canFeelIt) { Intensity = 0; return; }
            float distance = Geometry.Distance(me, uniceCharacter);
            Intensity = distance < Rules.HeartbeatRange ? 1 - distance / Rules.HeartbeatRange : 0;
            beatTimer -= dt;
            if (Intensity > 0 && beatTimer <= 0)
            {
                beatTimer = .42f + (1 - Intensity) * .7f;   // faster when closer
                Pulse = 1;
                app.sfx.Play("heart");
            }
        }

        /// <summary>Per frame: the pulse fades out.</summary>
        public void Fade(float dt) => Pulse = Mathf.Max(0, Pulse - dt * 2.5f);
    }
}
