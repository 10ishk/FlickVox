using FlickVox.Infrastructure;
using MessageBox = System.Windows.MessageBox;

namespace FlickVox;

public partial class PhraseEditorWindow : Window
{
    public string PhraseTitle => PhraseName.Text.Trim();
    public string PhraseBody => PhraseText.Text.Trim();

    public PhraseEditorWindow(string name, string text)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowBackdrop.ApplyDarkTitleBar(this);
        PhraseName.Text = name;
        PhraseText.Text = text;
        Loaded += (_, _) => { PhraseName.Focus(); PhraseName.SelectAll(); };
    }

    void Save(object sender, RoutedEventArgs e)
    {
        if (PhraseTitle.Length == 0 || PhraseBody.Length == 0)
        {
            MessageBox.Show(this, "Enter both a name and text for the phrase.", "FlickVox", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }
}
