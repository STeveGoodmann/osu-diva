using osu.Framework.Localisation;

namespace osu.Game.Rulesets.DivaFlight.Localisation
{
    /// <summary>
    /// The texts of the ruleset's settings in English and Simplified Chinese. The language follows lazer's own language setting:
    /// any Chinese culture (zh, zh-CN, zh-TW, ...) gets the Chinese text, everything else the English text.
    /// </summary>
    public static class DivaStrings
    {
        private sealed class Bilingual : ILocalisableStringData
        {
            private readonly string english;
            private readonly string chinese;

            public Bilingual(string english, string chinese)
            {
                this.english = english;
                this.chinese = chinese;
            }

            public string GetLocalised(LocalisationParameters parameters)
                => parameters.Store?.EffectiveCulture.TwoLetterISOLanguageName == "zh" ? chinese : english;

            public bool Equals(ILocalisableStringData? other) => other is Bilingual b && b.english == english && b.chinese == chinese;

            public override bool Equals(object? obj) => obj is ILocalisableStringData other && Equals(other);

            public override int GetHashCode() => System.HashCode.Combine(english, chinese);

            public override string ToString() => english;
        }

        private static LocalisableString text(string english, string chinese) => new LocalisableString(new Bilingual(english, chinese));

        public static LocalisableString FlightSpeed => text("Flight speed", "飞行速度");
        public static LocalisableString FlightSpeedHint => text("How fast notes fly in, in osu! pixels per second (400 is about 900 px/s on a 1080p screen).", "音符飞入的速度，单位是 osu! 像素/秒（400 约等于 1080p 屏幕上的 900 像素/秒）。");

        public static LocalisableString NoteSize => text("Note size", "音符大小");

        public static LocalisableString FlightDirection => text("Flight direction", "飞行方向");
        public static LocalisableString FlightDirectionHint => text("Vertical, or fan: the angle grows towards the sides of the screen. The optimiser starts from this direction.", "竖直，或扇形：越靠近屏幕两侧，飞入角度越大。优化器从这个方向开始搜索。");

        public static LocalisableString FanAngle => text("Fan angle", "扇形角度");
        public static LocalisableString FanAngleHint => text("Notes at the edge of the screen fly in at up to this angle (degrees).", "位于屏幕边缘的音符，飞入角度最大到这个度数。");

        public static LocalisableString ShowNumbers => text("Show combo numbers", "显示连击编号");
        public static LocalisableString ShowTrail => text("Show trails", "显示拖尾");

        public static LocalisableString ShowSliderBody => text("Show slider body guide", "显示滑条引导点");
        public static LocalisableString ShowSliderBodyHint => text("Dots along the slider path while the slider is coming up.", "滑条出现之前，沿滑条路径显示一排小圆点。");

        public static LocalisableString ApproachCircleVisibility => text("osu! approach circle visibility", "osu! 接近圈可见度");

        public static LocalisableString ApproachCircleVisibilityHint => text(
            "How visible the ring that shrinks onto each circle is: 0% hides it, 100% is the normal osu! look. It sets the opacity and the size the ring starts at (1 + 3 x this, in circle sizes); it always ends at the size of the circle. The flying notes are the main timing indicator. Applies to notes that appear after the change. Spinner rings are not affected.",
            "打击前逐渐缩小到圆圈上的那个圆环的可见程度：0% 为隐藏，100% 为 osu! 原来的样子。它同时决定圆环的不透明度和起始大小（起始大小 = 1 + 3 × 此值，单位是圆圈大小），圆环最后总是缩小到和圆圈一样大。飞行音符是主要的时机提示。只对改动之后出现的物件生效，转盘的圆环不受影响。");

        public static LocalisableString OptimiseTrajectories => text("Optimise trajectories", "优化飞行轨迹");
        public static LocalisableString OptimiseTrajectoriesHint => text("Adjust angles to avoid notes that are on screen too briefly, flights through other objects, and crossing flights.", "调整飞入角度，避免音符在画面里停留太短、飞行路线穿过其他物件，以及飞行路线互相交叉。");

        public static LocalisableString MinOnScreenTime => text("Minimum time on screen (ms)", "最短画面内时间（毫秒）");
        public static LocalisableString MinOnScreenTimeHint => text("Notes that would be on screen for less than this are penalised by the trajectory optimiser, so combos near an edge enter from the far side instead.", "优化器会惩罚在画面里停留短于这个时间的音符，所以靠近屏幕边缘的连击会改为从较远的一侧飞入。");

        public static LocalisableString OnScreenTimeStrength => text("Time on screen penalty strength", "画面内时间惩罚强度");
        public static LocalisableString OnScreenTimeStrengthHint => text("How strongly the optimiser avoids notes that are on screen too briefly. Higher: fewer short notes, but more turning and more flights through other objects. 0 turns it off.", "优化器避免音符在画面里停留过短的力度。越高，停留过短的音符越少，但转向越多，穿过其他物件的飞行也越多。0 为关闭。");

        public static LocalisableString FreeEntryDirection => text("Free entry direction", "自由飞入方向");
        public static LocalisableString FreeEntryDirectionHint => text("Let the optimiser send any note in from any side, also within one combo. Off: notes always enter from above (within 75 degrees of the flight direction).", "允许优化器让任何音符从任何一侧飞入，同一连击内也可以。关闭后音符始终从上方飞入（与飞行方向相差不超过 75°）。");

        public static LocalisableString OptimiserSearch => text("Optimiser search", "优化器搜索方式");
        public static LocalisableString OptimiserSearchHint => text(
            "Single notes: each note on its own. Whole combos: a combo is one fan, set by a centre direction and a width (keeps the combo range best). Whole combos, then single notes: the same, then small adjustments per note (lowest total cost in my measurements).",
            "逐个音符：每个音符单独搜索。整个连击：把一个连击当作一把扇子，由中心方向和宽度两个数决定（最能保持连击的角度范围）。整个连击，再逐个音符：先按连击拟合，再对每个音符做小幅微调（在我的测量中总代价最低）。");

        public static LocalisableString ComboAngleRange => text("Combo entry angle range (degrees)", "连击飞入角度范围（度）");
        public static LocalisableString ComboAngleRangeHint => text("Notes of one combo should enter within about this many degrees of each other.", "同一连击的音符的飞入角度，彼此相差大约不超过这个度数。");

        public static LocalisableString FullAngleRangeDistance => text("Full angle range distance", "完整角度范围所需距离");
        public static LocalisableString FullAngleRangeDistanceHint => text("Neighbouring notes of a combo may use the full angle range only when at least this far apart (osu! pixels). Closer notes are allowed proportionally less. 0 turns it off.", "连击里相邻的两个音符，只有相距至少这么远（osu! 像素）才可以使用完整的角度范围；更近的按比例允许更小的角度差。0 为关闭。");

        public static LocalisableString FullAngleRangeInterval => text("Full angle range interval (ms)", "完整角度范围所需间隔（毫秒）");
        public static LocalisableString FullAngleRangeIntervalHint => text("...and at least this far apart in time. Faster notes are allowed proportionally less. 0 turns it off.", "并且相隔至少这么长的时间；更快的按比例允许更小的角度差。0 为关闭。");

        public static LocalisableString OutwardFanStrength => text("Outward fan strength", "向外散开强度");
        public static LocalisableString OutwardFanStrengthHint => text("How strongly neighbouring notes of a combo are made to fan outwards (their flight lines move apart towards the screen edge). 0 turns it off.", "连击里相邻的音符被推向向外散开的力度（飞行线越靠近屏幕边缘分得越开）。0 为关闭。");

        public static LocalisableString ComboAnglePenalty => text("Combo angle difference penalty", "连击角度差惩罚");
        public static LocalisableString ComboAnglePenaltyHint => text("How strongly the optimiser avoids entry directions that differ too much within a combo. 0 turns it off.", "优化器避免同一连击内飞入方向相差过大的力度。0 为关闭。");

        public static LocalisableString AxisAlignmentReward => text("Horizontal / vertical alignment reward", "水平/竖直对齐奖励");
        public static LocalisableString AxisAlignmentRewardHint => text("How strongly the optimiser prefers combos whose average entry direction is horizontal or vertical. 0 turns it off.", "优化器偏好平均飞入方向为水平或竖直的连击的力度。0 为关闭。");

        public static LocalisableString NoteMaterial => text("Note material", "音符材质");

        // names of the options in the drop-downs (used through [LocalisableDescription] on the enum members)
        public static LocalisableString DirectionVertical => text("Vertical", "竖直");
        public static LocalisableString DirectionFan => text("Fan", "扇形");
        public static LocalisableString MaterialClear => text("Clear", "清晰");
        public static LocalisableString MaterialSoft => text("Soft", "柔和");
        public static LocalisableString SearchSingleNotes => text("Single notes", "逐个音符");
        public static LocalisableString SearchWholeCombos => text("Whole combos", "整个连击");
        public static LocalisableString SearchWholeCombosThenNotes => text("Whole combos, then single notes", "整个连击，再逐个音符");
    }
}
