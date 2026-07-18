using System.Collections.ObjectModel;
using System.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.InstanceConsole.Editing;
using MCServerLauncher.WinUI.Models;
using WinUIEditor;

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
        Loaded += FileManagerPage_Loaded;
    }
    public LocalizedStrings Texts => App.Services.Localization.Texts;
    public ObservableCollection<RemoteFileModel> Files { get; } = [];
    public TextBox RemotePath => RemotePathTextBox;
    public int SelectedEncodingCodePage => _selectedEncoding.CodePage;
    public TextBox SearchInput => SearchTextBox;
    public CodeEditorControl Editor => EditorControl;
    public TextBlock StateText => EditorStateText;
    public ListView FilesList => RemoteFilesList;
    public string CurrentPath
    {
        get => CurrentPathTextBox.Text;
        set => CurrentPathTextBox.Text = value;
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
        _ = App.Services.Settings.SaveAsync();
    }

    private void LoadFile_Click(object sender, RoutedEventArgs e) => LoadFileRequested?.Invoke(this, EventArgs.Empty);
    private void SaveFile_Click(object sender, RoutedEventArgs e) => SaveFileRequested?.Invoke(this, EventArgs.Empty);
    private void ReloadFile_Click(object sender, RoutedEventArgs e) => ReloadFileRequested?.Invoke(this, EventArgs.Empty);
    private async void ChangeEncoding_Click(object sender, RoutedEventArgs e)
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
        if (await dialog.ShowAsync() != ContentDialogResult.Primary
            || comboBox.SelectedItem is not EncodingInfo selected
            || selected.CodePage == _selectedEncoding.CodePage)
        {
            return;
        }

        _selectedEncoding = selected;
        EncodingButton.Content = selected.DisplayName;
        EncodingChanged?.Invoke(this, EventArgs.Empty);
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
}
