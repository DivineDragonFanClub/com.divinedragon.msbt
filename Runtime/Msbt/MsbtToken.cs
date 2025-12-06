using System;

#nullable enable

namespace DivineDragon.Msbt
{
    public enum MsbtTokenKind
    {
        PlainText,
        NewLine,
        Arg,
        TalkType,
        Window,
        Window2,
        Wait,
        Animation,
        Alias,
        PlayerName,
        MascotName,
        Fade,
        Icon,
        Localize,
        Localize2,
        PictureShow,
        PictureHide,
    }

    public abstract class MsbtToken
    {
        public abstract MsbtTokenKind Kind { get; }
    }

    public sealed class PlainTextToken : MsbtToken
    {
        public PlainTextToken(string text)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        public string Text { get; }

        public override MsbtTokenKind Kind => MsbtTokenKind.PlainText;
    }

    public sealed class NewLineToken : MsbtToken
    {
        private NewLineToken() { }

        public static NewLineToken Instance { get; } = new NewLineToken();

        public override MsbtTokenKind Kind => MsbtTokenKind.NewLine;
    }

    public sealed class ArgToken : MsbtToken
    {
        public ArgToken(ushort value)
        {
            Value = value;
        }

        public ushort Value { get; }

        public override MsbtTokenKind Kind => MsbtTokenKind.Arg;
    }

    public sealed class TalkTypeToken : MsbtToken
    {
        public TalkTypeToken(ushort talkType, string? unknown)
        {
            TalkType = talkType;
            Unknown = unknown;
        }

        public ushort TalkType { get; }

        public string? Unknown { get; }

        public override MsbtTokenKind Kind => MsbtTokenKind.TalkType;
    }

    public sealed class WindowToken : MsbtToken
    {
        public WindowToken(ushort windowType, string speaker, string? variation)
        {
            WindowType = windowType;
            Speaker = speaker ?? throw new ArgumentNullException(nameof(speaker));
            Variation = variation;
        }

        public ushort WindowType { get; }

        public string Speaker { get; }

        public string? Variation { get; }

        public override MsbtTokenKind Kind => MsbtTokenKind.Window;
    }

    public sealed class Window2Token : MsbtToken
    {
        public Window2Token(ushort windowType)
        {
            WindowType = windowType;
        }

        public ushort WindowType { get; }

        public override MsbtTokenKind Kind => MsbtTokenKind.Window2;
    }

    public sealed class WaitToken : MsbtToken
    {
        public WaitToken(ushort waitType, uint? duration)
        {
            WaitType = waitType;
            Duration = duration;
        }

        public ushort WaitType { get; }

        public uint? Duration { get; }

        public override MsbtTokenKind Kind => MsbtTokenKind.Wait;
    }

    public sealed class AnimationToken : MsbtToken
    {
        public AnimationToken(ushort animationType, string target, string animation)
        {
            AnimationType = animationType;
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Animation = animation ?? throw new ArgumentNullException(nameof(animation));
        }

        public ushort AnimationType { get; }

        public string Target { get; }

        public string Animation { get; }

        public override MsbtTokenKind Kind => MsbtTokenKind.Animation;
    }

    public sealed class AliasToken : MsbtToken
    {
        public AliasToken(string actual, string displayed)
        {
            Actual = actual ?? throw new ArgumentNullException(nameof(actual));
            Displayed = displayed ?? throw new ArgumentNullException(nameof(displayed));
        }

        public string Actual { get; }

        public string Displayed { get; }

        public override MsbtTokenKind Kind => MsbtTokenKind.Alias;
    }

    public sealed class PlayerNameToken : MsbtToken
    {
        private PlayerNameToken() { }

        public static PlayerNameToken Instance { get; } = new PlayerNameToken();

        public override MsbtTokenKind Kind => MsbtTokenKind.PlayerName;
    }

    public sealed class MascotNameToken : MsbtToken
    {
        private MascotNameToken() { }

        public static MascotNameToken Instance { get; } = new MascotNameToken();

        public override MsbtTokenKind Kind => MsbtTokenKind.MascotName;
    }

    public sealed class FadeToken : MsbtToken
    {
        public FadeToken(ushort fadeType, uint duration, ushort? unknown)
        {
            FadeType = fadeType;
            Duration = duration;
            Unknown = unknown;
        }

        public ushort FadeType { get; }

        public uint Duration { get; }

        public ushort? Unknown { get; }

        public override MsbtTokenKind Kind => MsbtTokenKind.Fade;
    }

    public sealed class IconToken : MsbtToken
    {
        public IconToken(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public string Name { get; }

        public override MsbtTokenKind Kind => MsbtTokenKind.Icon;
    }

    public sealed class LocalizeToken : MsbtToken
    {
        public LocalizeToken(ushort localizeType, string option1, string option2)
        {
            LocalizeType = localizeType;
            Option1 = option1 ?? throw new ArgumentNullException(nameof(option1));
            Option2 = option2 ?? throw new ArgumentNullException(nameof(option2));
        }

        public ushort LocalizeType { get; }

        public string Option1 { get; }

        public string Option2 { get; }

        public override MsbtTokenKind Kind => MsbtTokenKind.Localize;
    }

    public sealed class Localize2Token : MsbtToken
    {
        public Localize2Token(ushort localizeType)
        {
            LocalizeType = localizeType;
        }

        public ushort LocalizeType { get; }

        public override MsbtTokenKind Kind => MsbtTokenKind.Localize2;
    }

    public sealed class PictureShowToken : MsbtToken
    {
        public PictureShowToken(uint unknown, string picture, string function)
        {
            Unknown = unknown;
            Picture = picture ?? throw new ArgumentNullException(nameof(picture));
            Function = function ?? throw new ArgumentNullException(nameof(function));
        }

        public uint Unknown { get; }

        public string Picture { get; }

        public string Function { get; }

        public override MsbtTokenKind Kind => MsbtTokenKind.PictureShow;
    }

    public sealed class PictureHideToken : MsbtToken
    {
        public PictureHideToken(uint unknown, string function)
        {
            Unknown = unknown;
            Function = function ?? throw new ArgumentNullException(nameof(function));
        }

        public uint Unknown { get; }

        public string Function { get; }

        public override MsbtTokenKind Kind => MsbtTokenKind.PictureHide;
    }
}
