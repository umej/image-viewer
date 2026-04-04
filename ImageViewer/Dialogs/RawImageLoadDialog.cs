using System;
using System.Windows;
using ImageViewer.Wrapper;

namespace ImageViewer.Dialogs;

/// <summary>
/// RAW画像読み込みダイアログクラス
/// LoadRawImage関数を使用してユーザーが指定したパラメータでRAW画像を読み込む
/// </summary>
public partial class RawImageLoadDialog : Window
{
    private Image _imageWrapper;
    public bool DialogResult { get; private set; }

    public RawImageLoadDialog(Image imageWrapper)
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
    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "RAW files (*.raw)|*.raw|All files (*.*)|*.*",
            Title = "RAW画像ファイルを選択してください"
        };

        if(openFileDialog.ShowDialog() == true)
        {
            FilePathTextBox.Text = openFileDialog.FileName;
        }
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
            MessageBox.Show($"エラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show("ファイルパスを選択してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if(!System.IO.File.Exists(FilePathTextBox.Text))
        {
            MessageBox.Show("指定されたファイルが見つかりません。", "ファイルエラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        // 幅と高さの確認
        if(!int.TryParse(WidthTextBox.Text, out int width) || width <= 0)
        {
            MessageBox.Show("幅に正の整数を入力してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if(!int.TryParse(HeightTextBox.Text, out int height) || height <= 0)
        {
            MessageBox.Show("高さに正の整数を入力してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        // ビット深度の確認
        if(BitDepthComboBox.SelectedItem == null)
        {
            MessageBox.Show("ビット深度を選択してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        // Bayer配列パターンの確認
        if(BayerPatternComboBox.SelectedItem == null)
        {
            MessageBox.Show("Bayer配列パターンを選択してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }
}