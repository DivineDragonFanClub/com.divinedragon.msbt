using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
#nullable enable

using DivineDragon.Msbt;

namespace DivineDragon.Msbt.Scripting
{
    public static class AstraScript
    {
        public static Dictionary<string, IList<MsbtToken>> Parse(string source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var parser = new Parser(source);
            var entries = new Dictionary<string, IList<MsbtToken>>(StringComparer.Ordinal);
            while (!parser.AtEnd)
            {
                var (key, tokens) = parser.NextKeyedEntry();
                entries[key] = tokens;
            }

            return entries;
        }

        public static IList<MsbtToken> ParseEntry(string source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var parser = new Parser(source);
            return parser.NextEntry();
        }

        public static Dictionary<string, ushort[]> PackScript(string source)
        {
            var parsed = Parse(source);
            return parsed.ToDictionary(
                kvp => kvp.Key,
                kvp => MsbtScript.PackTokens(kvp.Value),
                StringComparer.Ordinal);
        }

        public static string ConvertEntriesToScript(IReadOnlyDictionary<string, string> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            var builder = new StringBuilder();
            foreach (var entry in entries)
            {
                builder.Append('[').Append(entry.Key).AppendLine("]");
                builder.AppendLine(entry.Value);
                builder.AppendLine();
            }

            return builder.ToString();
        }

        public static Dictionary<string, string> ConvertScriptToEntries(string source)
        {
            var parsed = Parse(source);
            var output = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in parsed)
            {
                output[entry.Key] = MsbtScript.PrettyPrintTokenizedEntry(entry.Value);
            }

            return output;
        }

        private sealed class Parser
        {
            private readonly string _source;
            private readonly PeekableLexer _lexer;

            public Parser(string source)
            {
                _source = source;
                _lexer = new PeekableLexer(new Lexer(_source));
            }

            public bool AtEnd => !_lexer.Peek().HasValue;

            public (string Key, IList<MsbtToken> Tokens) NextKeyedEntry()
            {
                var key = ExpectIdentifier();
                Expect(TokenKind.NewLine);
                var entry = NextEntry();
                ConsumeTrailingNewLines(entry);
                return (key, entry);
            }

            public IList<MsbtToken> NextEntry()
            {
                var commands = new List<MsbtToken>();
                while (!AtEnd)
                {
                    var peeked = _lexer.Peek();
                    if (!peeked.HasValue || peeked.Value.Kind == TokenKind.Identifier)
                    {
                        break;
                    }

                    var token = Next();
                    switch (token.Kind)
                    {
                        case TokenKind.Arg:
                            Expect(TokenKind.LeftParen);
                            var argValue = ExpectNumber();
                            Expect(TokenKind.RightParen);
                            commands.Add(new ArgToken((ushort)argValue));
                            break;
                        case TokenKind.Type:
                            Expect(TokenKind.LeftParen);
                            var talkType = (ushort)ExpectNumber();
                            SkipWhitespace();
                            var unknown = NextOptional(ExpectString);
                            Expect(TokenKind.RightParen);
                            commands.Add(new TalkTypeToken(talkType, unknown));
                            break;
                        case TokenKind.Window:
                            Expect(TokenKind.LeftParen);
                            var windowType = (ushort)ExpectNumber();
                            Expect(TokenKind.Comma);
                            var speaker = ExpectString();
                            var variation = NextOptional(ExpectString);
                            Expect(TokenKind.RightParen);
                            commands.Add(new WindowToken(windowType, speaker, variation));
                            break;
                        case TokenKind.Window2:
                            Expect(TokenKind.LeftParen);
                            var windowType2 = (ushort)ExpectNumber();
                            Expect(TokenKind.RightParen);
                            commands.Add(new Window2Token(windowType2));
                            break;
                        case TokenKind.Wait:
                            Expect(TokenKind.LeftParen);
                            var waitType = (ushort)ExpectNumber();
                            var duration = NextOptional(() => (uint)ExpectNumber());
                            Expect(TokenKind.RightParen);
                            commands.Add(new WaitToken(waitType, duration));
                            break;
                        case TokenKind.Anim:
                            Expect(TokenKind.LeftParen);
                            var animType = (ushort)ExpectNumber();
                            Expect(TokenKind.Comma);
                            var target = ExpectString();
                            Expect(TokenKind.Comma);
                            var animation = ExpectString();
                            Expect(TokenKind.RightParen);
                            commands.Add(new AnimationToken(animType, target, animation));
                            break;
                        case TokenKind.Alias:
                            Expect(TokenKind.LeftParen);
                            var actual = ExpectString();
                            Expect(TokenKind.Comma);
                            var displayed = ExpectString();
                            Expect(TokenKind.RightParen);
                            commands.Add(new AliasToken(actual, displayed));
                            break;
                        case TokenKind.PlayerName:
                            commands.Add(PlayerNameToken.Instance);
                            break;
                        case TokenKind.MascotName:
                            commands.Add(MascotNameToken.Instance);
                            break;
                        case TokenKind.Fade:
                            Expect(TokenKind.LeftParen);
                            var fadeType = (ushort)ExpectNumber();
                            Expect(TokenKind.Comma);
                            var fadeDuration = (uint)ExpectNumber();
                            var fadeUnknown = NextOptional(() => (ushort)ExpectNumber());
                            Expect(TokenKind.RightParen);
                            commands.Add(new FadeToken(fadeType, fadeDuration, fadeUnknown));
                            break;
                        case TokenKind.Icon:
                            Expect(TokenKind.LeftParen);
                            var icon = ExpectString();
                            Expect(TokenKind.RightParen);
                            commands.Add(new IconToken(icon));
                            break;
                        case TokenKind.Localize:
                            Expect(TokenKind.LeftParen);
                            var option1 = ExpectString();
                            Expect(TokenKind.Comma);
                            var option2 = ExpectString();
                            var localizeType = NextOptional(() => (ushort)ExpectNumber()) ?? (ushort)0;
                            Expect(TokenKind.RightParen);
                            commands.Add(new LocalizeToken(localizeType, option1, option2));
                            break;
                        case TokenKind.Localize2:
                            Expect(TokenKind.LeftParen);
                            var localizeType2 = (ushort)ExpectNumber();
                            Expect(TokenKind.RightParen);
                            commands.Add(new Localize2Token(localizeType2));
                            break;
                        case TokenKind.Show:
                            Expect(TokenKind.LeftParen);
                            var showUnknown = ExpectNumber();
                            Expect(TokenKind.Comma);
                            var picture = ExpectString();
                            Expect(TokenKind.Comma);
                            var function = ExpectString();
                            Expect(TokenKind.RightParen);
                            commands.Add(new PictureShowToken((uint)showUnknown, picture, function));
                            break;
                        case TokenKind.Hide:
                            Expect(TokenKind.LeftParen);
                            var hideUnknown = ExpectNumber();
                            Expect(TokenKind.Comma);
                            var hideFunction = ExpectString();
                            Expect(TokenKind.RightParen);
                            commands.Add(new PictureHideToken((uint)hideUnknown, hideFunction));
                            break;
                        case TokenKind.NewLine:
                            commands.Add(NewLineToken.Instance);
                            break;
                        case TokenKind.String:
                        case TokenKind.Text:
                        case TokenKind.Number:
                        case TokenKind.LeftParen:
                        case TokenKind.RightParen:
                        case TokenKind.Comma:
                            PushText(commands, token.Text);
                            break;
                        default:
                            throw CreateError(token, $"Unexpected token '{token.Kind}'.");
                    }
                }

                return commands;
            }

            private static void ConsumeTrailingNewLines(IList<MsbtToken> tokens)
            {
                for (var i = tokens.Count - 1; i >= 0; i--)
                {
                    if (tokens[i] is NewLineToken)
                    {
                        tokens.RemoveAt(i);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            private static void PushText(IList<MsbtToken> commands, string text)
            {
                if (commands.Count > 0 && commands[commands.Count - 1] is PlainTextToken existing)
                {
                    commands[commands.Count - 1] = new PlainTextToken(existing.Text + text);
                }
                else
                {
                    commands.Add(new PlainTextToken(text));
                }
            }

            private void SkipWhitespace()
            {
                var peeked = _lexer.Peek();
                if (peeked.HasValue && peeked.Value.Kind == TokenKind.Text && string.IsNullOrWhiteSpace(peeked.Value.Text))
                {
                    _lexer.Next();
                }
            }

            private T? NextOptional<T>(Func<T> func) where T : struct
            {
                var peeked = _lexer.Peek();
                if (peeked.HasValue && peeked.Value.Kind == TokenKind.Comma)
                {
                    _lexer.Next();
                    return func();
                }

                return null;
            }

            private string? NextOptional(Func<string> func)
            {
                var peeked = _lexer.Peek();
                if (peeked.HasValue && peeked.Value.Kind == TokenKind.Comma)
                {
                    _lexer.Next();
                    return func();
                }

                return null;
            }

            private uint ExpectNumber()
            {
                SkipWhitespace();
                var token = Next();
                if (token.Kind != TokenKind.Number)
                {
                    throw CreateError(token, $"Expected number, found '{token.Text}'.");
                }

                if (!uint.TryParse(token.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                {
                    if (int.TryParse(token.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var signed))
                    {
                        value = unchecked((uint)signed);
                    }
                    else
                    {
                        throw CreateError(token, $"Invalid number literal '{token.Text}'.");
                    }
                }

                return value;
            }

            private string ExpectString()
            {
                SkipWhitespace();
                var token = Next();
                if (token.Kind != TokenKind.String)
                {
                    throw CreateError(token, $"Expected string literal, found '{token.Text}'.");
                }

                return token.Text.Substring(1, token.Text.Length - 2);
            }

            private string ExpectIdentifier()
            {
                SkipWhitespace();
                var token = Next();
                if (token.Kind != TokenKind.Identifier)
                {
                    throw CreateError(token, $"Expected identifier, found '{token.Text}'.");
                }

                return token.Text.Substring(1, token.Text.Length - 2).Trim();
            }

            private void Expect(TokenKind kind)
            {
                SkipWhitespace();
                var token = Next();
                if (token.Kind != kind)
                {
                    throw CreateError(token, $"Expected {kind}, found '{token.Text}'.");
                }
            }

            private Token Next()
            {
                var token = _lexer.Next();
                if (!token.HasValue)
                {
                    throw new AstraScriptException("Unexpected end of file.", _source.Length, 0);
                }

                return token.Value;
            }

            private AstraScriptException CreateError(Token token, string message)
            {
                return new AstraScriptException(message, token.Start, token.Length);
            }
        }

        private sealed class PeekableLexer
        {
            private readonly Lexer _lexer;
            private Token? _peeked;

            public PeekableLexer(Lexer lexer)
            {
                _lexer = lexer;
            }

            public Token? Peek()
            {
                if (_peeked == null)
                {
                    _peeked = _lexer.NextToken();
                }

                return _peeked;
            }

            public Token? Next()
            {
                if (_peeked != null)
                {
                    var result = _peeked;
                    _peeked = null;
                    return result;
                }

                return _lexer.NextToken();
            }
        }

        private sealed class Lexer
        {
            private static readonly (string Literal, TokenKind Kind)[] KeywordTokens = new[]
            {
                ("$Window2", TokenKind.Window2),
                ("$Window", TokenKind.Window),
                ("$Show", TokenKind.Show),
                ("$Hide", TokenKind.Hide),
                ("$Alias", TokenKind.Alias),
                ("$Anim", TokenKind.Anim),
                ("$Type", TokenKind.Type),
                ("$Wait", TokenKind.Wait),
                ("$Icon", TokenKind.Icon),
                ("$Fade", TokenKind.Fade),
                ("$Arg", TokenKind.Arg),
                ("$G2", TokenKind.Localize2),
                ("$G", TokenKind.Localize),
                ("$P", TokenKind.PlayerName),
                ("$M", TokenKind.MascotName),
            };

            private static readonly char[] TextTerminators = { '\r', '\n', '[', '$', '(', ')', ',' };

            private readonly string _source;
            private int _position;

            public Lexer(string source)
            {
                _source = source;
                _position = 0;
            }

            public Token? NextToken()
            {
                if (_position >= _source.Length)
                {
                    return null;
                }

                var start = _position;
                var current = _source[_position];

                if (current == '\r' || current == '\n')
                {
                    if (current == '\r' && _position + 1 < _source.Length && _source[_position + 1] == '\n')
                    {
                        _position += 2;
                        return new Token(TokenKind.NewLine, "\r\n", start, 2);
                    }

                    _position++;
                    return new Token(TokenKind.NewLine, current.ToString(), start, 1);
                }

                if (char.IsWhiteSpace(current))
                {
                    var startWhitespace = _position;
                    while (_position < _source.Length)
                    {
                        var c = _source[_position];
                        if (c == '\r' || c == '\n' || !char.IsWhiteSpace(c))
                        {
                            break;
                        }
                        _position++;
                    }
                    var slice = _source.Substring(startWhitespace, _position - startWhitespace);
                    return new Token(TokenKind.Text, slice, startWhitespace, slice.Length);
                }

                if (current == '[')
                {
                    var index = _source.IndexOf(']', _position + 1);
                    if (index == -1)
                    {
                        return ReadTextToken();
                    }

                    var segment = _source.Substring(start, index - start + 1);
                    if (IsKeyIdentifier(segment))
                    {
                        _position = index + 1;
                        return new Token(TokenKind.Identifier, segment, start, segment.Length);
                    }
                    return ReadTextToken();
                }

                if (current == '(')
                {
                    _position++;
                    return new Token(TokenKind.LeftParen, "(", start, 1);
                }

                if (current == ')')
                {
                    _position++;
                    return new Token(TokenKind.RightParen, ")", start, 1);
                }

                if (current == ',')
                {
                    _position++;
                    return new Token(TokenKind.Comma, ",", start, 1);
                }

                if (current == '"')
                {
                    return ReadStringToken();
                }

                if (current == '$')
                {
                    foreach (var keyword in KeywordTokens)
                    {
                        var literal = keyword.Literal;
                        if (_position + literal.Length <= _source.Length &&
                            string.CompareOrdinal(_source, _position, literal, 0, literal.Length) == 0)
                        {
                            var kind = keyword.Kind;
                            _position += literal.Length;
                            return new Token(kind, literal, start, literal.Length);
                        }
                    }

                    return ReadTextToken();
                }

                if (current == '-' || char.IsDigit(current))
                {
                    return ReadNumberToken();
                }

                return ReadTextToken();
            }

            private Token ReadTextToken()
            {
                var start = _position;
                while (_position < _source.Length)
                {
                    var c = _source[_position];
                    if (TextTerminators.Contains(c))
                    {
                        break;
                    }

                    _position++;
                }

                if (_position == start)
                {
                    _position++;
                }

                var text = _source.Substring(start, _position - start);
                return new Token(TokenKind.Text, text, start, text.Length);
            }

            private Token ReadStringToken()
            {
                var start = _position;
                _position++; // skip opening quote
                while (_position < _source.Length)
                {
                    var c = _source[_position];
                    if (c == '"')
                    {
                        _position++;
                        var slice = _source.Substring(start, _position - start);
                        return new Token(TokenKind.String, slice, start, slice.Length);
                    }

                    if (c == '\r' || c == '\n')
                    {
                        break;
                    }

                    _position++;
                }

                // Unterminated string falls back to text token
                _position = start;
                return ReadTextToken();
            }

            private Token ReadNumberToken()
            {
                var start = _position;
                if (_source[_position] == '-')
                {
                    _position++;
                }

                while (_position < _source.Length && char.IsDigit(_source[_position]))
                {
                    _position++;
                }

                var text = _source.Substring(start, _position - start);
                return new Token(TokenKind.Number, text, start, text.Length);
            }

            private static bool IsKeyIdentifier(string value)
            {
                if (value.Length < 2 || value[0] != '[' || value[value.Length - 1] != ']')
                {
                    return false;
                }

                for (var i = 1; i < value.Length - 1; i++)
                {
                    var ch = value[i];
                    if (char.IsLetterOrDigit(ch) || ch == '#' || ch == '+' || ch == '_' || ch == '\'' || ch == '"' || char.IsWhiteSpace(ch))
                    {
                        continue;
                    }

                    return false;
                }

                return true;
            }
        }

        private readonly struct Token
        {
            public Token(TokenKind kind, string text, int start, int length)
            {
                Kind = kind;
                Text = text;
                Start = start;
                Length = length;
            }

            public TokenKind Kind { get; }

            public string Text { get; }

            public int Start { get; }

            public int Length { get; }
        }

        private enum TokenKind
        {
            LeftParen,
            RightParen,
            Comma,
            Arg,
            Type,
            Window,
            Window2,
            Wait,
            Anim,
            Alias,
            PlayerName,
            MascotName,
            Fade,
            Icon,
            Localize,
            Localize2,
            Show,
            Hide,
            NewLine,
            Number,
            String,
            Identifier,
            Text,
        }

        public sealed class AstraScriptException : Exception
        {
            public AstraScriptException(string message, int start, int length)
                : base(message)
            {
                Start = start;
                Length = length;
            }

            public int Start { get; }

            public int Length { get; }
        }
    }
}
