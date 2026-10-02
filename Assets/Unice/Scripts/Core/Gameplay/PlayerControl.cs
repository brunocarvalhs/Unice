using UnityEngine;
using UnityEngine.InputSystem;

namespace Unice
{
    /// <summary>
    /// This device's player: keyboard and joystick input, moving the character, and the E/Q/R actions. The host applies
    /// actions directly; a guest sends them to the host in its presence (see <see cref="RoomSync.Publish"/>).
    /// </summary>
    sealed class PlayerControl
    {
        /// <summary>Joystick/keys below this length count as no input.</summary>
        const float InputDeadZone = .3f;
        /// <summary>After stepping into a spot, input is ignored this long so the character doesn't walk straight out.</summary>
        const float HideFreezeSeconds = .35f;
        /// <summary>Guest Unice: seconds between two catch requests, so one touch doesn't send a burst.</summary>
        const float CatchRequestInterval = .3f;

        readonly App app;
        Game game => app.game;
        Character me => app.myCharacter;
        bool upHeld, downHeld, leftHeld, rightHeld, sneakHeld;
        float freezeTime, catchCooldown;

        /// <summary>The last action, published in this device's presence; the host applies it once per new <see cref="actionSeq"/>.</summary>
        public int actionSeq, actionArg;
        public PlayerAction action;

        public PlayerControl(App app) { this.app = app; }

        /// <summary>New room or match: no pending action.</summary>
        public void Reset() { actionSeq = 0; action = PlayerAction.None; actionArg = -1; }

        public void TickCooldowns(float dt)
        {
            if (freezeTime > 0) freezeTime -= dt;
            if (catchCooldown > 0) catchCooldown -= dt;
        }

        /// <summary>Per frame: which keys are held, plus the one-shot keys (E/Q/R/P, Esc).</summary>
        public void ReadKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) { upHeld = downHeld = leftHeld = rightHeld = sneakHeld = false; return; }
            bool typing = app.ui.Typing;   // keys go to the text field
            upHeld = !typing && (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed);
            downHeld = !typing && (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed);
            leftHeld = !typing && (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed);
            rightHeld = !typing && (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed);
            sneakHeld = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            if (keyboard.escapeKey.wasPressedThisFrame) app.Back();   // also the Android back button
            if (typing || !game.IsLive || app.paused) return;
            if (keyboard.eKey.wasPressedThisFrame) UseNearbySpot();
            if (keyboard.qKey.wasPressedThisFrame) TryPassDoll();
            if (keyboard.rKey.wasPressedThisFrame) TryLaugh();
            if (keyboard.pKey.wasPressedThisFrame) app.TogglePause();
        }

        /// <summary>Keys plus joystick, as a unit direction (zero inside the dead zone).</summary>
        public Vector2 MoveInput()
        {
            var keys = new Vector2((rightHeld ? 1 : 0) - (leftHeld ? 1 : 0), (downHeld ? 1 : 0) - (upHeld ? 1 : 0));
            var direction = keys + app.ui.JoystickDirection;
            return direction.magnitude < InputDeadZone ? Vector2.zero : direction.normalized;
        }

        /// <summary>Moves this player from the input. As the Unice on a guest device, also asks the host to catch whoever it touches.</summary>
        public void Move(float dt)
        {
            var character = me; character.moving = false;
            if (!game.CanMove(character) || freezeTime > 0 || app.paused) { character.loud = false; character.talking = false; return; }

            character.sneaking = !character.IsUnice && (sneakHeld || app.ui.SneakToggled);
            var direction = MoveInput();
            if (direction != Vector2.zero)
            {
                float speed = character.IsUnice ? Rules.UnicePlayerSpeed : character.sneaking ? Rules.SneakSpeed : Rules.RunSpeed;
                float oldX = character.x, oldY = character.y;
                Game.MoveCharacter(character, direction.x * speed * dt, direction.y * speed * dt);
                character.faceX = direction.x; character.faceY = direction.y;
                character.moving = Geometry.Distance(character.x, character.y, oldX, oldY) > .01f;
            }
            character.loud = !character.IsUnice && character.moving && !character.sneaking;
            character.talking = app.IsTalking;

            // the host catches by touch itself (Game.Simulate); a guest Unice asks for it
            if (character.IsUnice && game.phase == Phase.Hunt && !game.isHost && catchCooldown <= 0)
                foreach (var hider in game.characters)
                    if (!hider.IsUnice && !hider.outOfRound && hider.hidingSpot == null && Geometry.Distance(character, hider) < Rules.CatchRange)
                    {
                        SendAction(PlayerAction.Catch, hider.index);
                        catchCooldown = CatchRequestInterval;
                        break;
                    }
        }

        /// <summary>Per step during a round: the HUD's noise meter follows how loud the player is, smoothly.</summary>
        public void UpdateNoiseLevel(float dt)
        {
            if (me == null) return;
            float target = me.loud ? 1 : 0;
            me.noiseLevel += (target - me.noiseLevel) * Mathf.Min(1, dt * 6);
        }

        void SendAction(PlayerAction newAction, int arg = -1)
        {
            if (game.isHost) { game.ApplyAction(me, newAction, arg); return; }
            actionSeq++; action = newAction; actionArg = arg;
            app.room.sync.PublishSoon();
        }

        /// <summary>E: hide in / leave the spot nearby, or (Unice) open it.</summary>
        public void UseNearbySpot()
        {
            var character = me;
            if (character == null || character.outOfRound || !game.IsLive || app.paused) return;
            if (character.IsUnice)
            {
                if (game.phase != Phase.Hunt || character.stunTime > 0) return;
                var spotToOpen = Game.NearestSpot(character);
                if (spotToOpen == null) { app.ui.Toast(Localization.Get("act.unice.need_near")); return; }
                SendAction(PlayerAction.Open, spotToOpen.spotIndex);
                Metrics.Log("player_action", ("type", "open"));
                return;
            }
            if (character.hidingSpot != null)
            {
                app.sfx.Play("creak"); SendAction(PlayerAction.Exit);
                Metrics.Log("player_action", ("type", "exit"));
                return;
            }
            var spot = Game.NearestSpot(character);
            if (spot == null) { app.ui.Toast(Localization.Get("act.hide.need_near")); return; }
            if (spot.occupant != null) { app.ui.Toast(Localization.Get("act.hide.occupied", spot.occupant.name)); app.sfx.Play("bad"); return; }
            app.sfx.Play("creak"); freezeTime = HideFreezeSeconds;
            SendAction(PlayerAction.Hide, spot.spotIndex);
            Metrics.Log("player_action", ("type", "hide"), ("spot", spot.type));
        }

        /// <summary>Q: hand the doll to someone right next to you.</summary>
        public void TryPassDoll()
        {
            var character = me;
            if (character == null || character.outOfRound || character.IsUnice || !game.IsLive || app.paused) return;
            if (game.doll.holder != character) { app.ui.Toast(Localization.Get("act.pass.no_doll")); return; }
            if (character.passCooldown > 0) { app.ui.Toast(Localization.Get("act.pass.cooldown", Mathf.CeilToInt(character.passCooldown))); app.sfx.Play("bad"); return; }
            if (game.PassTarget(character) == null) { app.ui.Toast(Localization.Get("act.pass.no_target")); return; }
            SendAction(PlayerAction.Pass);
            Metrics.Log("player_action", ("type", "pass"));
        }

        /// <summary>R: laugh on purpose. Anyone can, but it gives away your position to the Unice like the doll's own giggle.</summary>
        public void TryLaugh()
        {
            var character = me;
            if (character == null || character.outOfRound || character.IsUnice || !game.IsLive || app.paused) return;
            if (character.laughCooldown > 0) { app.ui.Toast(Localization.Get("act.laugh.cooldown", Mathf.CeilToInt(character.laughCooldown))); app.sfx.Play("bad"); return; }
            SendAction(PlayerAction.Laugh);
            Metrics.Log("player_action", ("type", "laugh"));
        }
    }
}
