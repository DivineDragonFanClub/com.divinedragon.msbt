using System;

namespace DivineDragon.Msbt.Editor
{
    /// <summary>
    /// Identifier for one MSBT file inside a language tree, equal to the filename
    /// without extension (e.g., "GameData", "Patch0", "G001"). Wraps a string for
    /// type safety so callers can't accidentally pass it where a MessageId or raw
    /// path is expected.
    /// </summary>
    public readonly struct FileId : IEquatable<FileId>
    {
        public string Value { get; }

        public FileId(string value)
        {
            Value = value;
        }

        public static implicit operator string(FileId id) => id.Value;
        public static implicit operator FileId(string s) => new FileId(s);

        public bool Equals(FileId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is FileId other && Equals(other);
        public override int GetHashCode() => Value != null ? Value.GetHashCode() : 0;
        public override string ToString() => Value ?? "(null)";

        public static bool operator ==(FileId a, FileId b) => a.Equals(b);
        public static bool operator !=(FileId a, FileId b) => !a.Equals(b);
    }

    /// <summary>
    /// Identifier for one entry inside an MSBT file (e.g., "MTID_Plain", "MCID_G001").
    /// Wraps a string for type safety.
    /// </summary>
    public readonly struct MessageId : IEquatable<MessageId>
    {
        public string Value { get; }

        public MessageId(string value)
        {
            Value = value;
        }

        public static implicit operator string(MessageId id) => id.Value;
        public static implicit operator MessageId(string s) => new MessageId(s);

        public bool Equals(MessageId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is MessageId other && Equals(other);
        public override int GetHashCode() => Value != null ? Value.GetHashCode() : 0;
        public override string ToString() => Value ?? "(null)";

        public static bool operator ==(MessageId a, MessageId b) => a.Equals(b);
        public static bool operator !=(MessageId a, MessageId b) => !a.Equals(b);
    }

    /// <summary>
    /// Language code in Engage's country+lang format (e.g., "USen", "JPja"). Exposes
    /// a Country helper that returns the 2-char prefix used in directory naming.
    /// </summary>
    public readonly struct Language : IEquatable<Language>
    {
        public string Code { get; }

        public Language(string code)
        {
            Code = code;
        }

        public string Country =>
            string.IsNullOrEmpty(Code) || Code.Length < 2 ? null : Code.Substring(0, 2);

        public static implicit operator string(Language lang) => lang.Code;
        public static implicit operator Language(string s) => new Language(s);

        public bool Equals(Language other) => string.Equals(Code, other.Code, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is Language other && Equals(other);
        public override int GetHashCode() => Code != null ? Code.GetHashCode() : 0;
        public override string ToString() => Code ?? "(null)";

        public static bool operator ==(Language a, Language b) => a.Equals(b);
        public static bool operator !=(Language a, Language b) => !a.Equals(b);
    }
}
