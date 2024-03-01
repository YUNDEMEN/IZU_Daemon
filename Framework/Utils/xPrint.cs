namespace Wonder.Utils
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
        public void AppendLine(string line)
        {
            var ts = line.Split(HEADERSPLITTER, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (ts.Length > 1)
            {
                _titles.Add(ts[0]);
                _contents.Add(ts[1]);
            }
        }
        public string Build()
        {
            int indices = _titles.Count > _contents.Count ? _contents.Count : _titles.Count;
            int max = _titles.Max(t => t.Length);
            for (int i = 0; i < indices; i++)
            {
                _lines.Add($"{_titles[i].PadRight(max)} : {_contents[i]}");
            }
            return string.Join("\r\n", _lines);
        }
        public override string ToString()
        {
            return Build();
        }
    }
}
