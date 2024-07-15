using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OHTC.Tools
{
    /// <summary>
    /// Interaction logic for WindowWithCustomTitleBar.xaml
    /// </summary>
    public partial class TransferCommandEditor : Window
    {
        JArray jsonConfig;
        public TransferCommandEditor()
        {
            InitializeComponent();
            LoadConfig();
        }
        protected override void OnClosed(EventArgs e)
        {
            OnSave(this, jsonConfig.ToString());
            base.OnClosed(e);
        }
        void LoadConfig()
        {
            try
            {
                if (!System.IO.File.Exists("oht_task.json"))
                {
                    MessageBox.Show("oht_task.json is missing!");
                    return;
                }
                jsonConfig = JsonConvert.DeserializeObject<JArray>(System.IO.File.ReadAllText("oht_task.json"));
                foreach (var item in jsonConfig)
                {
                    list.Items.Add(new ListViewItem { Content = $"{item["ohtid"]}" });
                }
            }
            catch (Exception ex)
            {
            }
        }

        public event EventHandler<string> OnSave = delegate { };
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(f_oht.Text))
            {
                return;
            }
            if (string.IsNullOrEmpty(f_tasks.Text))
            {
                return;
            }
            if(!(bool)f_move.IsChecked && !(bool)f_carry.IsChecked)
            {
                return;
            }
            string[] tasks = f_tasks.Text.Split(new string[] { "\r"},StringSplitOptions.RemoveEmptyEntries);
            if (tasks.FirstOrDefault(t => t.Split(' ').Length < 2) != null)
            {
                return;
            }

            JObject? ohtNode = jsonConfig.FirstOrDefault(t => t["ohtid"].ToString() == f_oht.Text) as JObject;
            bool isNew = false;
            if (ohtNode == null)
            {
                ohtNode = new JObject();
                ohtNode["ohtid"] = f_oht.Text;
                isNew = true;
            }

            if ((bool)f_move.IsChecked)
                ohtNode["mode"] = "move";
            else if ((bool)f_carry.IsChecked)
                ohtNode["mode"] = "carry";

            JArray nodeTasks = new JArray();            
            for (int i = 0; i < tasks.Length; i++)
            {
                ohtNode["StartTaskIndex"] = 0;
                string line = tasks[i];
                if (line.StartsWith("*"))
                {
                    ohtNode["StartTaskIndex"] = i;
                    line = line.Trim('*');
                }

                string[] t = line.Split(" ");
                if (t.Length < 2) continue;
                JObject nodeTask = new JObject();
                nodeTask["fromStation"] = t[0];
                nodeTask["toStation"] = t[1];
                nodeTasks.Add(nodeTask);
            }
            ohtNode["tasks"] = nodeTasks;

            if (isNew)
            {
                jsonConfig.Add(ohtNode);
            }



            list.Items.Clear();
            foreach (var item in jsonConfig)
            {
                list.Items.Add(new ListViewItem { Content = $"{item["ohtid"]}" });
            }

            System.IO.File.WriteAllText("oht_task.json", jsonConfig.ToString());
        }
        private void Del_Click(object sender, RoutedEventArgs e)
        {
            if (list.SelectedItem == null) return;
            string oht = (list.SelectedItem as ListBoxItem).Content.ToString();
            JObject? ohtNode = jsonConfig.FirstOrDefault(t => t["ohtid"].ToString() == oht) as JObject;
            if (ohtNode == null) return;

            jsonConfig.Remove(ohtNode);
            list.Items.Clear();
            foreach (var item in jsonConfig)
            {
                list.Items.Add(new ListViewItem { Content = $"{item["ohtid"]}" });
            }
            System.IO.File.WriteAllText("oht_task.json", jsonConfig.ToString());
        }

        private void list_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;
            string oht = (e.AddedItems[0] as ListBoxItem).Content.ToString();
            JObject? item = jsonConfig.FirstOrDefault(t => t["ohtid"].ToString() == oht) as JObject;
            if (item == null) return;
            try
            {
                f_oht.Text = oht;
                if ($"{item["mode"]}" == "move")
                    f_move.IsChecked = true;
                else if ($"{item["mode"]}" == "carry")
                    f_carry.IsChecked = true;

                int startIndex = (int)item["StartTaskIndex"];

                JArray tasks = (JArray)item["tasks"];
                f_tasks.Text = string.Empty;
                for (int i = 0; i < tasks.Count; i++)
                {
                    f_tasks.Text += ($"{(i == startIndex ? "*" : string.Empty)}{tasks[i]["fromStation"]} {tasks[i]["toStation"]}\r");
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
