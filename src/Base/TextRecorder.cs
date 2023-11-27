using System.Collections.Concurrent;
using System.Text;

namespace IZU.Base
{
	public class TextRecorder
	{
		static TextRecorder recorder = new TextRecorder();
		public static TextRecorder Instance
		{
			get
			{
				if (recorder == null) recorder = new TextRecorder();
				return recorder;
			}
		}
		readonly ConcurrentQueue<string> queue;
		readonly string savePth = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "records\\WriteTextAsync.csv");
		public TextRecorder()
		{
			queue = new();
			FileInfo dir = new FileInfo(savePth);
			if (!dir.Directory.Exists) dir.Directory.Create();
			Task.Factory.StartNew(async () => {
				await LoopAsync();
			},TaskCreationOptions.LongRunning);
		}
		public void EnqueueAsync(string task)
		{
			queue.Enqueue(task);
		}

		async Task LoopAsync()
		{
			StreamWriter? outputFile = null;
			while (true)
			{
				if (queue.IsEmpty)
				{
					if (outputFile != null)
					{
						outputFile.Close();
						outputFile = null;
					}
					Task.Delay(1000).Wait();
					continue;
				}
				if (outputFile == null)
					outputFile = new StreamWriter(savePth,true,Encoding.UTF8);
				if (queue.TryDequeue(out string? task))
				{
					if (task == null) continue;
					Console.WriteLine(task.ToString());
					await outputFile.WriteLineAsync(task.ToString());
					await Task.Delay(50);
				}
			}
		}

	}
}
