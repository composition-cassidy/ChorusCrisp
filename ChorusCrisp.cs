// Chorus Crisp - Vegas Pro Script
// For Sparta Remix vocal chop layering effect

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ScriptPortal.Vegas;

public class EntryPoint
{
    const double MIN_SPLICE_TIME = 0.020;
    const double MAX_SPLICE_TIME = 0.060;
    const double MIN_DUCK_DB = 0.0;
    const double MAX_DUCK_DB = -15.0;

    public void FromVegas(Vegas vegas)
    {
        List<TrackEvent> selectedEvents = new List<TrackEvent>();
        
        foreach (Track track in vegas.Project.Tracks)
        {
            foreach (TrackEvent ev in track.Events)
            {
                if (ev.Selected && ev.IsAudio())
                {
                    selectedEvents.Add(ev);
                }
            }
        }

        if (selectedEvents.Count == 0)
        {
            MessageBox.Show("No audio clips selected!\nSelect some audio events and try again.", 
                "Chorus Crisp", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ChorusCrispDialog dialog = new ChorusCrispDialog();
        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        double splicePercent = dialog.SplicePosition / 100.0;
        double crispPercent = dialog.Crispness / 100.0;
        double offsetPercent = dialog.OffsetAmount / 100.0;
        CurveType fadeType = dialog.SelectedCurveType;

        double spliceTime = MIN_SPLICE_TIME + (splicePercent * (MAX_SPLICE_TIME - MIN_SPLICE_TIME));
        double duckDb = MIN_DUCK_DB + (crispPercent * (MAX_DUCK_DB - MIN_DUCK_DB));

        int successCount = 0;
        int errorCount = 0;
        string lastError = "";

        using (UndoBlock undo = new UndoBlock("Chorus Crisp"))
        {
            foreach (TrackEvent ev in selectedEvents)
            {
                try
                {
                    ProcessEvent(ev, spliceTime, duckDb, offsetPercent, fadeType);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    lastError = ex.Message;
                }
            }
        }

        string message = String.Format("Processed {0} clip(s)!\n\nSplice at: {1:F3}s\nVolume duck: {2:F1} dB\nOffset: {3}%\nFade type: {4}",
            successCount, spliceTime, duckDb, (int)(offsetPercent * 100), fadeType);
        if (errorCount > 0)
        {
            message += String.Format("\n\n{0} clip(s) had errors:\n{1}", errorCount, lastError);
        }
        MessageBox.Show(message, "Chorus Crisp Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ProcessEvent(TrackEvent originalEvent, double spliceTime, 
        double duckDb, double offsetPercent, CurveType fadeType)
    {
        Timecode eventLength = originalEvent.Length;
        Timecode spliceOffset = Timecode.FromSeconds(spliceTime);

        if (eventLength.ToMilliseconds() < spliceTime * 1000 + 10)
        {
            return;
        }

        TrackEvent secondEvent = originalEvent.Split(spliceOffset);

        if (secondEvent == null)
            return;

        Timecode overlapDuration = Timecode.FromSeconds(spliceTime * offsetPercent);

        AudioEvent audioSecond = secondEvent as AudioEvent;
        if (audioSecond != null)
        {
            Timecode newStart = secondEvent.Start - overlapDuration;
            
            foreach (Take take in audioSecond.Takes)
            {
                take.Offset = take.Offset - overlapDuration;
            }
            
            secondEvent.Start = newStart;
            secondEvent.Length = secondEvent.Length + overlapDuration;
            
            double linearGain = Math.Pow(10.0, duckDb / 20.0);
            audioSecond.NormalizeGain = linearGain;
        }

        secondEvent.FadeIn.Length = overlapDuration;
        secondEvent.FadeIn.Curve = fadeType;

        originalEvent.FadeOut.Length = overlapDuration;
        originalEvent.FadeOut.Curve = fadeType;
    }
}

public class ChorusCrispSettings
{
    public int SpliceValue = 47;
    public int CrispValue = 100;
    public int OffsetValue = 0;
    public int CurveIndex = 4;
    
    private static string GetSettingsPath()
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChorusCrisp");
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);
        return Path.Combine(folder, "settings.txt");
    }
    
    public void Save()
    {
        try
        {
            string[] lines = new string[]
            {
                "SpliceValue=" + SpliceValue.ToString(),
                "CrispValue=" + CrispValue.ToString(),
                "OffsetValue=" + OffsetValue.ToString(),
                "CurveIndex=" + CurveIndex.ToString()
            };
            File.WriteAllLines(GetSettingsPath(), lines);
        }
        catch { }
    }
    
    public static ChorusCrispSettings Load()
    {
        ChorusCrispSettings settings = new ChorusCrispSettings();
        try
        {
            string path = GetSettingsPath();
            if (File.Exists(path))
            {
                string[] lines = File.ReadAllLines(path);
                foreach (string line in lines)
                {
                    string[] parts = line.Split('=');
                    if (parts.Length == 2)
                    {
                        string key = parts[0].Trim();
                        int value;
                        if (Int32.TryParse(parts[1].Trim(), out value))
                        {
                            if (key == "SpliceValue") settings.SpliceValue = value;
                            else if (key == "CrispValue") settings.CrispValue = value;
                            else if (key == "OffsetValue") settings.OffsetValue = value;
                            else if (key == "CurveIndex") settings.CurveIndex = value;
                        }
                    }
                }
            }
        }
        catch { }
        return settings;
    }
}

public class ChorusCrispPreset
{
    public string Name;
    public int SpliceValue;
    public int CrispValue;
    public int OffsetValue;
    public int CurveIndex;
    public bool IsUserPreset;
    public bool IsSeparator;
    
    public ChorusCrispPreset(string name, int splice, int crisp, int offset, int curve, bool isUser = false)
    {
        Name = name;
        SpliceValue = splice;
        CrispValue = crisp;
        OffsetValue = offset;
        CurveIndex = curve;
        IsUserPreset = isUser;
        IsSeparator = false;
    }
    
    public static ChorusCrispPreset CreateSeparator(string label)
    {
        ChorusCrispPreset sep = new ChorusCrispPreset(label, -1, -1, -1, -1, false);
        sep.IsSeparator = true;
        return sep;
    }
    
    public override string ToString() { return Name; }
}

public class ChorusCrispUserPresets
{
    private static string GetUserPresetsPath()
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChorusCrisp");
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);
        return Path.Combine(folder, "userpresets.txt");
    }
    
    public static List<ChorusCrispPreset> Load()
    {
        List<ChorusCrispPreset> list = new List<ChorusCrispPreset>();
        try
        {
            string path = GetUserPresetsPath();
            if (File.Exists(path))
            {
                string[] lines = File.ReadAllLines(path);
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string[] parts = line.Split('|');
                    if (parts.Length == 5)
                    {
                        int splice, crisp, offset, curve;
                        if (int.TryParse(parts[1], out splice) &&
                            int.TryParse(parts[2], out crisp) &&
                            int.TryParse(parts[3], out offset) &&
                            int.TryParse(parts[4], out curve))
                        {
                            list.Add(new ChorusCrispPreset(parts[0], splice, crisp, offset, curve, true));
                        }
                    }
                }
            }
        }
        catch { }
        return list;
    }
    
    public static void Save(List<ChorusCrispPreset> userPresets)
    {
        try
        {
            List<string> lines = new List<string>();
            foreach (ChorusCrispPreset p in userPresets)
            {
                if (p.IsUserPreset && !p.IsSeparator)
                {
                    lines.Add(string.Format("{0}|{1}|{2}|{3}|{4}", 
                        p.Name, p.SpliceValue, p.CrispValue, p.OffsetValue, p.CurveIndex));
                }
            }
            File.WriteAllLines(GetUserPresetsPath(), lines.ToArray());
        }
        catch { }
    }
    
    public static void Add(ChorusCrispPreset preset)
    {
        List<ChorusCrispPreset> existing = Load();
        existing.Add(preset);
        Save(existing);
    }
    
    public static void Delete(string presetName)
    {
        List<ChorusCrispPreset> existing = Load();
        existing.RemoveAll(p => p.Name == presetName);
        Save(existing);
    }
}

public class PresetNameDialog : Form
{
    private TextBox nameBox;
    private Button okButton;
    private Button cancelButton;
    
    public string PresetName { get { return nameBox.Text.Trim(); } }
    
    public PresetNameDialog()
    {
        this.Text = "Save Preset";
        this.ClientSize = new Size(300, 120);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterParent;
        this.BackColor = Color.FromArgb(45, 45, 48);
        this.ForeColor = Color.White;
        
        Label prompt = new Label();
        prompt.Text = "Enter preset name:";
        prompt.Location = new Point(15, 15);
        prompt.Size = new Size(270, 20);
        prompt.ForeColor = Color.White;
        this.Controls.Add(prompt);
        
        nameBox = new TextBox();
        nameBox.Location = new Point(15, 40);
        nameBox.Size = new Size(270, 25);
        nameBox.Font = new Font(this.Font.FontFamily, 10);
        nameBox.MaxLength = 50;
        this.Controls.Add(nameBox);
        
        okButton = new Button();
        okButton.Text = "Save";
        okButton.Location = new Point(70, 75);
        okButton.Size = new Size(75, 30);
        okButton.BackColor = Color.FromArgb(0, 122, 204);
        okButton.ForeColor = Color.White;
        okButton.FlatStyle = FlatStyle.Flat;
        okButton.DialogResult = DialogResult.OK;
        okButton.Click += OkButton_Click;
        this.Controls.Add(okButton);
        
        cancelButton = new Button();
        cancelButton.Text = "Cancel";
        cancelButton.Location = new Point(155, 75);
        cancelButton.Size = new Size(75, 30);
        cancelButton.BackColor = Color.FromArgb(80, 80, 80);
        cancelButton.ForeColor = Color.White;
        cancelButton.FlatStyle = FlatStyle.Flat;
        cancelButton.DialogResult = DialogResult.Cancel;
        this.Controls.Add(cancelButton);
        
        this.AcceptButton = okButton;
        this.CancelButton = cancelButton;
    }
    
    private void OkButton_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(nameBox.Text))
        {
            MessageBox.Show("Please enter a preset name.", "Save Preset", 
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            this.DialogResult = DialogResult.None;
            return;
        }
        
        // Check for invalid characters
        if (nameBox.Text.Contains("|"))
        {
            MessageBox.Show("Preset name cannot contain the '|' character.", "Save Preset",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            this.DialogResult = DialogResult.None;
            return;
        }
    }
}

public class ChorusCrispDialog : Form
{
    private ComboBox presetCombo;
    private Button savePresetButton;
    private Button deletePresetButton;
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
    private List<ChorusCrispPreset> allPresets;
    private int userPresetStartIndex = -1;

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
        allPresets = new List<ChorusCrispPreset>();
        
        // Built-in presets
        allPresets.Add(new ChorusCrispPreset("Custom", -1, -1, -1, -1));
        allPresets.Add(new ChorusCrispPreset("Jario Style", 50, 80, 20, 4));
        allPresets.Add(new ChorusCrispPreset("Standard", 40, 80, 10, 0));
        allPresets.Add(new ChorusCrispPreset("Snappy", 35, 67, 5, 1));
        
        // Load user presets
        List<ChorusCrispPreset> userPresets = ChorusCrispUserPresets.Load();
        if (userPresets.Count > 0)
        {
            allPresets.Add(ChorusCrispPreset.CreateSeparator("─── User Presets ───"));
            userPresetStartIndex = allPresets.Count;
            allPresets.AddRange(userPresets);
        }
    }
    
    private void RefreshPresetList()
    {
        isLoadingPreset = true;
        
        // Remember current selection if it's a user preset
        string currentSelection = null;
        if (presetCombo.SelectedIndex >= 0)
        {
            ChorusCrispPreset selected = allPresets[presetCombo.SelectedIndex];
            if (selected.IsUserPreset)
                currentSelection = selected.Name;
        }
        
        // Rebuild preset list
        allPresets.Clear();
        presetCombo.Items.Clear();
        
        // Built-in presets
        allPresets.Add(new ChorusCrispPreset("Custom", -1, -1, -1, -1));
        allPresets.Add(new ChorusCrispPreset("Jario Style", 50, 80, 20, 4));
        allPresets.Add(new ChorusCrispPreset("Standard", 40, 80, 10, 0));
        allPresets.Add(new ChorusCrispPreset("Snappy", 35, 67, 5, 1));
        
        // Load user presets
        List<ChorusCrispPreset> userPresets = ChorusCrispUserPresets.Load();
        if (userPresets.Count > 0)
        {
            allPresets.Add(ChorusCrispPreset.CreateSeparator("─── User Presets ───"));
            userPresetStartIndex = allPresets.Count;
            allPresets.AddRange(userPresets);
        }
        else
        {
            userPresetStartIndex = -1;
        }
        
        // Repopulate combo box
        foreach (ChorusCrispPreset preset in allPresets)
        {
            presetCombo.Items.Add(preset);
        }
        
        // Try to restore selection
        int newIndex = 0;
        if (currentSelection != null)
        {
            for (int i = 0; i < allPresets.Count; i++)
            {
                if (allPresets[i].Name == currentSelection && allPresets[i].IsUserPreset)
                {
                    newIndex = i;
                    break;
                }
            }
        }
        
        presetCombo.SelectedIndex = newIndex;
        isLoadingPreset = false;
        UpdateDeleteButtonState();
    }

    private void InitializeComponent()
    {
        this.AutoScaleMode = AutoScaleMode.Dpi;
        this.AutoScaleDimensions = new SizeF(96F, 96F);
        
        this.Text = "Chorus Crisp";
        this.ClientSize = new Size(450, 460);
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
        presetCombo.Size = new Size(250, 28);
        presetCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        presetCombo.Font = new Font(this.Font.FontFamily, 10);
        foreach (ChorusCrispPreset preset in allPresets)
        {
            presetCombo.Items.Add(preset);
        }
        presetCombo.SelectedIndex = 0;
        presetCombo.SelectedIndexChanged += PresetCombo_SelectedIndexChanged;
        this.Controls.Add(presetCombo);
        
        // Save preset button
        savePresetButton = new Button();
        savePresetButton.Text = "Save";
        savePresetButton.Location = new Point(340, yPos - 3);
        savePresetButton.Size = new Size(50, 26);
        savePresetButton.BackColor = Color.FromArgb(60, 60, 65);
        savePresetButton.ForeColor = Color.White;
        savePresetButton.FlatStyle = FlatStyle.Flat;
        savePresetButton.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
        savePresetButton.Click += SavePresetButton_Click;
        this.Controls.Add(savePresetButton);
        
        // Delete preset button
        deletePresetButton = new Button();
        deletePresetButton.Text = "Del";
        deletePresetButton.Location = new Point(395, yPos - 3);
        deletePresetButton.Size = new Size(40, 26);
        deletePresetButton.BackColor = Color.FromArgb(60, 60, 65);
        deletePresetButton.ForeColor = Color.White;
        deletePresetButton.FlatStyle = FlatStyle.Flat;
        deletePresetButton.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
        deletePresetButton.Enabled = false;
        deletePresetButton.Click += DeletePresetButton_Click;
        this.Controls.Add(deletePresetButton);

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
    
    private void SavePresetButton_Click(object sender, EventArgs e)
    {
        using (PresetNameDialog nameDialog = new PresetNameDialog())
        {
            if (nameDialog.ShowDialog(this) == DialogResult.OK)
            {
                string name = nameDialog.PresetName;
                
                // Check if name already exists
                foreach (ChorusCrispPreset p in allPresets)
                {
                    if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && !p.IsSeparator)
                    {
                        DialogResult overwrite = MessageBox.Show(
                            string.Format("A preset named '{0}' already exists.\nDo you want to overwrite it?", name),
                            "Preset Exists", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        
                        if (overwrite == DialogResult.No)
                            return;
                        
                        // Delete existing and add new
                        if (p.IsUserPreset)
                        {
                            ChorusCrispUserPresets.Delete(name);
                        }
                        else
                        {
                            MessageBox.Show("Cannot overwrite built-in presets.\nPlease choose a different name.",
                                "Save Preset", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        break;
                    }
                }
                
                // Create and save new preset
                ChorusCrispPreset newPreset = new ChorusCrispPreset(
                    name, spliceSlider.Value, crispSlider.Value, 
                    offsetSlider.Value, curveCombo.SelectedIndex, true);
                
                ChorusCrispUserPresets.Add(newPreset);
                RefreshPresetList();
                
                // Select the newly saved preset
                for (int i = 0; i < allPresets.Count; i++)
                {
                    if (allPresets[i].Name == name && allPresets[i].IsUserPreset)
                    {
                        presetCombo.SelectedIndex = i;
                        break;
                    }
                }
            }
        }
    }
    
    private void DeletePresetButton_Click(object sender, EventArgs e)
    {
        if (presetCombo.SelectedIndex < 0) return;
        
        ChorusCrispPreset selected = allPresets[presetCombo.SelectedIndex];
        if (!selected.IsUserPreset) return;
        
        DialogResult confirm = MessageBox.Show(
            string.Format("Delete preset '{0}'?", selected.Name),
            "Delete Preset", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        
        if (confirm == DialogResult.Yes)
        {
            ChorusCrispUserPresets.Delete(selected.Name);
            RefreshPresetList();
            presetCombo.SelectedIndex = 0;
        }
    }
    
    private void UpdateDeleteButtonState()
    {
        if (presetCombo.SelectedIndex >= 0)
        {
            ChorusCrispPreset selected = allPresets[presetCombo.SelectedIndex];
            deletePresetButton.Enabled = selected.IsUserPreset;
        }
        else
        {
            deletePresetButton.Enabled = false;
        }
    }
    
    private void PresetCombo_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (presetCombo.SelectedIndex < 0) return;
        
        ChorusCrispPreset preset = allPresets[presetCombo.SelectedIndex];
        
        // Skip separator items
        if (preset.IsSeparator)
        {
            // Move to next valid item
            if (presetCombo.SelectedIndex + 1 < presetCombo.Items.Count)
            {
                presetCombo.SelectedIndex++;
            }
            else
            {
                presetCombo.SelectedIndex = 0;
            }
            return;
        }
        
        UpdateDeleteButtonState();
        
        if (preset.SpliceValue < 0) return; // Custom preset
        
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
        // Check if current values match any preset (including user presets)
        for (int i = 1; i < allPresets.Count; i++)
        {
            ChorusCrispPreset p = allPresets[i];
            if (p.IsSeparator) continue;
            
            if (spliceSlider.Value == p.SpliceValue &&
                crispSlider.Value == p.CrispValue &&
                offsetSlider.Value == p.OffsetValue &&
                curveCombo.SelectedIndex == p.CurveIndex)
            {
                isLoadingPreset = true;
                presetCombo.SelectedIndex = i;
                isLoadingPreset = false;
                UpdateDeleteButtonState();
                return;
            }
        }
        // No match, set to Custom
        isLoadingPreset = true;
        presetCombo.SelectedIndex = 0;
        isLoadingPreset = false;
        UpdateDeleteButtonState();
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
