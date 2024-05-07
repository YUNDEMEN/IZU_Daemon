using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.IO;
using Wonder.Infrastructure;

namespace Wonder.Service
{
    public class TelnetCommandService : ITelnetCommandService
    {
        private readonly ILogger _logger;
        protected readonly IServiceProvider _serviceProvider;
        private readonly RootCommand _commandRoot;
        private TestConsole? _telnetConsole;
        public IServiceProvider ServiceProvider { get { return _serviceProvider; } }
        public TelnetCommandService(IServiceProvider serviceProvider)
        {
            _logger = LogManager.Factory.CreateLogger<TelnetCommandService>();
            _serviceProvider = serviceProvider;
            _commandRoot = new RootCommand("izu command line") { Name = "izu" };
        }

        public void CollectCommands()
        {
            var commandTypes = GetAllTypesThatImplementInterface<Command>();
            _logger.LogInformation($"{commandTypes.Count()} commands is found");
            foreach (var type in commandTypes)
            {
                var command = Activator.CreateInstance(type, this) as Command;
                if (command == null)
                    continue;

                _commandRoot.Add(command);
                _logger.LogInformation($"collect command [{command.Name}] : {command.Description}");
            }
        }
        public void WriteLine(string message)
        {
            if (!string.IsNullOrEmpty(message))
                _telnetConsole!.WriteLine(message);
            else
                _telnetConsole!.WriteLine("empty message");
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
            var sources = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var source in sources)
            {
                var results = source.GetTypes().Where(type =>
                !type.Equals(typeof(RootCommand)) &&
                !type.Equals(typeof(Command)) &&
                typeof(T).IsAssignableFrom(type) &&
                !type.IsInterface &&
                !type.IsAbstract
                );
                foreach (var item in results)
                {
                    yield return item;
                }
            }
            //var source = System.Reflection.Assembly.GetExecutingAssembly();
            //_logger.LogDebug($"command source: {source.FullName}");

        }
    }
}
