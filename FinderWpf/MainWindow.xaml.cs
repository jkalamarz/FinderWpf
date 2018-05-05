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
        private IList<GridSplitter> splitters;
        private IList<string> roots;
        private Queue<string> pathHistory;
        private bool ignoreNested;
        private int prevMaxIndex;

        public MainWindow()
        {
            InitializeComponent();
            listBoxes = new[] { listBox1, listBox2, listBox3, listBox4, listBox5, listBox6, listBox7 };
            splitters = new[] { gridSplitter1, gridSplitter2, gridSplitter3, gridSplitter4, gridSplitter5, gridSplitter6, gridSplitter7 };
            roots = DriveInfo.GetDrives().Select(d => d.Name).ToList();
            roots.Add(@"Y:\zdjeciaHD");
            var rootList = roots.Select(r => new ListItem {Name = r.TrimEnd('\\'), ParentPath = null, FullPath = r, Type="Q", Forward=">"});
            ignoreNested = false;

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
            listBoxes.Add(newListBox);
        }

        private void centerBoxes(int maxIndex)
        {
            if (maxIndex == prevMaxIndex)
                return;
            for (var i=0; i<listBoxes.Count - 1; i++)
            {
                if (i>0)
                    colGrid.ColumnDefinitions[2 * i - 1].Width = new GridLength(i >= maxIndex ? 0 : 1);
                colGrid.ColumnDefinitions[2 * i].Width = new GridLength(i >= maxIndex ? 0 : 150);
            }
            if (maxIndex > prevMaxIndex)
            {
                    Dispatcher.Invoke(() =>
                    {
                        dirsScroll.ScrollToEnd();
                    }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
            prevMaxIndex = maxIndex;
        }

        private void listBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ignoreNested)
                return;
            var listBox = (ListBox) sender;
            var listBoxIndex = listBoxes.IndexOf(listBox);

            textBlockPreview.Visibility = Visibility.Collapsed;
            textBlockPreview.Text = string.Empty;
            mediaPreview.Visibility = Visibility.Collapsed;
            mediaPreview.Source = null;
            imagePreview.Visibility = Visibility.Collapsed;
            imagePreview.Source = null;

            for (var i = listBoxIndex + 1; i < listBoxes.Count; i++)
                listBoxes[i].ItemsSource = null;

            if (listBox.SelectedItems.Count != 1)
                return;

            var item = (ListItem)listBox.SelectedItem;
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
                    filePreviewGrid.ColumnDefinitions[4].MinWidth = 150;
                    centerBoxes(listBoxIndex);
                    if (new [] {".jpg", ".jpeg", ".png"}.Contains(Path.GetExtension(path).ToLower()))
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

                filePreviewGrid.ColumnDefinitions[4].MinWidth = 0;

                if (listBoxIndex + 1 == listBoxes.Count || !Directory.Exists(path))
                    return;

                var nextListBox = listBoxes[listBoxIndex + 1];
                nextListBox.Items.Clear();

                centerBoxes(listBoxIndex + 1);
                
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
                ignoreNested = true;
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
            ignoreNested = false;

        }

        private void listBox_KeyDown(object sender, KeyEventArgs e)
        {
            var mainBox = (ListBox)sender;
            var boxIndex = listBoxes.IndexOf(mainBox);
            switch (e.Key)
            {
                case Key.Right:
                    e.Handled = true;
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
                    listBox_SelectionChanged(nextListBox, null);

                    break;
                case Key.Left:
                    e.Handled = true;
                    if (boxIndex == 0)
                        break;
                    var prevListBox = listBoxes[boxIndex - 1];
                    (prevListBox.ItemContainerGenerator.ContainerFromItem(prevListBox.SelectedItem) as ListBoxItem)?.Focus();
                    listBox_SelectionChanged(prevListBox, null);

                    break;
                case Key.Space:
                    e.Handled = true;
                    var width = mainBox.ActualWidth;
                    break;
            }
        }

        private void ScrollViewer_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
        {

        }
    }
}
