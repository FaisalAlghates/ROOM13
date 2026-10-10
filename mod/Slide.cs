// ROOM 13 mod: running slide for the final chase.
// While sprinting in the last stage, press C (or Ctrl) to slide forward low and fast,
// which lets the player go under low obstacles without stopping.
using System;
using Room13;
using UnityEngine;

namespace Room13Mod
{
    public static class Slide
    {
        const float Duration = 0.9f;     // seconds
        const float Cooldown = 0.7f;     // seconds after a slide ends
        const float EyeDrop = 0.16f;     // extra camera lowering while sliding

        static bool sliding;
        static float t0, lastEnd = -99f;
        static bool hinted;
        static bool anywhere = Environment.GetEnvironmentVariable("ROOM13_SLIDE_ANY") == "1";

        static bool Allowed()
        {
            if (anywhere) return true;
            var d = R13.Director;
            if (d == null) return false;
            string s = d.Stage;
            return s != null && s.IndexOf("Final", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // called once per frame at the top of PlayerController.Update
        public static void Tick(PlayerController p)
        {
            if (!hinted && Allowed() && R13.UI != null)
            {
                hinted = true;
                R13.UI.Hint("Sprint + C to SLIDE under obstacles", 7f);
            }
            if (sliding && Time.time - t0 >= Duration) End(p);
        }

        // called at the top of TryToggleCrouch; true = key consumed (slide started)
        public static bool TryStart(PlayerController p)
        {
            if (sliding || p.BotControl || R13.Paused || R13.InputLocked) return false;
            if (!Allowed()) return false;
            if (Time.time - lastEnd < Cooldown) return false;
            if (!p.IsSprinting || !p.IsMoving) return false;
            if (p.cc == null || !p.cc.isGrounded) return false;

            sliding = true;
            t0 = Time.time;
            p.IsCrouching = true;
            p.cc.height = Mathf.Min(p.cc.height, p.crouchH);   // drop the collider at once so low pipes are cleared
            p.Shake(0.05f, 0.25f);
            if (R13.Audio != null) R13.Audio.Play2D("slide_scrape", 0.8f, 1f);
            Debug.Log("[ROOM13] slide start");
            return true;
        }

        static void End(PlayerController p)
        {
            sliding = false;
            lastEnd = Time.time;
            if (p.CanStand()) p.IsCrouching = false;           // otherwise stay crouched under the obstacle
            Debug.Log("[ROOM13] slide end");
        }

        // movement input override: push straight ahead during the slide
        public static Vector2 ModInput(PlayerController p, Vector2 v)
        {
            return sliding ? new Vector2(0f, 1f) : v;
        }

        // speed override: burst of speed that fades out
        public static float ModSpeed(PlayerController p, float v)
        {
            if (!sliding) return v;
            float k = Mathf.Clamp01((Time.time - t0) / Duration);
            return p.sprintSpeed * (1.45f - 0.95f * k * k);
        }

        public static float ModEye(PlayerController p, float v)
        {
            return sliding ? v - EyeDrop : v;
        }

        // CanStand() must say "no" while sliding so sprinting does not pop the player up
        public static bool BlocksStand() { return sliding; }
    }
}
