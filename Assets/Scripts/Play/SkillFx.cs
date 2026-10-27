using UnityEngine;

namespace VK
{
    /// <summary>Stage ladder of Thần Thông visuals and sounds (spec section 8). Stage 0 adds nothing on top of the base effect.</summary>
    public static class SkillFx
    {
        public static void Ladder(int stage, Vector2 pos, Color c)
        {
            if (stage <= 0) return;
            var run = Game.I.run;
            if (stage >= 1)
            {
                Fx.Ring(pos, c, 0.3f, 2.2f, 0.45f);
                Fx.Sigil(pos + Vector2.down * 0.35f, c, 0.75f, 0.6f);
                Sfx.Play("sk_chime", 0.5f, 0.04f);
            }
            if (stage >= 2)
            {
                Fx.Sigil(pos + Vector2.down * 0.35f, c, 1.5f, 1f, 70f);
                Fx.ScreenTint(c, 0.08f, 0.25f);
                Fx.Hitstop(0.06f);
                Sfx.Play("sk_whoosh", 0.6f, 0.03f);
                Sfx.Play("sk_boom", 0.5f, 0.03f);
            }
            if (stage >= 3)
            {
                if (run != null) Fx.Apparition(Res.Tile(run.ch.sprite), pos, c, 6f, 0.9f);
                Fx.Pillar(pos, c, 9f, 0.7f);
                Fx.ScreenTint(c, 0.12f, 0.3f);
                Sfx.Play("sk_choir", 0.6f, 0.02f);
                Sfx.Play("sk_boom", 0.7f, 0.03f, 0.8f);
            }
            if (run != null && run.stageCallout)
            {   // first cast after a Tiến Giai names the new form
                run.stageCallout = false;
                Callout(Skills.FormName(run.ch, stage), pos, c);
            }
        }

        /// <summary>Name of the new form above the hero.</summary>
        public static void Callout(string text, Vector2 pos, Color c) => Fx.Text(pos + Vector2.up * 1.4f, text, c, 1.4f, 1.6f);
    }
}
