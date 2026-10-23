using Godot;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SatoriCharacter;

internal static class SpellCardBanner
{
    private static Control? _overlay;
    private static Tween? _tween;

    internal static void Hide()
    {
        if (_tween != null && GodotObject.IsInstanceValid(_tween)) _tween.Kill();
        _tween = null;
        if (_overlay != null && GodotObject.IsInstanceValid(_overlay))
        {
            _overlay.Hide();
            _overlay.QueueFree();
        }
        _overlay = null;
    }

    internal static void Show(NCombatRoom room, string title)
    {
        Hide();
        var parent = room.Ui;
        if (parent == null || !GodotObject.IsInstanceValid(parent)) return;
        var view = parent.Size;
        if (view.X <= 0 || view.Y <= 0) view = room.GetViewportRect().Size;
        float scale = Math.Clamp(view.X / 1920f, 0.5f, 1.5f);
        float width = Math.Min(view.X * 0.86f, 1320f * scale);
        float height = 120f * scale;
        var center = new Vector2(view.X / 2f, view.Y * 0.43f);

        var overlay = MakeControl("SatoriSpellBanner");
        overlay.ZIndex = 30;
        parent.AddChild(overlay);
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _overlay = overlay;
        var ribbon = MakeControl("Ribbon");
        ribbon.Size = new Vector2(width, height);
        ribbon.Position = center - ribbon.Size / 2f - new Vector2(56f * scale, 0);
        ribbon.Modulate = new Color(1, 1, 1, 0);
        overlay.AddChild(ribbon);

        AddGradient(ribbon, new Color("32162ee0"), Vector2.Zero, ribbon.Size);
        AddGradient(ribbon, new Color("e596bfcc"), new Vector2(0, 4f * scale), new Vector2(width, 2f * scale));
        AddGradient(ribbon, new Color("e596bf99"), new Vector2(0, height - 6f * scale), new Vector2(width, 2f * scale));

        var label = new Label
        {
            Text = title,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            FocusMode = Control.FocusModeEnum.None,
            ClipText = true
        };
        ribbon.AddChild(label);
        label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        // Reuse the game's CJK font, without shipping another font file.
        var font = FontManager.GetSubstituteFont(SatoriConfig.UseJapaneseSpellNames ? "jpn" : "zhs", FontType.Bold);
        int fontSize = (int)MathF.Round(46f * scale * SatoriConfig.SpellBannerScale);
        if (font != null)
        {
            label.AddThemeFontOverride("font", font);
            float textWidth = font.GetStringSize(title, HorizontalAlignment.Left, -1, fontSize).X;
            if (textWidth > width - 80f * scale)
                fontSize = Math.Max(16, (int)(fontSize * (width - 80f * scale) / textWidth));
        }
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", new Color("fff0f8"));
        label.AddThemeColorOverride("font_outline_color", new Color("291027"));
        label.AddThemeConstantOverride("outline_size", Math.Max(2, (int)(5f * scale)));
        label.AddThemeColorOverride("font_shadow_color", new Color("b2548999"));
        label.AddThemeConstantOverride("shadow_offset_y", Math.Max(1, (int)(3f * scale)));

        double duration = SatoriConfig.SpellBannerDuration;
        double enter = Math.Min(0.22, duration * 0.2);
        double exit = Math.Min(0.3, duration * 0.25);
        float restingX = center.X - width / 2f;
        _tween = overlay.CreateTween();
        _tween.TweenProperty(ribbon, "modulate:a", 1f, enter);
        _tween.Parallel().TweenProperty(ribbon, "position:x", restingX, enter)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _tween.TweenProperty(ribbon, "position:x", restingX + 12f * scale, duration - enter - exit);
        _tween.TweenProperty(ribbon, "modulate:a", 0f, exit);
        _tween.Parallel().TweenProperty(ribbon, "position:x", restingX + 70f * scale, exit)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        _tween.TweenCallback(Callable.From(() =>
        {
            overlay.QueueFree();
            if (_overlay == overlay) { _overlay = null; _tween = null; }
        }));
    }

    private static Control MakeControl(string name) => new()
    {
        Name = name,
        MouseFilter = Control.MouseFilterEnum.Ignore,
        FocusMode = Control.FocusModeEnum.None
    };

    private static void AddGradient(Control parent, Color color, Vector2 position, Vector2 size)
    {
        var gradient = new Gradient
        {
            Offsets = new[] { 0f, 0.2f, 0.8f, 1f },
            Colors = new[] { new Color(color, 0), color, color, new Color(color, 0) }
        };
        parent.AddChild(new TextureRect
        {
            Texture = new GradientTexture2D { Gradient = gradient, Width = 64, Height = 2, FillFrom = Vector2.Zero, FillTo = Vector2.Right },
            Position = position,
            Size = size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            FocusMode = Control.FocusModeEnum.None
        });
    }
}
