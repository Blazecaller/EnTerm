using System.Text;
using System.Text.RegularExpressions;

namespace ENTestTerminal;

public enum LinkMode
{
    Unknown,
    Data,
    Command
}

public sealed class AtLinkParser
{
    private static readonly Regex TestFrameRegex = new(@"\|(?<body>[^|\r\n]*)\|\r\n", RegexOptions.Compiled);

    private readonly StringBuilder _buffer = new();
    private const int MaxBufferChars = 4000;
    private const int TrimKeepChars = 500;

    public event Action<TestFrame>? TestFrameReceived;
    public event Action<LinkMode>? ModeChanged;
    public event Action<bool>? TestModeChanged;

    public void Feed(byte[] data, int count)
    {
        _buffer.Append(Encoding.Latin1.GetString(data, 0, count));

        while (true)
        {
            string current = _buffer.ToString();

            int bestIndex = int.MaxValue;
            int bestLength = 0;
            Action? action = null;

            void Consider(string marker, Action onMatch)
            {
                int idx = current.IndexOf(marker, StringComparison.Ordinal);
                if (idx >= 0 && idx < bestIndex)
                {
                    bestIndex = idx;
                    bestLength = marker.Length;
                    action = onMatch;
                }
            }

            Consider("+COMMAND MODE\r\n", () => ModeChanged?.Invoke(LinkMode.Command));
            Consider("+DATA MODE\r\n", () => ModeChanged?.Invoke(LinkMode.Data));
            Consider("BEGIN TEST\r\n", () => TestModeChanged?.Invoke(true));
            Consider("END TEST\r\n", () => TestModeChanged?.Invoke(false));

            var frameMatch = TestFrameRegex.Match(current);
            if (frameMatch.Success && frameMatch.Index < bestIndex)
            {
                bestIndex = frameMatch.Index;
                bestLength = frameMatch.Length;
                string body = frameMatch.Groups["body"].Value;
                action = () =>
                {
                    if (TestFrame.TryParse(body, out var frame) && frame is not null)
                    {
                        TestFrameReceived?.Invoke(frame);
                    }
                };
            }

            if (action is null)
            {
                if (_buffer.Length > MaxBufferChars)
                {
                    _buffer.Remove(0, _buffer.Length - TrimKeepChars);
                }
                break;
            }

            _buffer.Remove(0, bestIndex + bestLength);
            action();
        }
    }
}
