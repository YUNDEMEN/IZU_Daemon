namespace Wonder.Infrastructure
{
    public class xPrint
    {
        public readonly char[] HEADERSPLITTER = { ':', '：' };
        private List<string> _lines;
        private List<string> _titles;
        private List<string> _contents;
        public xPrint()
        {
            _lines = new();
            _titles = new();
            _contents = new();
        }
        public bool HasContent { get { return _contents.Count > 0; } }
        public void AppendLine(string line)
        {
            int i = line.IndexOf(HEADERSPLITTER[0]);
            i = i < 0 ? line.IndexOf(HEADERSPLITTER[1]) : i;
            _titles.Add(line[..i]);
            _contents.Add(line[(i+1)..]?.Trim());
        }
        public string Build()
        {
            int indices = _titles.Count > _contents.Count ? _contents.Count : _titles.Count;
            int max = _titles.Count > 0 ? _titles.Max(t => t.Length) : 0;
            for (int i = 0; i < indices; i++)
            {
                _lines.Add($"{_titles[i].PadRight(max)} : {_contents[i]}");
            }
            return _lines.Count > 0 ? string.Join("\r\n", _lines) : string.Empty;
        }
        public override string ToString()
        {
            return Build();
        }
    }
}
