using System;
using System.Drawing;
using System.Windows.Forms;
using ScriptPortal.Vegas;

namespace ChorusCrisp
{
    public class ChorusCrispDialog : Form
    {
        private ComboBox presetCombo;
        private TrackBar spliceSlider;
        private TrackBar crispSlider;
        private TrackBar offsetSlider;
        private ComboBox curveCombo;
        private Label spliceLabel;
        private Label crispLabel;
        private Label offsetLabel;
        private Button okButton;
        private Button cancelButton;
        
        private bool isLoadingPreset = false;
        private ChorusCrispPreset[] presets;

        public int SplicePosition { get { return spliceSlider.Value; } }
        public int Crispness { get { return 100 - crispSlider.Value; } }
        public int OffsetAmount { get { return 100 - offsetSlider.Value; } }
        public CurveType SelectedCurveType 
        { 
            get 
            {
                switch (curveCombo.SelectedIndex)
                {
                    case 0: return CurveType.Linear;
                    case 1: return CurveType.Fast;
                    case 2: return CurveType.Slow;
                    case 3: return CurveType.Sharp;
                    case 4: return CurveType.Smooth;
                    default: return CurveType.Linear;
                }
            }
        }

        public ChorusCrispDialog()
        {
            InitializePresets();
            InitializeComponent();
            LoadSettings();
        }
        
        private void InitializePresets()
        {
            presets = PresetManager.GetDefaultPresets();
        }

        private void InitializeComponent()
        {
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            
            this.Text = "Chorus Crisp";
            this.ClientSize = new Size(450, 420);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ForeColor = Color.White;

            int yPos = 20;

            // Preset selector
            Label presetTitle = new Label();
            presetTitle.Text = "Preset:";
            presetTitle.Location = new Point(20, yPos);
            presetTitle.Size = new Size(60, 20);
            presetTitle.ForeColor = Color.White;
            this.Controls.Add(presetTitle);

            presetCombo = new ComboBox();
            presetCombo.Location = new Point(85, yPos - 3);
            presetCombo.Size = new Size(345, 28);
            presetCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            presetCombo.Font = new Font(this.Font.FontFamily, 10);
            foreach (ChorusCrispPreset preset in presets)
            {
                presetCombo.Items.Add(preset);
            }
            presetCombo.SelectedIndex = 0;
            presetCombo.SelectedIndexChanged += PresetCombo_SelectedIndexChanged;
            this.Controls.Add(presetCombo);

            yPos += 40;

            // Splice Position column
            Label spliceTitle = new Label();
            spliceTitle.Text = "Splice Position";
            spliceTitle.Location = new Point(20, yPos);
            spliceTitle.Size = new Size(120, 20);
            spliceTitle.ForeColor = Color.White;
            spliceTitle.TextAlign = ContentAlignment.MiddleCenter;
            this.Controls.Add(spliceTitle);

            // Crispness column
            Label crispTitle = new Label();
            crispTitle.Text = "Volume Duck";
            crispTitle.Location = new Point(160, yPos);
            crispTitle.Size = new Size(120, 20);
            crispTitle.ForeColor = Color.White;
            crispTitle.TextAlign = ContentAlignment.MiddleCenter;
            this.Controls.Add(crispTitle);

            // Offset column
            Label offsetTitle = new Label();
            offsetTitle.Text = "Offset";
            offsetTitle.Location = new Point(300, yPos);
            offsetTitle.Size = new Size(120, 20);
            offsetTitle.ForeColor = Color.White;
            offsetTitle.TextAlign = ContentAlignment.MiddleCenter;
            this.Controls.Add(offsetTitle);

            yPos += 25;

            // Splice slider (vertical)
            spliceSlider = new TrackBar();
            spliceSlider.Orientation = Orientation.Vertical;
            spliceSlider.Location = new Point(55, yPos);
            spliceSlider.Size = new Size(45, 120);
            spliceSlider.Minimum = 0;
            spliceSlider.Maximum = 100;
            spliceSlider.Value = 47;
            spliceSlider.TickFrequency = 10;
            spliceSlider.ValueChanged += Slider_ValueChanged;
            this.Controls.Add(spliceSlider);

            // Crisp slider (vertical)
            crispSlider = new TrackBar();
            crispSlider.Orientation = Orientation.Vertical;
            crispSlider.Location = new Point(195, yPos);
            crispSlider.Size = new Size(45, 120);
            crispSlider.Minimum = 0;
            crispSlider.Maximum = 100;
            crispSlider.Value = 100;
            crispSlider.TickFrequency = 10;
            crispSlider.ValueChanged += Slider_ValueChanged;
            this.Controls.Add(crispSlider);

            // Offset slider (vertical)
            offsetSlider = new TrackBar();
            offsetSlider.Orientation = Orientation.Vertical;
            offsetSlider.Location = new Point(335, yPos);
            offsetSlider.Size = new Size(45, 120);
            offsetSlider.Minimum = 0;
            offsetSlider.Maximum = 100;
            offsetSlider.Value = 0;
            offsetSlider.TickFrequency = 10;
            offsetSlider.ValueChanged += Slider_ValueChanged;
            this.Controls.Add(offsetSlider);

            yPos += 125;

            // Splice value label
            spliceLabel = new Label();
            spliceLabel.Location = new Point(20, yPos);
            spliceLabel.Size = new Size(120, 20);
            spliceLabel.ForeColor = Color.FromArgb(100, 200, 255);
            spliceLabel.Text = "47%";
            spliceLabel.TextAlign = ContentAlignment.MiddleCenter;
            this.Controls.Add(spliceLabel);

            // Crisp value label
            crispLabel = new Label();
            crispLabel.Location = new Point(160, yPos);
            crispLabel.Size = new Size(120, 20);
            crispLabel.ForeColor = Color.FromArgb(100, 200, 255);
            crispLabel.Text = "0.0 dB";
            crispLabel.TextAlign = ContentAlignment.MiddleCenter;
            this.Controls.Add(crispLabel);

            // Offset value label
            offsetLabel = new Label();
            offsetLabel.Location = new Point(300, yPos);
            offsetLabel.Size = new Size(120, 20);
            offsetLabel.ForeColor = Color.FromArgb(100, 200, 255);
            offsetLabel.Text = "100%";
            offsetLabel.TextAlign = ContentAlignment.MiddleCenter;
            this.Controls.Add(offsetLabel);

            yPos += 30;

            // Range labels
            Label spliceRange = new Label();
            spliceRange.Text = "(20ms - 60ms)";
            spliceRange.Location = new Point(20, yPos);
            spliceRange.Size = new Size(120, 16);
            spliceRange.ForeColor = Color.Gray;
            spliceRange.TextAlign = ContentAlignment.MiddleCenter;
            spliceRange.Font = new Font(this.Font.FontFamily, 8);
            this.Controls.Add(spliceRange);

            Label crispRange = new Label();
            crispRange.Text = "(0 to -15 dB)";
            crispRange.Location = new Point(160, yPos);
            crispRange.Size = new Size(120, 16);
            crispRange.ForeColor = Color.Gray;
            crispRange.TextAlign = ContentAlignment.MiddleCenter;
            crispRange.Font = new Font(this.Font.FontFamily, 8);
            this.Controls.Add(crispRange);

            Label offsetRange = new Label();
            offsetRange.Text = "(0% - 100%)";
            offsetRange.Location = new Point(300, yPos);
            offsetRange.Size = new Size(120, 16);
            offsetRange.ForeColor = Color.Gray;
            offsetRange.TextAlign = ContentAlignment.MiddleCenter;
            offsetRange.Font = new Font(this.Font.FontFamily, 8);
            this.Controls.Add(offsetRange);

            yPos += 25;

            Label curveTitle = new Label();
            curveTitle.Text = "Crossfade Curve:";
            curveTitle.Location = new Point(20, yPos);
            curveTitle.Size = new Size(130, 20);
            curveTitle.ForeColor = Color.White;
            this.Controls.Add(curveTitle);

            yPos += 25;

            curveCombo = new ComboBox();
            curveCombo.Location = new Point(20, yPos);
            curveCombo.Size = new Size(410, 28);
            curveCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            curveCombo.Font = new Font(curveCombo.Font.FontFamily, 10);
            curveCombo.Items.AddRange(new string[] { "Linear", "Fast", "Slow", "Sharp", "Smooth" });
            curveCombo.SelectedIndex = 4;
            curveCombo.SelectedIndexChanged += CurveCombo_SelectedIndexChanged;
            this.Controls.Add(curveCombo);

            yPos += 45;

            okButton = new Button();
            okButton.Text = "Apply Crisp";
            okButton.Location = new Point(100, yPos);
            okButton.Size = new Size(120, 35);
            okButton.BackColor = Color.FromArgb(0, 122, 204);
            okButton.ForeColor = Color.White;
            okButton.FlatStyle = FlatStyle.Flat;
            okButton.DialogResult = DialogResult.OK;
            okButton.Click += OkButton_Click;
            this.Controls.Add(okButton);

            cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.Location = new Point(230, yPos);
            cancelButton.Size = new Size(120, 35);
            cancelButton.BackColor = Color.FromArgb(80, 80, 80);
            cancelButton.ForeColor = Color.White;
            cancelButton.FlatStyle = FlatStyle.Flat;
            cancelButton.DialogResult = DialogResult.Cancel;
            this.Controls.Add(cancelButton);

            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }
        
        private void LoadSettings()
        {
            isLoadingPreset = true;
            ChorusCrispSettings settings = ChorusCrispSettings.Load();
            spliceSlider.Value = Math.Max(0, Math.Min(100, settings.SpliceValue));
            crispSlider.Value = Math.Max(0, Math.Min(100, settings.CrispValue));
            offsetSlider.Value = Math.Max(0, Math.Min(100, settings.OffsetValue));
            curveCombo.SelectedIndex = Math.Max(0, Math.Min(4, settings.CurveIndex));
            UpdateLabels();
            isLoadingPreset = false;
            CheckForMatchingPreset();
        }
        
        private void SaveSettings()
        {
            ChorusCrispSettings settings = new ChorusCrispSettings();
            settings.SpliceValue = spliceSlider.Value;
            settings.CrispValue = crispSlider.Value;
            settings.OffsetValue = offsetSlider.Value;
            settings.CurveIndex = curveCombo.SelectedIndex;
            settings.Save();
        }
        
        private void PresetCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (presetCombo.SelectedIndex <= 0) return;
            
            ChorusCrispPreset preset = presets[presetCombo.SelectedIndex];
            if (preset.SpliceValue < 0) return;
            
            isLoadingPreset = true;
            spliceSlider.Value = preset.SpliceValue;
            crispSlider.Value = preset.CrispValue;
            offsetSlider.Value = preset.OffsetValue;
            curveCombo.SelectedIndex = preset.CurveIndex;
            UpdateLabels();
            isLoadingPreset = false;
        }
        
        private void Slider_ValueChanged(object sender, EventArgs e)
        {
            UpdateLabels();
            if (!isLoadingPreset)
            {
                CheckForMatchingPreset();
            }
        }
        
        private void CurveCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!isLoadingPreset)
            {
                CheckForMatchingPreset();
            }
        }
        
        private void CheckForMatchingPreset()
        {
            // Check if current values match any preset
            for (int i = 1; i < presets.Length; i++)
            {
                ChorusCrispPreset p = presets[i];
                if (spliceSlider.Value == p.SpliceValue &&
                    crispSlider.Value == p.CrispValue &&
                    offsetSlider.Value == p.OffsetValue &&
                    curveCombo.SelectedIndex == p.CurveIndex)
                {
                    isLoadingPreset = true;
                    presetCombo.SelectedIndex = i;
                    isLoadingPreset = false;
                    return;
                }
            }
            // No match, set to Custom
            isLoadingPreset = true;
            presetCombo.SelectedIndex = 0;
            isLoadingPreset = false;
        }
        
        private void UpdateLabels()
        {
            spliceLabel.Text = String.Format("{0}%", spliceSlider.Value);
            double duckDb = 0.0 + ((100 - crispSlider.Value) / 100.0) * (-15.0 - 0.0);
            crispLabel.Text = String.Format("{0:F1} dB", duckDb);
            offsetLabel.Text = String.Format("{0}%", 100 - offsetSlider.Value);
        }
        
        private void OkButton_Click(object sender, EventArgs e)
        {
            SaveSettings();
        }
    }
}
