using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OHTC.Tools.Tools
{
    internal class ErrText
    {
        static IDictionary<string, string> data;
        static ErrText()
        {
            data = new Dictionary<string, string>();

            if (!File.Exists("err_text.txt"))
                return;

            string[] errors = File.ReadAllLines("err_text.txt");
            foreach (var err in errors)
            {
                if (err.StartsWith('#')) continue;

                string[] info = err.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                if (info.Length < 2) continue;
                data[info[0]] = info[1];
            }
        }

        public static string GetErrText(string code)
        {
            if(!data.ContainsKey(code))
                return code;

            return data[code];
        }

    }
}
