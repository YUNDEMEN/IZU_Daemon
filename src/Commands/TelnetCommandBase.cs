using IZU.Interfaces;
using NLog.Fluent;
using System.CommandLine;
using System.CommandLine.IO;

namespace IZU.Commands
{
    public interface ITelnetCommand
    {
        string Name { get; }
        string Execute(string[] args);
    }

    public abstract class TelnetCommandBase : RootCommand, ITelnetCommand
    {
        //public override string Name
        //{
        //    get => base.Name;
        //    set => base.Name = CommandName;
        //}
        //RootCommand log = new RootCommand
        //        {
        //            new Argument<string>("url","web site url"),
        //            new Option<bool>(new string[]{ "--gethtml" ,"-html"},"Get html source"),
        //            new Option<bool>(new string[]{ "--getimage" ,"-image"},"Get images"),
        //            new Option<bool>(new string[]{ "--regex-option" ,"-regex"},"Use regex"),
        //            new Option<bool>(new string[]{ "--htmlagilitypack-option", "-agpack"},"Use HtmlAgilityPack"),
        //            new Option<bool>(new string[]{ "--anglesharp-option", "-agsharp"},"Use AngleSharp"),
        //            new Option<string>(new string[]{ "--download-path" ,"-path"},"Designate download path")
        //        };


        protected IIZUService _izuService;
        protected IS7NetService _s7netService;
        public TelnetCommandBase(string commandName, IIZUService service, IS7NetService s7netService)
        {
            _izuService = service;
            _s7netService = s7netService;
            Name = commandName;
        }

        protected virtual void CreateOption(string[] alias,string description)
        {
            var opt = new Option<bool>(alias, description);
            Add(opt);
            //this.SetHandler(() => { }, opt);
        }

        public virtual string Execute(string[] args)
        { 
            TestConsole testConsole = new();
            this.Invoke(args, testConsole);
            return testConsole.Out.ToString()!;
        }

    }

    public class CustomConsole : IConsole
    {
        /*
         
            MemoryStream ms = new MemoryStream();
            TextWriter tw = new StreamWriter(ms);

            IConsole console = new CustomConsole(StandardStreamWriter.Create(tw));
         */
        public CustomConsole(IStandardStreamWriter stdOut = null, IStandardStreamWriter stdError = null)
        {
            if (stdOut != null)
            {
                Out = stdOut;
                
                this.WriteLine("aaaaaaaaaaaaaa");
            }
            else
            {
                Out = StandardStreamWriter.Create(Console.Out);
            }

            if (stdError != null)
            {
                Error = stdError;
            }
            else
            {
                Error = StandardStreamWriter.Create(Console.Error);
            }
        }

        public IStandardStreamWriter Out { get; }
        public IStandardStreamWriter Error { get; }

        public bool IsOutputRedirected { get; } = false;

        public bool IsErrorRedirected { get; } = false;

        public bool IsInputRedirected { get; } = false;
    }
}
