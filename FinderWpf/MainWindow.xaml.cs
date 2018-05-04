using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace FinderWpf
{

    public class ListItem
    {
        public string Name { get; set; }
        public string FullPath;
        public string ParentPath;
        public string Type { get; set; }
        public string Forward { get; set; }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private IList<ListBox> listBoxes;
        private IList<string> roots;
        private Queue<string> pathHistory;

        public MainWindow()
        {
            InitializeComponent();
            listBoxes = new[] { listBox1, listBox2, listBox3, listBox4 };
            roots = DriveInfo.GetDrives().Select(d => d.Name).ToList();
            roots.Add(@"D:\Dropbox\zdjeciaHD");
            var rootList = roots.Select(r => new ListItem {Name = r.TrimEnd('\\'), ParentPath = null, FullPath = r, Type="Q", Forward=">"});

            listBox1.ItemsSource = rootList;
            foreach (var listBox in listBoxes.Skip(1))
            {
                listBox.ItemTemplate = listBox1.ItemTemplate;
            }
            pathHistory = new Queue<string>();
        }

        private string getMatchingHistory(string path)
        {
            if (!path.EndsWith(@"\"))
                path += @"\";
            return pathHistory.LastOrDefault(h => h.StartsWith(path));
        }

        private void createNewListView()
        {
            var newListBox = new ListBox { SelectionMode = SelectionMode.Extended, Name = $"listBox{listBoxes.Count + 1}"};
            newListBox.SelectionChanged += listBox_SelectionChanged;
            newListBox.KeyDown += listBox_KeyDown;
            newListBox.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        }

        private void listBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = (ListBox) sender;
            textBlockPreview.Visibility = Visibility.Collapsed;
            textBlockPreview.Text = string.Empty;
            mediaPreview.Visibility = Visibility.Collapsed;
            mediaPreview.Source = null;
            imagePreview.Visibility = Visibility.Collapsed;
            imagePreview.Source = null;

            var listBoxIndex = listBoxes.IndexOf(listBox);

            for (var i = listBoxIndex + 1; i < listBoxes.Count; i++)
                listBoxes[i].ItemsSource = null;

            if (e.AddedItems.Count != 1)
                return;

            var item = (ListItem) e.AddedItems[0];
            var path = item.FullPath;
            if (path == null)
                return;

            pathBox.Text = path;

            pathHistory.Enqueue(path.TrimEnd('\\'));
            if (pathHistory.Count > 500)
                pathHistory.Dequeue();

            try
            {
                if (!File.GetAttributes(path).HasFlag(FileAttributes.Directory))
                {
                    if (new [] {".jpg", ".jpeg", ".png"}.Contains(Path.GetExtension(path)))
                    {
                        imagePreview.Visibility = Visibility.Visible;
                        imagePreview.Source = new BitmapImage(new Uri(path, UriKind.Absolute));
                    }
                    else if (path.EndsWith(".mp4", StringComparison.InvariantCultureIgnoreCase))
                    {
                        mediaPreview.Visibility = Visibility.Visible;
                        mediaPreview.Source = new Uri(path, UriKind.Absolute);
                        mediaPreview.IsMuted = true;
                    }
                    else
                    {
                        textBlockPreview.Visibility = Visibility.Visible;
                        var buf = new char[10000];
                        using (var st = new StreamReader(path))
                        {
                            st.ReadBlock(buf, 0, 10000);
                        }
                        textBlockPreview.Text = new string(buf);
                    }
                    return;
                }

                if (listBoxIndex + 1 == listBoxes.Count)
                    return;

                var nextListBox = listBoxes[listBoxIndex + 1];
                nextListBox.Items.Clear();

                if (!Directory.Exists(path))
                    return;


                
                var dirs = Directory.GetDirectories(path);
                var items = dirs.Select(dir => new ListItem
                {
                    Name = Path.GetFileName(dir),
                    ParentPath = path,
                    FullPath = dir,
                    Type = "D",
                    Forward = ">"
                }).ToList();

                var files = Directory.GetFiles(path);
                items.AddRange(files.Select(file => new ListItem
                {
                    Name = Path.GetFileName(file),
                    ParentPath = path,
                    FullPath = file,
                    Type = "f",
                    Forward = ""
                }));
                nextListBox.ItemsSource = items;

                var lastHistory = getMatchingHistory(path);
                var selected = lastHistory == null ? null : items.FirstOrDefault(i => lastHistory.Equals(i.FullPath, StringComparison.InvariantCultureIgnoreCase) || lastHistory.StartsWith(i.FullPath + @"\", StringComparison.InvariantCultureIgnoreCase));
                nextListBox.SelectedItem = selected;

                if (selected != null)
                    nextListBox.ScrollIntoView(selected);
            }
            catch (Exception ex)
            {
                textBlockPreview.Visibility = Visibility.Visible;
                imagePreview.Visibility = Visibility.Collapsed;
                imagePreview.Source = null;
                textBlockPreview.Text = ex.ToString();
            }

        }

        private void listBox_KeyDown(object sender, KeyEventArgs e)
        {
            var mainBox = (ListBox)sender;
            switch (e.Key)
            {
                case Key.Right:
                    e.Handled = true;
                    var boxIndex = listBoxes.IndexOf(mainBox);
                    if (boxIndex + 1 == listBoxes.Count)
                        break;
                    var nextListBox = listBoxes[boxIndex + 1];
                    if (nextListBox.Items.Count == 0)
                        break;
                    var nextItems = nextListBox.Items.OfType<ListItem>().ToList();
                    var lastHistory = getMatchingHistory((mainBox.SelectedItem as ListItem)?.FullPath);
                    var selected = (lastHistory == null ? null : nextItems.FirstOrDefault(i => lastHistory.Equals(i.FullPath, StringComparison.InvariantCultureIgnoreCase) || lastHistory.StartsWith(i.FullPath + @"\", StringComparison.InvariantCultureIgnoreCase))) ??
                                   nextItems.First();
                    nextListBox.SelectedItem = selected;
                    (nextListBox.ItemContainerGenerator.ContainerFromItem(selected) as ListBoxItem)?.Focus();

                    break;
                case Key.Left:
                    e.Handled = true;
                    int listIndex = listBoxes.IndexOf(sender as ListBox);
                    if (listIndex == 0)
                        break;
                    var prevListBox = listBoxes[listIndex - 1];
                    (prevListBox.ItemContainerGenerator.ContainerFromItem(prevListBox.SelectedItem) as ListBoxItem)?.Focus();

                    break;
                case Key.Space:
                    e.Handled = true;
                    var width = mainBox.ActualWidth;
                    break;
            }
        }

    }
}
