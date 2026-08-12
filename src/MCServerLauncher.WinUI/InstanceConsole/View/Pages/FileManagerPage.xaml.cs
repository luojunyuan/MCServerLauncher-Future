using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using CommunityToolkit.Mvvm.Input;
using MCServerLauncher.WinUI.Core;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.InstanceConsole.Editing;
using MCServerLauncher.WinUI.InstanceConsole.Modules;
using MCServerLauncher.WinUI.Models;
using Serilog;
using WinUIEditor;
using MuxControls = Microsoft.UI.Xaml.Controls;

namespace MCServerLauncher.WinUI.InstanceConsole.View.Pages;

public sealed partial class FileManagerPage : UserControl
{
    private readonly EncodingInfo[] _encodings;
    private EncodingInfo _selectedEncoding;

    public FileManagerPage()
    {
        _encodings = Encoding.GetEncodings().OrderBy(encoding => encoding.DisplayName).ToArray();
        _selectedEncoding = _encodings.FirstOrDefault(encoding => encoding.CodePage == Encoding.UTF8.CodePage)
            ?? _encodings[0];
        InitializeComponent();
        EncodingButton.Content = _selectedEncoding.DisplayName;
        Files.CollectionChanged += (_, _) => UpdateEmptyState();
        Loaded += FileManagerPage_Loaded;
    }
    public LocalizedStrings Texts => App.Services.Localization.Texts;
    public ObservableCollection<RemoteFileModel> Files { get; } = [];
    public ObservableCollection<DirectoryNode> DirectoryTreeItems { get; } = [];
    public TextBox RemotePath => RemotePathTextBox;
    public int SelectedEncodingCodePage => _selectedEncoding.CodePage;
    public TextBox SearchInput => SearchTextBox;
    public CodeEditorControl Editor => EditorControl;
    public TextBlock StateText => EditorStateText;
    public ListView FilesList => RemoteFilesList;
    public string CurrentPath
    {
        get => CurrentPathTextBox.Text;
        set
        {
            CurrentPathTextBox.Text = value;
            RebuildDirectoryTree();
        }
    }
    public RemoteFileModel? SelectedFile => RemoteFilesList.SelectedItem as RemoteFileModel;
    public void SetNavigationState(bool canGoBack, bool canGoForward)
    {
        BackButton.IsEnabled = canGoBack;
        ForwardButton.IsEnabled = canGoForward;
    }
    public event EventHandler? LoadFileRequested;
    public event EventHandler? SaveFileRequested;
    public event EventHandler? ReloadFileRequested;
    public event EventHandler? EncodingChanged;
    public event EventHandler? SearchRequested;
    public event EventHandler? UndoRequested;
    public event EventHandler? RedoRequested;
    public event EventHandler? CopyRequested;
    public event EventHandler? PasteRequested;
    public event EventHandler? SelectAllRequested;
    public event EventHandler? ZoomOutRequested;
    public event EventHandler? ZoomInRequested;
    public event EventHandler? RefreshDirectoryRequested;
    public event EventHandler? OpenItemRequested;
    public event EventHandler? DownloadRequested;
    public event EventHandler? UploadRequested;
    public event EventHandler? RenameRequested;
    public event EventHandler? DeleteFileRequested;
    public event EventHandler? CreateDirectoryRequested;
    public event EventHandler? NavigateUpRequested;
    public event EventHandler? NavigateBackRequested;
    public event EventHandler? NavigateForwardRequested;

    private void FileManagerPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (App.Services.Settings.Current.App.HideTips.TryGetValue("FileManagerMultiSelect", out var hidden) && hidden)
            MultiSelectTipBar.IsOpen = false;
    }

    private void MultiSelectTipBar_Closed(object sender, object e)
    {
        App.Services.Settings.Current.App.HideTips["FileManagerMultiSelect"] = true;
        App.Services.Settings.SaveAsync().FireAndForget("MultiSelectTipBar_Closed");
    }

    private void LoadFile_Click(object sender, RoutedEventArgs e) => LoadFileRequested?.Invoke(this, EventArgs.Empty);
    private void SaveFile_Click(object sender, RoutedEventArgs e) => SaveFileRequested?.Invoke(this, EventArgs.Empty);
    private void ReloadFile_Click(object sender, RoutedEventArgs e) => ReloadFileRequested?.Invoke(this, EventArgs.Empty);
    private void ChangeEncoding_Click(object sender, RoutedEventArgs e)
        => ChangeEncodingCoreAsync().FireAndForget("ChangeEncoding_Click");

    private async Task ChangeEncodingCoreAsync()
    {
        var comboBox = new ComboBox
        {
            ItemsSource = _encodings,
            SelectedItem = _selectedEncoding,
            DisplayMemberPath = nameof(EncodingInfo.DisplayName),
            Width = 300
        };
        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = Texts["PreventGarbageTextTip"],
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(comboBox);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Texts["Encoding"],
            Content = content,
            PrimaryButtonText = Texts["OK"],
            CloseButtonText = Texts["Cancel"],
            DefaultButton = ContentDialogButton.Primary
        };
        try
        {
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary
                || comboBox.SelectedItem is not EncodingInfo selected
                || selected.CodePage == _selectedEncoding.CodePage)
            {
                return;
            }

            _selectedEncoding = selected;
            EncodingButton.Content = selected.DisplayName;
            EncodingChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[WinUI] Encoding picker dialog failed");
        }
    }
    private void SearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e) { if (e.Key == Windows.System.VirtualKey.Enter) { e.Handled = true; SearchRequested?.Invoke(this, EventArgs.Empty); } }
    private void FindNext_Click(object sender, RoutedEventArgs e) => SearchRequested?.Invoke(this, EventArgs.Empty);
    private void Undo_Click(object sender, RoutedEventArgs e) => UndoRequested?.Invoke(this, EventArgs.Empty);
    private void Redo_Click(object sender, RoutedEventArgs e) => RedoRequested?.Invoke(this, EventArgs.Empty);
    private void Copy_Click(object sender, RoutedEventArgs e) => CopyRequested?.Invoke(this, EventArgs.Empty);
    private void Paste_Click(object sender, RoutedEventArgs e) => PasteRequested?.Invoke(this, EventArgs.Empty);
    private void SelectAll_Click(object sender, RoutedEventArgs e) => SelectAllRequested?.Invoke(this, EventArgs.Empty);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => ZoomOutRequested?.Invoke(this, EventArgs.Empty);
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => ZoomInRequested?.Invoke(this, EventArgs.Empty);
    private void RefreshDirectory_Click(object sender, RoutedEventArgs e) => RefreshDirectoryRequested?.Invoke(this, EventArgs.Empty);
    private void CurrentPathTextBox_KeyDown(object sender, KeyRoutedEventArgs e) { if (e.Key == Windows.System.VirtualKey.Enter) { e.Handled = true; RefreshDirectoryRequested?.Invoke(this, EventArgs.Empty); } }
    private void OpenItem_Click(object sender, RoutedEventArgs e) => OpenItemRequested?.Invoke(this, EventArgs.Empty);
    private void Download_Click(object sender, RoutedEventArgs e) => DownloadRequested?.Invoke(this, EventArgs.Empty);
    private void Upload_Click(object sender, RoutedEventArgs e) => UploadRequested?.Invoke(this, EventArgs.Empty);
    private void Rename_Click(object sender, RoutedEventArgs e) => RenameRequested?.Invoke(this, EventArgs.Empty);
    private void DeleteFile_Click(object sender, RoutedEventArgs e) => DeleteFileRequested?.Invoke(this, EventArgs.Empty);
    private void CreateDirectory_Click(object sender, RoutedEventArgs e) => CreateDirectoryRequested?.Invoke(this, EventArgs.Empty);
    private void NavigateUp_Click(object sender, RoutedEventArgs e) => NavigateUpRequested?.Invoke(this, EventArgs.Empty);
    private void NavigateBack_Click(object sender, RoutedEventArgs e) => NavigateBackRequested?.Invoke(this, EventArgs.Empty);
    private void NavigateForward_Click(object sender, RoutedEventArgs e) => NavigateForwardRequested?.Invoke(this, EventArgs.Empty);
    private void FilesList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => OpenItemRequested?.Invoke(this, EventArgs.Empty);

    private void DirectoryTree_ItemInvoked(MuxControls.TreeView sender, MuxControls.TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is DirectoryNode node)
        {
            NavigateToDirectory(node.VirtualPath);
            return;
        }

        if (args.InvokedItem is FrameworkElement element && element.DataContext is DirectoryNode fromDataContext)
        {
            NavigateToDirectory(fromDataContext.VirtualPath);
        }
    }

    private void NavigateToDirectory(string virtualPath)
    {
        CurrentPath = virtualPath;
        RefreshDirectoryRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RebuildDirectoryTree()
    {
        DirectoryTreeItems.Clear();

        var path = NormalizeVirtualPath(CurrentPath);
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        var root = new DirectoryNode("/", "/", isExpanded: true);
        DirectoryTreeItems.Add(root);

        var current = root;
        var cumulative = string.Empty;
        foreach (var segment in segments)
        {
            cumulative += "/" + segment;
            var node = new DirectoryNode(segment, cumulative, isExpanded: true);
            current.Children.Add(node);
            current = node;
        }

        foreach (var directory in Files.Where(item => item.IsDirectory && item.Name != ".."))
        {
            current.Children.Add(new DirectoryNode(directory.Name, directory.VirtualPath));
        }
    }

    private void UpdateEmptyState()
    {
        if (Files.Any(item => item.Name != ".."))
        {
            EmptyStateLayer.Visibility = Visibility.Collapsed;
            FileListHeader.Visibility = Visibility.Visible;
            RemoteFilesList.Visibility = Visibility.Visible;
            return;
        }

        EmptyStateLayer.Symbol = "📂";
        EmptyStateLayer.StopTip = Texts["NothingHere"];
        EmptyStateLayer.StopDescription = Texts["TryAddSomething"];
        EmptyStateLayer.ButtonText = Texts["Refresh"];
        EmptyStateLayer.ButtonCommand = new RelayCommand(() => RefreshDirectoryRequested?.Invoke(this, EventArgs.Empty));
        EmptyStateLayer.Visibility = Visibility.Visible;
        FileListHeader.Visibility = Visibility.Collapsed;
        RemoteFilesList.Visibility = Visibility.Collapsed;
    }

    private static string NormalizeVirtualPath(string path) => VirtualPath.Normalize(path);
}

public sealed class DirectoryNode
{
    public DirectoryNode(string name, string virtualPath, bool isExpanded = false)
    {
        Name = name;
        VirtualPath = virtualPath;
        IsExpanded = isExpanded;
    }

    public string Name { get; }
    public string VirtualPath { get; }
    public bool IsExpanded { get; }
    public ObservableCollection<DirectoryNode> Children { get; } = [];
}
