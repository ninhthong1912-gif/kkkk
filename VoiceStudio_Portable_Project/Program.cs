using System.Speech.Synthesis;
using NAudio.Wave;
using NAudio.Lame;

namespace VoiceStudio;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public sealed class MainForm : Form
{
    private readonly SpeechSynthesizer synth = new();
    private readonly ComboBox voices = new();
    private readonly TextBox input = new();
    private readonly TrackBar rate = new();
    private readonly Label rateLabel = new();
    private readonly Button preview = new();
    private readonly Button stop = new();
    private readonly Button export = new();
    private readonly Label status = new();
    private string? tempWav;

    public MainForm()
    {
        Text = "VoiceStudio — Giọng Windows & xuất MP3";
        MinimumSize = new Size(760, 620);
        Size = new Size(900, 720);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(13, 19, 35);
        ForeColor = Color.FromArgb(235, 241, 255);
        Font = new Font("Segoe UI", 10);
        BuildUi();
        LoadVoices();
        FormClosing += (_, _) => Cleanup();
    }

    private Label MakeLabel(string text, int x, int y, int w, int h = 26)
    {
        var l = new Label { Text = text, Left = x, Top = y, Width = w, Height = h, ForeColor = ForeColor };
        Controls.Add(l);
        return l;
    }

    private void BuildUi()
    {
        var title = MakeLabel("VoiceStudio", 24, 18, 500, 38);
        title.Font = new Font("Segoe UI Semibold", 22);
        MakeLabel("Dùng giọng đọc cài sẵn trên Windows · Không cần API key", 26, 58, 720, 26);

        MakeLabel("Giọng đọc trên máy", 26, 102, 220);
        voices.SetBounds(26, 130, 820, 34);
        voices.DropDownStyle = ComboBoxStyle.DropDownList;
        Controls.Add(voices);

        MakeLabel("Văn bản cần đọc", 26, 180, 300);
        input.SetBounds(26, 208, 820, 270);
        input.Multiline = true;
        input.ScrollBars = ScrollBars.Vertical;
        input.AcceptsReturn = true;
        input.BackColor = Color.FromArgb(20, 29, 49);
        input.ForeColor = ForeColor;
        input.BorderStyle = BorderStyle.FixedSingle;
        input.Font = new Font("Segoe UI", 11);
        input.Text = "Kính chào quý vị và các bạn. Chào mừng quý vị đến với chương trình hôm nay.";
        Controls.Add(input);

        MakeLabel("Tốc độ đọc", 26, 493, 100);
        rate.SetBounds(110, 487, 250, 42);
        rate.Minimum = -5; rate.Maximum = 5; rate.Value = 0; rate.TickFrequency = 1;
        rate.BackColor = BackColor;
        rate.ValueChanged += (_, _) => rateLabel.Text = rate.Value.ToString();
        Controls.Add(rate);
        rateLabel.SetBounds(365, 493, 40, 28);
        rateLabel.Text = "0"; rateLabel.ForeColor = ForeColor;
        Controls.Add(rateLabel);
        MakeLabel("(-5 chậm · +5 nhanh)", 410, 493, 220);

        StyleButton(preview, "▶  Nghe thử", 26, 540, 150, Color.FromArgb(88, 113, 235));
        preview.Click += async (_, _) => await PreviewAsync();
        StyleButton(stop, "■  Dừng", 188, 540, 120, Color.FromArgb(43, 57, 83));
        stop.Click += (_, _) => { synth.SpeakAsyncCancelAll(); SetStatus("Đã dừng phát."); };
        StyleButton(export, "↓  Xuất MP3", 320, 540, 170, Color.FromArgb(37, 135, 111));
        export.Click += async (_, _) => await ExportMp3Async();

        status.SetBounds(26, 600, 820, 50);
        status.ForeColor = Color.FromArgb(165, 190, 225);
        Controls.Add(status);
        SetStatus("Sẵn sàng. Giọng đọc phụ thuộc các giọng đã cài trên Windows.");
    }

    private void StyleButton(Button b, string text, int x, int y, int w, Color color)
    {
        b.Text = text; b.SetBounds(x, y, w, 42);
        b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0;
        b.BackColor = color; b.ForeColor = Color.White;
        b.Font = new Font("Segoe UI Semibold", 10);
        b.Cursor = Cursors.Hand;
        Controls.Add(b);
    }

    private void LoadVoices()
    {
        try
        {
            foreach (InstalledVoice v in synth.GetInstalledVoices())
                if (v.Enabled) voices.Items.Add(new VoiceItem(v.VoiceInfo.Name, v.VoiceInfo.Culture.Name));
            if (voices.Items.Count > 0) voices.SelectedIndex = 0;
            else SetStatus("Không tìm thấy giọng SAPI. Hãy cài thêm giọng trong Windows Settings > Accessibility > Narrator.");
        }
        catch (Exception ex) { SetStatus("Không tải được danh sách giọng: " + ex.Message); }
    }

    private void ConfigureVoice()
    {
        if (voices.SelectedItem is VoiceItem v) synth.SelectVoice(v.Name);
        synth.Rate = rate.Value;
        synth.Volume = 100;
    }

    private async Task PreviewAsync()
    {
        if (string.IsNullOrWhiteSpace(input.Text)) { SetStatus("Hãy nhập văn bản trước."); return; }
        try
        {
            ConfigureVoice();
            synth.SpeakAsyncCancelAll();
            synth.SpeakAsync(input.Text);
            SetStatus("Đang đọc văn bản bằng giọng Windows…");
            await Task.CompletedTask;
        }
        catch (Exception ex) { SetStatus("Lỗi đọc: " + ex.Message); }
    }

    private async Task ExportMp3Async()
    {
        if (string.IsNullOrWhiteSpace(input.Text)) { SetStatus("Hãy nhập văn bản trước."); return; }
        using var dialog = new SaveFileDialog { Filter = "MP3 audio (*.mp3)|*.mp3", FileName = "VoiceStudio.mp3", Title = "Lưu file MP3" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        export.Enabled = false;
        tempWav = Path.Combine(Path.GetTempPath(), "VoiceStudio_" + Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            ConfigureVoice();
            synth.SpeakAsyncCancelAll();
            SetStatus("Đang tạo giọng đọc và chuyển sang MP3…");
            await Task.Run(() =>
            {
                using (var wave = new WaveFileWriter(tempWav, new WaveFormat(44100, 16, 1)))
                {
                    synth.SetOutputToWaveStream(wave);
                    synth.Speak(input.Text);
                    synth.SetOutputToNull();
                }
                using var reader = new AudioFileReader(tempWav);
                using var writer = new LameMP3FileWriter(dialog.FileName, reader.WaveFormat, LAMEPreset.STANDARD);
                reader.CopyTo(writer);
            });
            SetStatus("Đã xuất MP3: " + dialog.FileName);
        }
        catch (Exception ex)
        {
            SetStatus("Xuất MP3 thất bại: " + ex.Message);
            MessageBox.Show(this, ex.ToString(), "VoiceStudio", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { export.Enabled = true; CleanupTemp(); }
    }

    private void SetStatus(string text) => status.Text = text;
    private void CleanupTemp()
    {
        try { if (tempWav != null && File.Exists(tempWav)) File.Delete(tempWav); } catch { }
        tempWav = null;
    }
    private void Cleanup() { synth.SpeakAsyncCancelAll(); synth.Dispose(); CleanupTemp(); }

    private sealed record VoiceItem(string Name, string Culture)
    {
        public override string ToString() => $"{Name} ({Culture})";
    }
}
