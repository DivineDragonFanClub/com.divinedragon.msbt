using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#nullable enable

namespace DivineDragon.Msbt
{
    public static class MsbtScript
    {
        public static List<MsbtToken> ParseTokens(IReadOnlyList<ushort> contents)
        {
            if (contents == null)
            {
                throw new ArgumentNullException(nameof(contents));
            }

            var scanner = new MsbtScanner(contents);
            var tokens = new List<MsbtToken>();
            while (!scanner.AtEnd)
            {
                var next = scanner.Peek();
                if (next == 0x0)
                {
                    break;
                }

                if (next == 0xA)
                {
                    scanner.Next();
                    tokens.Add(NewLineToken.Instance);
                    continue;
                }

                if (next == 0xF)
                {
                    throw new InvalidOperationException("Encountered unexpected 0xF terminator in MSBT payload.");
                }

                if (next == 0xE)
                {
                    tokens.Add(ParseCommand(scanner));
                    continue;
                }

                tokens.Add(new PlainTextToken(scanner.NextString()));
            }

            return tokens;
        }

        public static string PrettyPrintTokenizedEntry(IEnumerable<MsbtToken> tokens)
        {
            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }

            var builder = new StringBuilder();
            PrettyPrintTokens(builder, tokens);
            return builder.ToString();
        }

        public static ushort[] PackTokens(IEnumerable<MsbtToken> tokens)
        {
            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }

            var output = new List<ushort>();
            foreach (var token in tokens)
            {
                switch (token.Kind)
                {
                    case MsbtTokenKind.PlainText:
                        var text = (PlainTextToken)token;
                        output.AddRange(text.Text.Select(c => (ushort)c));
                        break;
                    case MsbtTokenKind.NewLine:
                        output.Add(0xA);
                        break;
                    case MsbtTokenKind.Arg:
                        var arg = (ArgToken)token;
                        output.Add(0xE);
                        output.Add(0x1);
                        output.Add(arg.Value);
                        output.Add(0);
                        break;
                    case MsbtTokenKind.TalkType:
                        var talkType = (TalkTypeToken)token;
                        new CommandPacker(0x2, talkType.TalkType)
                            .String(talkType.Unknown)
                            .Pack(output);
                        break;
                    case MsbtTokenKind.Window:
                        var window = (WindowToken)token;
                        new CommandPacker(0x3, window.WindowType)
                            .String(window.Speaker)
                            .String(window.Variation)
                            .Pack(output);
                        break;
                    case MsbtTokenKind.Window2:
                        var window2 = (Window2Token)token;
                        new CommandPacker(0x3, window2.WindowType)
                            .Pack(output);
                        break;
                    case MsbtTokenKind.Wait:
                        var wait = (WaitToken)token;
                        output.Add(0xE);
                        output.Add(0x4);
                        output.Add(wait.WaitType);
                        if (wait.Duration.HasValue)
                        {
                            output.Add(4);
                            var value = wait.Duration.Value;
                            output.Add((ushort)(value & 0xFFFF));
                            output.Add((ushort)((value >> 16) & 0xFFFF));
                        }
                        else
                        {
                            output.Add(0);
                        }
                        break;
                    case MsbtTokenKind.Animation:
                        var animation = (AnimationToken)token;
                        new CommandPacker(0x5, animation.AnimationType)
                            .String(animation.Target)
                            .String(animation.Animation)
                            .Pack(output);
                        break;
                    case MsbtTokenKind.Alias:
                        var aliasToken = (AliasToken)token;
                        new CommandPacker(0x6, 0x0)
                            .String(aliasToken.Actual)
                            .String(aliasToken.Displayed)
                            .Pack(output);
                        break;
                    case MsbtTokenKind.PlayerName:
                        output.Add(0xE);
                        output.Add(0x6);
                        output.Add(0x3);
                        output.Add(0x0);
                        break;
                    case MsbtTokenKind.MascotName:
                        output.Add(0xE);
                        output.Add(0x6);
                        output.Add(0x5);
                        output.Add(0x0);
                        break;
                    case MsbtTokenKind.Fade:
                        var fade = (FadeToken)token;
                        new CommandPacker(0x7, fade.FadeType)
                            .Int32(fade.Duration)
                            .OptionalInt16(fade.Unknown)
                            .Pack(output);
                        break;
                    case MsbtTokenKind.Icon:
                        var icon = (IconToken)token;
                        new CommandPacker(0x8, 0x2)
                            .String(icon.Name)
                            .Pack(output);
                        break;
                    case MsbtTokenKind.Localize:
                        var localize = (LocalizeToken)token;
                        new CommandPacker(0xA, localize.LocalizeType)
                            .String(localize.Option1)
                            .String(localize.Option2)
                            .Pack(output);
                        break;
                    case MsbtTokenKind.Localize2:
                        var localize2 = (Localize2Token)token;
                        new CommandPacker(0xA, localize2.LocalizeType)
                            .Pack(output);
                        break;
                    case MsbtTokenKind.PictureShow:
                        var pictureShow = (PictureShowToken)token;
                        new CommandPacker(0xB, 0x0)
                            .Int32(pictureShow.Unknown)
                            .String(pictureShow.Picture)
                            .String(pictureShow.Function)
                            .Pack(output);
                        break;
                    case MsbtTokenKind.PictureHide:
                        var pictureHide = (PictureHideToken)token;
                        new CommandPacker(0xB, 0x1)
                            .Int32(pictureHide.Unknown)
                            .String(pictureHide.Function)
                            .Pack(output);
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported token type '{token.GetType().Name}'.");
                }
            }

            output.Add(0);
            return output.ToArray();
        }

        public static string ConvertEntriesToScript(IReadOnlyDictionary<string, IList<MsbtToken>> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            var builder = new StringBuilder();
            foreach (var entry in entries)
            {
                builder.Append('[').Append(entry.Key).AppendLine("]");
                PrettyPrintTokens(builder, entry.Value);
                builder.AppendLine().AppendLine();
            }

            return builder.ToString();
        }

        private static MsbtToken ParseCommand(MsbtScanner scanner)
        {
            scanner.Next(); // consume 0xE
            var commandId = scanner.Next();
            var subId = scanner.Next();
            var length = scanner.Next();
            switch (commandId)
            {
                case 1:
                    scanner.ExpectLength(length, 0);
                    return new ArgToken(subId);
                case 2:
                    return new TalkTypeToken(subId, subId == 0 ? scanner.NextStringParameter() : null);
                case 3:
                    if (subId < 8)
                    {
                        var speaker = scanner.NextStringParameter();
                        string? variation = null;
                        if (subId == 0 || subId == 3)
                        {
                            variation = scanner.NextStringParameter();
                        }
                        return new WindowToken(subId, speaker, variation);
                    }

                    scanner.Skip(length);
                    return new Window2Token(subId);
                case 4:
                    return new WaitToken(subId, subId == 3 ? (uint?)scanner.NextUInt32() : null);
                case 5:
                    var target = scanner.NextStringParameter();
                    var animation = scanner.NextStringParameter();
                    return new AnimationToken(subId, target, animation);
                case 6:
                    if (subId == 0)
                    {
                        var actual = scanner.NextStringParameter();
                        var displayed = scanner.NextStringParameter();
                        return new AliasToken(actual, displayed);
                    }

                    if (subId == 3)
                    {
                        return PlayerNameToken.Instance;
                    }

                    if (subId == 5)
                    {
                        return MascotNameToken.Instance;
                    }

                    throw new InvalidOperationException($"Unknown name command subtype {subId}.");
                case 7:
                    if (subId > 1)
                    {
                        throw new InvalidOperationException($"Unsupported fade type {subId}.");
                    }
                    var duration = scanner.NextUInt32();
                    var unknown = subId == 1 ? (ushort?)scanner.Next() : null;
                    return new FadeToken(subId, duration, unknown);
                case 8:
                    if (subId != 2)
                    {
                        throw new InvalidOperationException($"Expected icon subtype 2, found {subId}.");
                    }

                    return new IconToken(scanner.NextStringParameter());
                case 10:
                    if (subId == 2 || subId == 3)
                    {
                        return new Localize2Token(subId);
                    }

                    var option1 = scanner.NextStringParameter();
                    var option2 = scanner.NextStringParameter();
                    return new LocalizeToken(subId, option1, option2);
                case 11:
                    if (subId > 1)
                    {
                        throw new InvalidOperationException($"Unsupported picture subtype {subId}.");
                    }

                    var unknown32 = scanner.NextUInt32();
                    if (subId == 0)
                    {
                        var picture = scanner.NextStringParameter();
                        var function = scanner.NextStringParameter();
                        return new PictureShowToken(unknown32, picture, function);
                    }
                    else
                    {
                        var function = scanner.NextStringParameter();
                        return new PictureHideToken(unknown32, function);
                    }
                default:
                    throw new InvalidOperationException($"Unknown command {commandId}.");
            }
        }

        private static void PrettyPrintTokens(StringBuilder builder, IEnumerable<MsbtToken> tokens)
        {
            foreach (var token in tokens)
            {
                switch (token.Kind)
                {
                    case MsbtTokenKind.PlainText:
                        builder.Append(((PlainTextToken)token).Text);
                        break;
                    case MsbtTokenKind.NewLine:
                        builder.Append('\n');
                        break;
                    case MsbtTokenKind.Arg:
                        builder.Append("$Arg(").Append(((ArgToken)token).Value).Append(')');
                        break;
                    case MsbtTokenKind.TalkType:
                        var talkType = (TalkTypeToken)token;
                        if (talkType.Unknown != null)
                        {
                            builder.Append("$Type(").Append(talkType.TalkType).Append(", \"").Append(talkType.Unknown).Append("\")");
                        }
                        else
                        {
                            builder.Append("$Type(").Append(talkType.TalkType).Append(')');
                        }
                        break;
                    case MsbtTokenKind.Window:
                        var window = (WindowToken)token;
                        builder.Append("$Window(")
                            .Append(window.WindowType)
                            .Append(", \"")
                            .Append(window.Speaker);
                        if (window.Variation != null)
                        {
                            builder.Append("\", \"").Append(window.Variation).Append("\")");
                        }
                        else
                        {
                            builder.Append("\")");
                        }
                        break;
                    case MsbtTokenKind.Window2:
                        builder.Append("$Window2(").Append(((Window2Token)token).WindowType).Append(')');
                        break;
                    case MsbtTokenKind.Wait:
                        var wait = (WaitToken)token;
                        if (wait.Duration.HasValue)
                        {
                            builder.Append("$Wait(").Append(wait.WaitType).Append(", ").Append(wait.Duration.Value).Append(')');
                        }
                        else
                        {
                            builder.Append("$Wait(").Append(wait.WaitType).Append(')');
                        }
                        break;
                    case MsbtTokenKind.Animation:
                        var anim = (AnimationToken)token;
                        builder.Append("$Anim(")
                            .Append(anim.AnimationType)
                            .Append(", \"")
                            .Append(anim.Target)
                            .Append("\", \"")
                            .Append(anim.Animation)
                            .Append("\")");
                        break;
                    case MsbtTokenKind.Alias:
                        var aliasToken = (AliasToken)token;
                        builder.Append("$Alias(\"")
                            .Append(aliasToken.Actual)
                            .Append("\", \"")
                            .Append(aliasToken.Displayed)
                            .Append("\")");
                        break;
                    case MsbtTokenKind.PlayerName:
                        builder.Append("$P");
                        break;
                    case MsbtTokenKind.MascotName:
                        builder.Append("$M");
                        break;
                    case MsbtTokenKind.Fade:
                        var fade = (FadeToken)token;
                        builder.Append("$Fade(")
                            .Append(fade.FadeType)
                            .Append(", ")
                            .Append(fade.Duration);
                        if (fade.Unknown.HasValue)
                        {
                            builder.Append(", ").Append(fade.Unknown.Value);
                        }
                        builder.Append(')');
                        break;
                    case MsbtTokenKind.Icon:
                        builder.Append("$Icon(\"").Append(((IconToken)token).Name).Append("\")");
                        break;
                    case MsbtTokenKind.Localize:
                        var localize = (LocalizeToken)token;
                        builder.Append("$G(\"")
                            .Append(localize.Option1)
                            .Append("\", \"")
                            .Append(localize.Option2);
                        if (localize.LocalizeType == 0)
                        {
                            builder.Append("\")");
                        }
                        else
                        {
                            builder.Append("\", ")
                                .Append(localize.LocalizeType)
                                .Append(')');
                        }
                        break;
                    case MsbtTokenKind.Localize2:
                        builder.Append("$G2(").Append(((Localize2Token)token).LocalizeType).Append(')');
                        break;
                    case MsbtTokenKind.PictureShow:
                        var show = (PictureShowToken)token;
                        builder.Append("$Show(")
                            .Append(show.Unknown)
                            .Append(", \"")
                            .Append(show.Picture)
                            .Append("\", \"")
                            .Append(show.Function)
                            .Append("\")");
                        break;
                    case MsbtTokenKind.PictureHide:
                        var hide = (PictureHideToken)token;
                        builder.Append("$Hide(")
                            .Append(hide.Unknown)
                            .Append(", \"")
                            .Append(hide.Function)
                            .Append("\")");
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported token '{token.GetType().Name}'.");
                }
            }
        }

        private sealed class MsbtScanner
        {
            private readonly IReadOnlyList<ushort> _slice;
            private int _position;

            public MsbtScanner(IReadOnlyList<ushort> slice)
            {
                _slice = slice;
                _position = 0;
            }

            public bool AtEnd => _position >= _slice.Count;

            public ushort Peek()
            {
                if (_position >= _slice.Count)
                {
                    throw new InvalidOperationException("Reached end of stream while parsing MSBT.");
                }

                return _slice[_position];
            }

            public ushort Next()
            {
                var value = Peek();
                _position++;
                return value;
            }

            public uint NextUInt32()
            {
                var low = Next();
                var high = Next();
                return (uint)(low | (high << 16));
            }

            public void Skip(ushort bytes)
            {
                var chars = bytes / 2;
                _position = Math.Min(_slice.Count, _position + chars);
            }

            public void ExpectLength(ushort actual, ushort expected)
            {
                if (actual != expected)
                {
                    throw new InvalidOperationException($"Expected command payload length {expected}, found {actual}.");
                }
            }

            public string NextString()
            {
                var start = _position;
                while (!AtEnd && !IsCommandBoundary(Peek()))
                {
                    _position++;
                }

                return ToString(start, _position - start);
            }

            public string NextStringParameter()
            {
                var length = Next() >> 1;
                if (_position + length > _slice.Count)
                {
                    throw new InvalidOperationException("String parameter exceeded buffer length.");
                }

                var value = ToString(_position, length);
                _position += length;
                return value;
            }

            private string ToString(int start, int length)
            {
                var chars = new char[length];
                for (var i = 0; i < length; i++)
                {
                    chars[i] = (char)_slice[start + i];
                }
                return new string(chars);
            }

            private static bool IsCommandBoundary(ushort value)
            {
                return value == 0xA || value == 0xE || value == 0xF || value == 0x0;
            }
        }

        private sealed class CommandPacker
        {
            private readonly ushort _id;
            private readonly ushort _subId;
            private readonly List<Action<List<ushort>>> _args = new List<Action<List<ushort>>>();

            public CommandPacker(ushort id, ushort subId)
            {
                _id = id;
                _subId = subId;
            }

            public CommandPacker Int32(uint value)
            {
                _args.Add(output =>
                {
                    output.Add((ushort)(value & 0xFFFF));
                    output.Add((ushort)((value >> 16) & 0xFFFF));
                });
                return this;
            }

            public CommandPacker OptionalInt16(ushort? value)
            {
                if (value.HasValue)
                {
                    var copy = value.Value;
                    _args.Add(output => output.Add(copy));
                }
                return this;
            }

            public CommandPacker String(string? value)
            {
                if (value != null)
                {
                    _args.Add(output =>
                    {
                        var index = output.Count;
                        output.Add(0); // placeholder for length
                        foreach (var c in value)
                        {
                            output.Add((ushort)c);
                        }
                        output[index] = (ushort)((output.Count - index - 1) * 2);
                    });
                }
                return this;
            }

            public void Pack(List<ushort> output)
            {
                output.Add(0xE);
                output.Add(_id);
                output.Add(_subId);
                output.Add(0);
                var lengthIndex = output.Count - 1;
                foreach (var arg in _args)
                {
                    arg(output);
                }
                output[lengthIndex] = (ushort)((output.Count - lengthIndex - 1) * 2);
            }
        }
    }
}
