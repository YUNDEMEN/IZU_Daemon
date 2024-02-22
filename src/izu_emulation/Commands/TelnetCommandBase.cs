using IZU.Interfaces;
using IZU.Service;
using System.CommandLine;
using System.CommandLine.IO;

namespace IZU.Commands
{
    public abstract class TelnetCommandBase : Command
    {
        protected ITelnetCommandService commandService;
        protected IIZUService _izuService;
        protected IS7NetService _s7netService;
        public TelnetCommandBase(string commandName, ITelnetCommandService commandService, IIZUService service, IS7NetService s7netService)
            : base(commandName)
        {
            this.commandService = commandService;
            _izuService = service;
            _s7netService = s7netService;
            Name = commandName;
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
