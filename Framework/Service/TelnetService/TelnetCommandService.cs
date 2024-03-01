using System.CommandLine;
using System.CommandLine.IO;

namespace Wonder.Service
{
    public class TelnetCommandService : ITelnetCommandService
    {
        protected readonly IServiceProvider _serviceProvider;
        private readonly RootCommand _commandRoot;
        private TestConsole? _telnetConsole;
        public IServiceProvider ServiceProvider { get { return _serviceProvider; } }
        public TelnetCommandService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _commandRoot = new RootCommand("izu command line") { Name = "izu" };
        }

        public void CollectCommands()
        {
            var commandTypes = GetAllTypesThatImplementInterface<Command>();
            foreach (var type in commandTypes)
            {
                var command = Activator.CreateInstance(type, this) as Command;
                if (command == null)
                    continue;

                _commandRoot.Add(command);
            }
        }
        public void WriteLine(string message)
        {
            _telnetConsole!.WriteLine(message);
        }
        public string RunCommand(params string[] args)
        {
            string name = args[0];
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            _telnetConsole = new();
            _commandRoot.Invoke(args, _telnetConsole);
            string result = _telnetConsole.Out.ToString()!;
            //var command = _commandRoot.FirstOrDefault(t => t.Name == name);
            //if (command == null)
            //    result = $"command [{name}] not exist!";
            //else
            //{
            //    _telnetConsole = new();
            //    _commandRoot.Invoke(args, _telnetConsole);
            //    result = _telnetConsole.Out.ToString()!;
            //    //result = command.Execute(args.Length > 1 ? args.Skip(1).ToArray() : Array.Empty<string>());
            //    //result = command.Execute(args);
            //}
            return result;
        }
        private IEnumerable<Type> GetAllTypesThatImplementInterface<T>()
        {
            return System.Reflection.Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(type => typeof(T).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);
        }
    }
}
