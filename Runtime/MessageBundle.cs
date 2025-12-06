using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DivineDragon.Msbt.Scripting;

namespace DivineDragon.Msbt
{
    /// <summary>
    /// Thin wrapper around the MSBT MessageMap. By default we assume the Unity bundle has already been unpacked
    /// into a raw .bytes file, so this class focuses purely on MSBT parsing.
    /// </summary>
    public sealed class MessageBundle
    {
        private readonly MessageMap _messageMap;

        private MessageBundle(MessageMap messageMap)
        {
            _messageMap = messageMap ?? throw new ArgumentNullException(nameof(messageMap));
        }

        public static MessageBundle Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path must not be empty.", nameof(path));
            }

            var messageMap = MessageMap.Load(path);
            return new MessageBundle(messageMap);
        }

        public IReadOnlyDictionary<string, ushort[]> Entries => _messageMap.Messages;

        public bool TryGetEntry(string label, out ushort[] data)
        {
            if (label == null)
            {
                throw new ArgumentNullException(nameof(label));
            }

            return _messageMap.Messages.TryGetValue(label, out data!);
        }

        public Dictionary<string, string> ExtractEntriesAsText()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in _messageMap.Messages)
            {
                var tokens = MsbtScript.ParseTokens(entry.Value);
                result[entry.Key] = MsbtScript.PrettyPrintTokenizedEntry(tokens);
            }

            return result;
        }

        public string ToAstraScript()
        {
            var tokenized = _messageMap.Messages.ToDictionary(
                kvp => kvp.Key,
                kvp => (IList<MsbtToken>)MsbtScript.ParseTokens(kvp.Value),
                StringComparer.Ordinal);
            return MsbtScript.ConvertEntriesToScript(tokenized);
        }

        public void ReplaceScript(string script)
        {
            var packed = AstraScript.PackScript(script);
            _messageMap.ReplaceMessages(packed);
        }

        public void ReplaceEntries(IDictionary<string, string> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            var packed = new Dictionary<string, ushort[]>(entries.Count, StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                var tokens = AstraScript.ParseEntry(entry.Value);
                packed[entry.Key] = MsbtScript.PackTokens(tokens);
            }

            _messageMap.ReplaceMessages(packed);
        }

        public byte[] Serialize()
        {
            return _messageMap.Serialize();
        }

        public void Save(string path)
        {
            _messageMap.Save(path);
        }
    }
}
