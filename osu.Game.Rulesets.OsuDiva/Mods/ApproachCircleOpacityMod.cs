using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects.Drawables;

namespace osu.Game.Rulesets.DivaFlight.Mods
{
    /// <summary>
    /// Scales the opacity and the starting size of the approach circle (the ring that shrinks onto a circle before it has to be hit) of every circle and slider head.
    /// 100% leaves osu!'s own look, 0% hides the rings. The ring always ends at the size of the circle. The flying notes are the main timing indicator; a faint ring is a second one.
    /// Not a mod the player can pick: <see cref="UI.DrawableOsuDivaRuleset"/> adds it to its own list so lazer calls it for every new hit object.
    /// Spinners are left alone: their shrinking ring is the time left on the spinner, not a hit indicator.
    /// </summary>
    public class ApproachCircleOpacityMod : Mod, IApplicableToDrawableHitObject
    {
        /// <summary>osu! fades the approach circle in to this opacity.</summary>
        public const float DEFAULT_OPACITY = 0.9f;

        /// <summary>osu! starts the approach circle at this many times the size of the circle.</summary>
        public const float DEFAULT_START_SCALE = 4;

        /// <summary>
        /// The size the approach circle starts at, as a multiple of the circle, for a visibility from 0 to 1.
        /// Only the largest size is reduced: every ring still shrinks to exactly the size of the circle at the hit time.
        /// 100% is osu!'s own 4, 0% would be 1 (no ring to see).
        /// </summary>
        public static float StartScale(float visibility) => 1 + (DEFAULT_START_SCALE - 1) * System.Math.Clamp(visibility, 0, 1);

        public override string Name => "Approach circle opacity";
        public override string Acronym => "AO";
        public override LocalisableString Description => "Scales how visible the osu! approach circles are; the flying notes show the timing.";
        public override double ScoreMultiplier => 1;
        public override ModType Type => ModType.Fun;
        public override bool UserPlayable => false;
        public override bool ValidForMultiplayer => false;
        public override bool ValidForMultiplayerAsFreeMod => false;

        /// <summary>How visible the osu! approach circles are, 0 (hidden) to 1 (as in osu!).</summary>
        public BindableFloat Visibility { get; } = new BindableFloat(0.25f)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        public void ApplyToDrawableHitObject(DrawableHitObject drawable)
        {
            drawable.ApplyCustomUpdateState += (drawableObject, _) =>
            {
                if (drawableObject is not DrawableHitCircle circle)
                    return;

                float visibility = Visibility.Value;

                if (visibility >= 0.999f)
                    return;

                var hitObject = circle.HitObject;
                var approach = circle.ApproachCircle;

                // the same way lazer's Hidden mod hides them: from the moment the approach would start.
                if (visibility <= 0.001f)
                {
                    using (circle.BeginAbsoluteSequence(hitObject.StartTime - hitObject.TimePreempt))
                        approach.Hide();

                    return;
                }

                // osu! fades the ring in to 90% and out at the hit time. Do the same to a fraction of that.
                approach.ClearTransforms(targetMember: nameof(Drawable.Alpha));

                using (circle.BeginAbsoluteSequence(hitObject.StartTime - hitObject.TimePreempt))
                    approach.FadeTo(DEFAULT_OPACITY * visibility, System.Math.Min(hitObject.TimeFadeIn * 2, hitObject.TimePreempt));

                using (circle.BeginAbsoluteSequence(hitObject.StartTime))
                    approach.FadeOut(50);

                // osu! starts the ring at 4 times the size of the circle and shrinks it evenly to 1 over the approach time.
                // Start smaller, end at the same size.
                approach.ClearTransforms(targetMember: nameof(Drawable.Scale));

                using (circle.BeginAbsoluteSequence(hitObject.StartTime - hitObject.TimePreempt))
                    approach.ScaleTo(StartScale(visibility)).ScaleTo(1f, hitObject.TimePreempt);
            };
        }
    }
}
