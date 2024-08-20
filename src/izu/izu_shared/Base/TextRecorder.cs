using System.Collections.Concurrent;
using System.Text;

namespace IZU.Base
{
	public class TextRecorder
	{
		string folder = string.Empty;
		readonly ConcurrentQueue<string> queue;
		const string header = "设备名称,设备类型,地址,别名,RW,新值,旧值,变量类型,描述,记录时间";
		Task _queueTask;
		readonly string recordFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "records");
		public TextRecorder(string folder)
		{
			this.folder = folder;
			queue = new();
			DirectoryInfo dir = new DirectoryInfo(Path.Combine(recordFolder, folder));
			if (!dir.Exists) dir.Create();

			_queueTask = Task.Factory.StartNew(async () =>
			{
				await LoopAsync();
			}, TaskCreationOptions.LongRunning);
		}
		public void EnqueueAsync(string task)
		{
			queue.Enqueue(task);
		}

		async Task LoopAsync()
		{
			StreamWriter? writer = null;
			while (true)
			{
				if (queue.IsEmpty)
				{
					if (writer != null)
					{
						writer.Close();
						writer = null;
					}
					Task.Delay(1000).Wait();
					continue;
				}

				if (queue.TryDequeue(out string? task))
				{
					if (writer == null)
					{
						bool newFile = !File.Exists(Path.Combine(recordFolder, folder, $"{DateTime.Now:yyyy-MM-dd}.csv"));
						writer = new(Path.Combine(recordFolder, folder, $"{DateTime.Now:yyyy-MM-dd}.csv"), true, Encoding.GetEncoding("GB2312"));
						if (newFile) await writer.WriteLineAsync(header);
					}

					if (task == null) continue;
					await writer.WriteLineAsync(task);
                    Console.WriteLine(task);
                    await Task.Delay(50);
				}
			}
		}

	}
}
