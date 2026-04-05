using ImageViewer.Helpers;
using ImageViewer.Wrapper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ImageViewer.Dialogs;

/// <summary>
/// RAW画像読み込みダイアログクラス
/// LoadRawImage関数を使用してユーザーが指定したパラメータでRAW画像を読み込む
/// </summary>
internal partial class RawImageLoadDialog : Window
{
    private ImageViewer.Wrapper.Image _imageWrapper;
    public bool DialogResult { get; private set; }

    public RawImageLoadDialog(ImageViewer.Wrapper.Image imageWrapper)
    {
        InitializeComponent();
        _imageWrapper = imageWrapper ?? throw new ArgumentNullException(nameof(imageWrapper));
        InitializeDefaultValues();
    }

    /// <summary>
    /// デフォルト値を初期化
    /// </summary>
    private void InitializeDefaultValues()
    {
        WidthTextBox.Text = "1920";
        HeightTextBox.Text = "1080";
        BitDepthComboBox.ItemsSource = new[] { 8, 16 };
        BitDepthComboBox.SelectedItem = 8;
        
        BayerPatternComboBox.ItemsSource = Enum.GetValues(typeof(BayerPattern));
        BayerPatternComboBox.SelectedItem = BayerPattern.RGGB;
    }

    /// <summary>
    /// ファイル選択ボタンクリック
    /// </summary>
    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".raw");
        picker.FileTypeFilter.Add("*");
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        IntPtr hwnd = WindowNative.GetWindowHandle(Context.Instance().MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();

        if (file != null)
        {
            FilePathTextBox.Text = file.Path;
        }
    }

    private async Task ShowErrorAsync(String Content)
    {
        var dialog = new ContentDialog
        {
            Title = "エラー",
            Content = Content,
            CloseButtonText = "OK",
            XamlRoot = Context.Instance().MainWindow.Content.XamlRoot
        };

        await dialog.ShowAsync();
    }

    /// <summary>
    /// OKボタンクリック - 画像読み込みを実行
    /// </summary>
    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if(!ValidateInputs())
        {
            return;
        }

        try
        {
            string filePath = FilePathTextBox.Text;
            int width = int.Parse(WidthTextBox.Text);
            int height = int.Parse(HeightTextBox.Text);
            int bitDepth = (int)BitDepthComboBox.SelectedItem;
            BayerPattern pattern = (BayerPattern)BayerPatternComboBox.SelectedItem;

            // LoadRawImage関数を呼び出し
            _imageWrapper.LoadRawImage(filePath, width, height, bitDepth, pattern);

            DialogResult = true;
            this.Close();
        }
        catch(Exception ex)
        {
            _ = ShowErrorAsync($"エラーが発生しました: {ex.Message}");
        }
    }

    /// <summary>
    /// キャンセルボタンクリック
    /// </summary>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        this.Close();
    }

    /// <summary>
    /// 入力値のバリデーション
    /// </summary>
    private bool ValidateInputs()
    {
        // ファイルパスの確認
        if(string.IsNullOrWhiteSpace(FilePathTextBox.Text))
        {
            _ = ShowErrorAsync("ファイルパスを選択してください。");
            return false;
        }

        if(!System.IO.File.Exists(FilePathTextBox.Text))
        {
            _ = ShowErrorAsync("指定されたファイルが見つかりません。");
            return false;
        }

        // 幅と高さの確認
        if(!int.TryParse(WidthTextBox.Text, out int width) || width <= 0)
        {
            _ = ShowErrorAsync("幅に正の整数を入力してください。");
            return false;
        }

        if(!int.TryParse(HeightTextBox.Text, out int height) || height <= 0)
        {
            _ = ShowErrorAsync("高さに正の整数を入力してください。");
            return false;
        }

        // ビット深度の確認
        if(BitDepthComboBox.SelectedItem == null)
        {
            _ = ShowErrorAsync("ビット深度を選択してください。");
            return false;
        }

        // Bayer配列パターンの確認
        if(BayerPatternComboBox.SelectedItem == null)
        {
            _ = ShowErrorAsync("Bayer配列パターンを選択してください。");
            return false;
        }

        return true;
    }
}