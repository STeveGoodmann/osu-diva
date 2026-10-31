using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.DivaFlight.UI
{
    public sealed class NoteTextures
    {
        public Texture Body { get; private init; } = null!;
        public Texture Core { get; private init; } = null!;
        public Texture SliderMark { get; private init; } = null!;
        public Texture SpinnerMark { get; private init; } = null!;
        public Texture Arrival { get; private init; } = null!;

        public static NoteTextures Load(TextureStore textures, string prefix) => new NoteTextures
        {
            Body = textures.Get(prefix + "note-body")!,
            Core = textures.Get(prefix + "note-core")!,
            SliderMark = textures.Get(prefix + "slider-mark")!,
            SpinnerMark = textures.Get(prefix + "spinner-mark")!,
            Arrival = textures.Get(prefix + "arrival-ring")!,
        };
    }

    /// <summary>
    /// One note (or one slider body guide) on screen. Positioned at its target; the flying part is moved by <see cref="UpdateTime"/>.
    /// </summary>
    public partial class FlightVisual : CompositeDrawable
    {
        /// <summary>The note textures are 128px canvases with a 78px circle in the middle.</summary>
        private const float canvas_scale = 128f / 78f;

        private const double fade_in_time = 150;

        private readonly FlightPart part;
        private readonly Color4 comboColour;
        private readonly float speed;

        private readonly Container? flyer;
        private readonly Sprite? body;
        private readonly Sprite? trailSprite;
        private readonly Sprite? arrival;
        private readonly Vector2 entryDirection;

        public FlightVisual(FlightPart part, NoteTextures textures, Texture trail, Color4 comboColour, bool showTrail, float speed)
        {
            this.part = part;
            this.comboColour = comboColour;
            this.speed = speed;

            Anchor = Anchor.TopLeft;
            Origin = Anchor.Centre;
            Position = part.Target;

            float canvas = part.Diameter * canvas_scale;
            Size = new Vector2(canvas);

            if (part.Kind == FlightPartKind.SliderBody)
            {
                buildBody();
                return;
            }

            // part.Direction is the Python (dx, dy): the note starts at target + (dx, -dy) * distance.
            entryDirection = new Vector2(part.Direction.X, -part.Direction.Y);

            var children = new System.Collections.Generic.List<Drawable>();

            children.Add(arrival = new Sprite
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(canvas),
                Texture = textures.Arrival,
                Colour = comboColour,
                Alpha = 0,
            });

            var flyerChildren = new System.Collections.Generic.List<Drawable>();

            if (showTrail)
            {
                // The trail texture is 512x128: tail on the left, head anchor at the right edge. Rotate it so the tail points back along the flight path.
                flyerChildren.Add(trailSprite = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.CentreRight,
                    Size = new Vector2(part.Diameter * 4, part.Diameter),
                    Texture = trail,
                    Rotation = (float)(Math.Atan2(-entryDirection.Y, -entryDirection.X) * 180 / Math.PI),
                    Alpha = 0.72f,
                });
            }

            flyerChildren.Add(body = new Sprite
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(canvas),
                Texture = textures.Body,
            });

            flyerChildren.Add(centred(textures.Core, canvas));

            if (part.Kind == FlightPartKind.SliderHead)
                flyerChildren.Add(centred(textures.SliderMark, canvas));
            else if (part.Kind == FlightPartKind.Spinner)
                flyerChildren.Add(centred(textures.SpinnerMark, canvas));

            if (part.Number > 0)
            {
                bool light = (comboColour.R * 0.299f + comboColour.G * 0.587f + comboColour.B * 0.114f) > 0.57f;

                flyerChildren.Add(new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = part.Number.ToString(),
                    Font = OsuFont.GetFont(weight: FontWeight.Bold, size: Math.Max(9, part.Diameter * 0.48f)),
                    Colour = light ? Color4.Black : Color4.White,
                });
            }

            children.Add(flyer = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(canvas),
                Children = flyerChildren.ToArray(),
            });

            InternalChildren = children.ToArray();
        }

        private static Sprite centred(Texture texture, float canvas) => new Sprite
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Size = new Vector2(canvas),
            Texture = texture,
        };

        private void buildBody()
        {
            var slider = part.Slider!;
            double length = slider.Path.Distance;
            float spacing = Math.Max(8, part.Diameter * 0.22f);
            int count = (int)Math.Clamp(length / spacing, 2, 120);
            float dot = Math.Max(3, part.Diameter * 0.1f);

            var dots = new Drawable[count + 1];

            for (int i = 0; i <= count; i++)
            {
                dots[i] = new Circle
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(dot),
                    Position = slider.Path.PositionAt((double)i / count),
                    Colour = comboColour,
                };
            }

            InternalChildren = dots;
            Alpha = 0;
        }

        public void UpdateTime(double now)
        {
            if (part.Kind == FlightPartKind.SliderBody)
            {
                double fadeIn = Math.Clamp((now - part.VisibleFrom) / fade_in_time, 0, 1);
                double fadeOut = now <= part.HoldUntil ? 1 : 1 - Math.Clamp((now - part.HoldUntil) / DivaFlightLayer.ARRIVAL_TIME, 0, 1);
                Alpha = (float)(0.35 * fadeIn * fadeOut);
                return;
            }

            double remaining = part.Time - now;

            if (remaining > 0)
            {
                var offset = DivaFlightMath.FlightOffset(part.Direction, remaining, speed);
                flyer!.Position = offset;
                flyer.Alpha = (float)Math.Clamp((now - part.VisibleFrom) / fade_in_time, 0, 1);
                body!.Colour = DivaFlightMath.DistanceColour(offset.Length * DivaFlightMath.COLOUR_DISTANCE_SCALE, comboColour);

                if (trailSprite != null)
                    trailSprite.Colour = body.Colour;

                arrival!.Alpha = 0;
                return;
            }

            flyer!.Position = Vector2.Zero;
            body!.Colour = DivaFlightMath.DistanceColour(0, comboColour);

            if (trailSprite != null)
                trailSprite.Alpha = 0;

            if (now <= part.HoldUntil)
            {
                // Spinners wait on their target until the spinner ends.
                flyer.Alpha = 1;
                arrival!.Alpha = 0;
                return;
            }

            // Arrived. This only marks the planned time, not whether the hit succeeded.
            float progress = (float)Math.Clamp((now - part.HoldUntil) / DivaFlightLayer.ARRIVAL_TIME, 0, 1);
            flyer.Alpha = 1 - progress;
            arrival!.Alpha = 0.8f * (1 - progress);
            arrival.Scale = new Vector2(1 + 0.7f * progress);
        }
    }
}
