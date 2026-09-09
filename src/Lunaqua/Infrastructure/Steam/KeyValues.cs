using System.Text;

namespace Lunaqua.Infrastructure.Steam;

/// <summary>Valve KeyValues（VDF / ACF）节点。</summary>
public sealed class KeyValues
{
    private readonly Dictionary<string, List<KeyValues>> _children = new(StringComparer.OrdinalIgnoreCase);

    public KeyValues(string? value = null) => Value = value;

    public string? Value { get; }

    public bool HasChildren => _children.Count > 0;

    public IEnumerable<KeyValues> Children(string key) =>
        _children.TryGetValue(key, out var list) ? list : [];

    public KeyValues? Child(string key) => Children(key).FirstOrDefault();

    public string? GetString(string key) => Child(key)?.Value;

    public IReadOnlyList<(string Key, KeyValues Node)> Entries() =>
        [.. _children.SelectMany(pair => pair.Value.Select(node => (pair.Key, node)))];

    internal void Add(string key, KeyValues node)
    {
        if (!_children.TryGetValue(key, out var list))
        {
            list = [];
            _children[key] = list;
        }

        list.Add(node);
    }
}

/// <summary>KeyValues 文本解析（支持 <c>//</c> 注释与带空格的引号值）。</summary>
public static class KeyValuesParser
{
    public static KeyValues Parse(string text)
    {
        var reader = new TokenReader(text);
        var root = new KeyValues();

        while (reader.Next(out var token))
        {
            if (token == "}")
            {
                continue;
            }

            if (!reader.Next(out var next))
            {
                root.Add(token, new KeyValues());
                break;
            }

            if (next == "{")
            {
                root.Add(token, ParseNode(reader));
            }
            else
            {
                root.Add(token, new KeyValues(next));
            }
        }

        return root;
    }

    private static KeyValues ParseNode(TokenReader reader)
    {
        var node = new KeyValues();

        while (reader.Next(out var token))
        {
            if (token == "}")
            {
                return node;
            }

            if (!reader.Next(out var next))
            {
                node.Add(token, new KeyValues());
                return node;
            }

            if (next == "{")
            {
                node.Add(token, ParseNode(reader));
            }
            else
            {
                node.Add(token, new KeyValues(next));
            }
        }

        return node;
    }

    private sealed class TokenReader(string text)
    {
        private int _index;

        public bool Next(out string token)
        {
            token = string.Empty;
            SkipTrivia();
            if (_index >= text.Length)
            {
                return false;
            }

            var c = text[_index];
            if (c is '{' or '}')
            {
                token = c.ToString();
                _index++;
                return true;
            }

            if (c == '"')
            {
                _index++;
                var builder = new StringBuilder();
                while (_index < text.Length && text[_index] != '"')
                {
                    if (text[_index] == '\\' && _index + 1 < text.Length)
                    {
                        _index++;
                    }

                    builder.Append(text[_index]);
                    _index++;
                }

                _index++;   // 收尾引号
                token = builder.ToString();
                return true;
            }

            var start = _index;
            while (_index < text.Length && !char.IsWhiteSpace(text[_index]) && text[_index] is not ('{' or '}'))
            {
                _index++;
            }

            token = text[start.._index];
            return token.Length > 0;
        }

        private void SkipTrivia()
        {
            while (_index < text.Length)
            {
                var c = text[_index];
                if (char.IsWhiteSpace(c))
                {
                    _index++;
                    continue;
                }

                if (c == '/' && _index + 1 < text.Length && text[_index + 1] == '/')
                {
                    while (_index < text.Length && text[_index] != '\n')
                    {
                        _index++;
                    }

                    continue;
                }

                break;
            }
        }
    }
}
