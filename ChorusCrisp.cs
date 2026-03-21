// ChorusCrisp v2.0 — Vegas Pro Script
// Vocal chop layering with Fluent dark UI and interactive timeline preview
//
// Installation: Drop into Vegas Pro Script Menu folder
//   e.g. C:\Program Files\VEGAS\VEGAS Pro 21.0\Script Menu\
// Usage: Select audio event(s), then Tools > Scripting > ChorusCrisp

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using ScriptPortal.Vegas;

// ══════════════════════════════════════════════════════════════
//  ENTRY POINT — Processing Logic
// ══════════════════════════════════════════════════════════════
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
            foreach (TrackEvent ev in track.Events)
                if (ev.Selected && ev.IsAudio())
                    selectedEvents.Add(ev);

        if (selectedEvents.Count == 0)
        {
            MessageBox.Show("No audio clips selected!\nSelect some audio events and try again.",
                "Chorus Crisp", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ChorusCrispDialog dialog = new ChorusCrispDialog(selectedEvents.Count);
        if (dialog.ShowDialog() != DialogResult.OK) return;

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
                try { ProcessEvent(ev, spliceTime, duckDb, offsetPercent, fadeType); successCount++; }
                catch (Exception ex) { errorCount++; lastError = ex.Message; }
            }
        }

        string message = String.Format("Processed {0} clip(s)!\n\nSplice at: {1:F3}s\nVolume duck: {2:F1} dB\nOffset: {3}%\nFade type: {4}",
            successCount, spliceTime, duckDb, (int)(offsetPercent * 100), fadeType);
        if (errorCount > 0) message += String.Format("\n\n{0} clip(s) had errors:\n{1}", errorCount, lastError);
        MessageBox.Show(message, "Chorus Crisp Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ProcessEvent(TrackEvent originalEvent, double spliceTime, double duckDb, double offsetPercent, CurveType fadeType)
    {
        Timecode eventLength = originalEvent.Length;
        Timecode spliceOffset = Timecode.FromSeconds(spliceTime);
        if (eventLength.ToMilliseconds() < spliceTime * 1000 + 10) return;

        // STEP 1: Nuke ALL fades on the original event BEFORE splitting
        // This removes any user-applied or inherited fades
        originalEvent.FadeIn.Length = Timecode.FromSeconds(0);
        originalEvent.FadeIn.Curve = fadeType;
        originalEvent.FadeOut.Length = Timecode.FromSeconds(0);
        originalEvent.FadeOut.Curve = fadeType;

        // STEP 2: Split
        TrackEvent secondEvent = originalEvent.Split(spliceOffset);
        if (secondEvent == null) return;

        // STEP 3: Nuke ALL fades on BOTH events AGAIN
        // Vegas adds "quick fades" automatically on every split — kill them
        originalEvent.FadeIn.Length = Timecode.FromSeconds(0);
        originalEvent.FadeIn.Curve = fadeType;
        originalEvent.FadeOut.Length = Timecode.FromSeconds(0);
        originalEvent.FadeOut.Curve = fadeType;
        secondEvent.FadeIn.Length = Timecode.FromSeconds(0);
        secondEvent.FadeIn.Curve = fadeType;
        secondEvent.FadeOut.Length = Timecode.FromSeconds(0);
        secondEvent.FadeOut.Curve = fadeType;

        // STEP 4: Position the overlap and apply gain
        Timecode overlapDuration = Timecode.FromSeconds(spliceTime * offsetPercent);
        AudioEvent audioSecond = secondEvent as AudioEvent;
        if (audioSecond != null)
        {
            Timecode newStart = secondEvent.Start - overlapDuration;
            foreach (Take take in audioSecond.Takes) take.Offset = take.Offset - overlapDuration;
            secondEvent.Start = newStart;
            secondEvent.Length = secondEvent.Length + overlapDuration;
            double linearGain = Math.Pow(10.0, duckDb / 20.0);
            audioSecond.NormalizeGain = audioSecond.NormalizeGain * linearGain;
        }

        // STEP 5: Set crossfade on a guaranteed clean slate
        // Set curve type FIRST, then length — prevents Vegas from
        // applying a default curve when the length gets set
        originalEvent.FadeOut.Curve = fadeType;
        originalEvent.FadeOut.Length = overlapDuration;
        secondEvent.FadeIn.Curve = fadeType;
        secondEvent.FadeIn.Length = overlapDuration;
    }
}

// ══════════════════════════════════════════════════════════════
//  THEME — Fluent Dark (matches ScreenShake)
// ══════════════════════════════════════════════════════════════
public static class Theme
{
    public static readonly Color Bg          = Color.FromArgb(32, 32, 32);
    public static readonly Color Card        = Color.FromArgb(44, 44, 44);
    public static readonly Color CardHover   = Color.FromArgb(52, 52, 52);
    public static readonly Color Border      = Color.FromArgb(60, 60, 60);
    public static readonly Color TitleBar    = Color.FromArgb(28, 28, 28);
    public static readonly Color TextPrimary = Color.FromArgb(255, 255, 255);
    public static readonly Color TextSecond  = Color.FromArgb(157, 157, 157);
    public static readonly Color Accent      = Color.FromArgb(0, 200, 220);
    public static readonly Color AccentHover = Color.FromArgb(0, 170, 190);
    public static readonly Color Danger      = Color.FromArgb(200, 60, 60);
}

// ══════════════════════════════════════════════════════════════
//  SETTINGS PERSISTENCE
// ══════════════════════════════════════════════════════════════
public class ChorusCrispSettings
{
    public int SpliceValue = 47;
    public int CrispValue = 100;
    public int OffsetValue = 0;
    public int CurveIndex = 4;

    private static string GetSettingsPath()
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChorusCrisp");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
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

// ══════════════════════════════════════════════════════════════
//  PRESETS
// ══════════════════════════════════════════════════════════════
public class ChorusCrispPreset
{
    public string Name;
    public int SpliceValue, CrispValue, OffsetValue, CurveIndex;
    public bool IsUserPreset, IsSeparator;

    public ChorusCrispPreset(string name, int splice, int crisp, int offset, int curve, bool isUser = false)
    { Name = name; SpliceValue = splice; CrispValue = crisp; OffsetValue = offset; CurveIndex = curve; IsUserPreset = isUser; IsSeparator = false; }

    public static ChorusCrispPreset CreateSeparator(string label)
    { ChorusCrispPreset s = new ChorusCrispPreset(label, -1, -1, -1, -1); s.IsSeparator = true; return s; }

    public override string ToString() { return Name; }
}

public class ChorusCrispUserPresets
{
    private static string GetUserPresetsPath()
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChorusCrisp");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
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
                        int sp, cr, of, cu;
                        if (int.TryParse(parts[1], out sp) && int.TryParse(parts[2], out cr) &&
                            int.TryParse(parts[3], out of) && int.TryParse(parts[4], out cu))
                            list.Add(new ChorusCrispPreset(parts[0], sp, cr, of, cu, true));
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
                if (p.IsUserPreset && !p.IsSeparator)
                    lines.Add(String.Format("{0}|{1}|{2}|{3}|{4}", p.Name, p.SpliceValue, p.CrispValue, p.OffsetValue, p.CurveIndex));
            File.WriteAllLines(GetUserPresetsPath(), lines.ToArray());
        }
        catch { }
    }

    public static void Add(ChorusCrispPreset preset)
    { List<ChorusCrispPreset> ex = Load(); ex.Add(preset); Save(ex); }

    public static void Delete(string presetName)
    { List<ChorusCrispPreset> ex = Load(); ex.RemoveAll(p => p.Name == presetName); Save(ex); }
}

// ══════════════════════════════════════════════════════════════
//  PRESET NAME DIALOG
// ══════════════════════════════════════════════════════════════
public class PresetNameDialog : Form
{
    private TextBox nameBox;
    public string PresetName { get { return nameBox.Text.Trim(); } }

    public PresetNameDialog()
    {
        Text = "Save Preset"; ClientSize = new Size(300, 120);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Bg; ForeColor = Theme.TextPrimary;
        Font = new Font("Segoe UI", 9.5f);

        Label prompt = new Label();
        prompt.Text = "Enter preset name:"; prompt.Location = new Point(15, 15);
        prompt.Size = new Size(270, 20); prompt.ForeColor = Theme.TextPrimary;
        Controls.Add(prompt);

        nameBox = new TextBox();
        nameBox.Location = new Point(15, 40); nameBox.Size = new Size(270, 25);
        nameBox.Font = new Font("Segoe UI", 10f); nameBox.MaxLength = 50;
        nameBox.BackColor = Theme.Card; nameBox.ForeColor = Theme.TextPrimary;
        Controls.Add(nameBox);

        FluentButton btnSave = new FluentButton("Save", true);
        btnSave.Location = new Point(100, 78); btnSave.Size = new Size(80, 30);
        btnSave.Click += delegate {
            if (string.IsNullOrWhiteSpace(nameBox.Text))
            { MessageBox.Show("Please enter a preset name.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (nameBox.Text.Contains("|"))
            { MessageBox.Show("Name cannot contain '|'.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            DialogResult = DialogResult.OK; Close();
        };
        Controls.Add(btnSave);

        FluentButton btnCancel = new FluentButton("Cancel", false);
        btnCancel.Location = new Point(190, 78); btnCancel.Size = new Size(80, 30);
        btnCancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
        Controls.Add(btnCancel);
    }
}

// ══════════════════════════════════════════════════════════════
//  VEGAS PREVIEW PANEL — GDI+ interactive timeline preview
// ══════════════════════════════════════════════════════════════
public class VegasPreviewPanel : Control
{
    // Parameters (0-100 scale)
    public int SplicePercent = 47;
    public int CrispPercent = 0;   // 0 = no duck, 100 = full -15dB
    public int OffsetPercent = 0;  // 0 = no overlap, 100 = full overlap

    public int CurveIndex = 4;     // 0=Linear,1=Fast,2=Slow,3=Sharp,4=Smooth

    public event EventHandler SpliceChanged;
    public event EventHandler CrispChanged;
    public event EventHandler OffsetChanged;

    private string dragHandle = null;
    private float[] waveA, waveB;

    // Layout cache
    private int rulerH = 22, padX = 12;
    private float pxPerSec, eventY, eventH, aEndX, olStartX, duckRightX;
    private float aStartT = 0.020f;
    private bool hasOverlap;

    static readonly Color VegasBg       = Color.FromArgb(26, 26, 26);
    static readonly Color VegasTrack    = Color.FromArgb(42, 42, 42);
    static readonly Color VegasEvent    = Color.FromArgb(61, 107, 61);
    static readonly Color VegasWave     = Color.FromArgb(92, 184, 92);
    static readonly Color VegasWaveDark = Color.FromArgb(74, 154, 74);
    static readonly Color VegasBorder   = Color.FromArgb(90, 138, 58);
    static readonly Color HandleOffset  = Color.FromArgb(255, 180, 50);
    static readonly Color HandleDuck    = Color.FromArgb(255, 107, 107);
    static readonly Color CrossfadeA    = Color.FromArgb(139, 212, 139);
    static readonly Color CrossfadeB    = Color.FromArgb(106, 170, 106);

    public VegasPreviewPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Default;
        Height = 180;

        // Generate fake waveforms
        waveA = GenWave(200, 42);
        waveB = GenWave(200, 137);
    }

    static float[] GenWave(int len, int seed)
    {
        float[] pts = new float[len];
        int s = seed;
        for (int i = 0; i < len; i++)
        {
            s = (int)(((long)s * 16807) % 2147483647);
            float r1 = (s % 1000) / 1000f;
            s = (int)(((long)s * 16807) % 2147483647);
            float r2 = (s % 1000) / 1000f;
            float env = (float)Math.Sin((double)i / len * Math.PI) * 0.6f + 0.4f;
            pts[i] = (r1 * 0.6f + r2 * 0.4f) * env;
        }
        return pts;
    }

    float EvalCurve(float t, int curve, bool fadeIn)
    {
        float v;
        switch (curve)
        {
            case 1: v = 1f - (1f - t) * (1f - t); break;         // Fast
            case 2: v = t * t; break;                              // Slow
            case 3: v = t * t * t; break;                          // Sharp
            case 4: v = t * t * (3f - 2f * t); break;             // Smooth
            default: v = t; break;                                 // Linear
        }
        return fadeIn ? v : 1f - v;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        int W = Width, H = Height;

        float spliceTime = 0.020f + (SplicePercent / 100f) * 0.040f;
        float duckDb = (CrispPercent / 100f) * -15f;
        float duckLin = (float)Math.Pow(10.0, duckDb / 20.0);
        float overlapTime = (OffsetPercent / 100f) * spliceTime;
        hasOverlap = overlapTime > 0.001f;

        float trackY = rulerH + 4;
        float trackH = H - trackY - 8;
        float ePadY = 6;
        eventY = trackY + ePadY;
        eventH = trackH - ePadY * 2;
        float waveH = eventH - 18;
        float waveYY = eventY + 15;

        float totalDur = 0.300f;
        pxPerSec = (W - padX * 2) / totalDur;

        float aEndT = aStartT + spliceTime;
        float bStartT = aEndT - overlapTime;
        float bEndT = 0.280f;

        float aStartX = padX + aStartT * pxPerSec;
        aEndX = padX + aEndT * pxPerSec;
        float bStartX = padX + bStartT * pxPerSec;
        float bEndX = padX + bEndT * pxPerSec;
        olStartX = bStartX;
        float olEndX = aEndX;
        float olW = olEndX - olStartX;

        duckRightX = bEndX;

        // ── Background ──
        using (SolidBrush bg = new SolidBrush(VegasBg)) g.FillRectangle(bg, 0, 0, W, H);

        // ── Ruler ──
        using (SolidBrush rb = new SolidBrush(Color.FromArgb(51, 51, 51))) g.FillRectangle(rb, 0, 0, W, rulerH);
        using (Pen lp = new Pen(Color.FromArgb(68, 68, 68))) g.DrawLine(lp, 0, rulerH, W, rulerH);

        using (Font rf = new Font("Segoe UI", 7.5f))
        using (SolidBrush rtb = new SolidBrush(Theme.TextSecond))
        {
            for (int ms = 0; ms <= 300; ms += 20)
            {
                float x = padX + (ms / 1000f) * pxPerSec;
                bool major = ms % 100 == 0;
                using (Pen tp = new Pen(major ? Color.FromArgb(102, 102, 102) : Color.FromArgb(68, 68, 68)))
                    g.DrawLine(tp, x, major ? 6 : 12, x, rulerH);
                if (major) g.DrawString(ms + "ms", rf, rtb, x + 2, 4);
            }
        }

        // ── Track bg ──
        using (SolidBrush tb = new SolidBrush(VegasTrack)) g.FillRectangle(tb, 0, trackY, W, trackH);

        // ── Event A ──
        using (SolidBrush ab = new SolidBrush(VegasEvent))
            g.FillRectangle(ab, aStartX, eventY, aEndX - aStartX, eventH);

        int waveSliceLen = Math.Max(1, (int)(200 * spliceTime / 0.060f));
        DrawWave(g, waveA, waveSliceLen, aStartX + 1, aEndX - aStartX - 2, waveYY, waveH, 1f, VegasWave);

        using (Pen bp = new Pen(VegasBorder)) g.DrawRectangle(bp, aStartX, eventY, aEndX - aStartX, eventH);

        using (Font lf = new Font("Segoe UI", 8f, FontStyle.Bold))
        using (SolidBrush lb = new SolidBrush(Color.FromArgb(204, 204, 204)))
        {
            if (aEndX - aStartX > 50) g.DrawString("Event A", lf, lb, aStartX + 4, eventY + 2);
        }

        // ── Event B ──
        int bR = (int)(61 * duckLin + 35 * (1 - duckLin));
        int bG = (int)(107 * duckLin + 65 * (1 - duckLin));
        int bB = (int)(61 * duckLin + 35 * (1 - duckLin));
        Color eventBCol = Color.FromArgb(bR, bG, bB);

        if (hasOverlap)
        {
            using (SolidBrush ob = new SolidBrush(Color.FromArgb(166, eventBCol)))
                g.FillRectangle(ob, bStartX, eventY, olEndX - bStartX, eventH);
            using (SolidBrush fb = new SolidBrush(eventBCol))
                g.FillRectangle(fb, olEndX, eventY, bEndX - olEndX, eventH);
        }
        else
        {
            using (SolidBrush fb = new SolidBrush(eventBCol))
                g.FillRectangle(fb, bStartX, eventY, bEndX - bStartX, eventH);
        }

        DrawWave(g, waveB, 200, bStartX + 1, bEndX - bStartX - 2, waveYY, waveH, duckLin, VegasWaveDark);

        using (Pen bp = new Pen(VegasBorder)) g.DrawRectangle(bp, bStartX, eventY, bEndX - bStartX, eventH);

        using (Font lf = new Font("Segoe UI", 8f, FontStyle.Bold))
        using (SolidBrush lb = new SolidBrush(Color.FromArgb(204, 204, 204)))
        {
            float labelX = hasOverlap ? Math.Max(olEndX + 4, bStartX + 4) : bStartX + 4;
            g.DrawString("Event B (split)", lf, lb, labelX, eventY + 2);
        }

        // ── Crossfade X lines ──
        if (hasOverlap && olW > 2)
        {
            using (Pen pA = new Pen(CrossfadeA, 2.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (Pen pB = new Pen(CrossfadeB, 2.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                List<PointF> fadeOut = new List<PointF>();
                List<PointF> fadeIn = new List<PointF>();
                for (float px = olStartX; px <= olEndX; px += 1f)
                {
                    float t = (px - olStartX) / olW;
                    float vOut = EvalCurve(t, CurveIndex, false);
                    float vIn = EvalCurve(t, CurveIndex, true);
                    fadeOut.Add(new PointF(px, eventY + (1f - vOut) * eventH));
                    fadeIn.Add(new PointF(px, eventY + (1f - vIn) * eventH));
                }
                if (fadeOut.Count > 1) g.DrawLines(pA, fadeOut.ToArray());
                if (fadeIn.Count > 1) g.DrawLines(pB, fadeIn.ToArray());
            }

            // Crossfade duration label
            using (Font cf = new Font("Segoe UI", 7f, FontStyle.Bold))
            using (SolidBrush cb = new SolidBrush(HandleOffset))
            {
                string olLabel = String.Format("{0:F1}ms crossfade", overlapTime * 1000);
                SizeF sz = g.MeasureString(olLabel, cf);
                g.DrawString(olLabel, cf, cb, (olStartX + olEndX) / 2 - sz.Width / 2, eventY + eventH + ePadY - 1);
            }
        }

        // ── "fx" badge ──
        using (SolidBrush fxBg = new SolidBrush(Color.FromArgb(140, 0, 0, 0)))
            g.FillRectangle(fxBg, bEndX - 24, eventY + eventH - 16, 20, 13);
        using (Font fxf = new Font("Segoe UI", 6f, FontStyle.Bold))
        using (SolidBrush fxb = new SolidBrush(Color.FromArgb(255, 204, 0)))
            g.DrawString("fx", fxf, fxb, bEndX - 21, eventY + eventH - 15);

        // ═══════════════════════════════
        //  INTERACTIVE HANDLES
        // ═══════════════════════════════
        Font hf = new Font("Segoe UI", 6.5f, FontStyle.Bold);

        // Handle 1: Offset — left border of crossfade
        if (hasOverlap)
        {
            using (Pen op = new Pen(HandleOffset, 2f))
                g.DrawLine(op, olStartX, eventY, olStartX, eventY + eventH);
            // Grip dots
            float gy = eventY + eventH / 2;
            using (SolidBrush gd = new SolidBrush(HandleOffset))
            { for (int dy = -8; dy <= 8; dy += 8) g.FillEllipse(gd, olStartX - 2, gy + dy - 2, 4, 4); }
            using (SolidBrush hb = new SolidBrush(HandleOffset))
                g.DrawString("OFFSET", hf, hb, olStartX - 20, eventY - 12);
        }

        // Handle 2: Splice — dashed cyan line
        float sx = aEndX;
        using (Pen sp = new Pen(Theme.Accent, 2f) { DashPattern = new float[] { 4, 2 } })
            g.DrawLine(sp, sx, rulerH, sx, H);
        float sgy = eventY + eventH / 2;
        using (SolidBrush sd = new SolidBrush(Theme.Accent))
        { for (int dy = -8; dy <= 8; dy += 8) g.FillEllipse(sd, sx - 2, sgy + dy - 2, 4, 4); }
        using (SolidBrush sb = new SolidBrush(Theme.Accent))
            g.DrawString(String.Format("SPLICE {0:F1}ms", spliceTime * 1000), hf, sb, sx + 4, rulerH + 3);

        // Handle 3: Duck — horizontal line across Event B at duck level
        float duckNonOlStart = hasOverlap ? olEndX : bStartX;
        float dgm = eventY + eventH * (CrispPercent / 100f);
        using (Pen dp = new Pen(HandleDuck, 2f))
            g.DrawLine(dp, duckNonOlStart, dgm, bEndX, dgm);
        // Diamond grip on left end
        using (SolidBrush dd = new SolidBrush(HandleDuck))
        {
            PointF[] diamond = new PointF[]
            { new PointF(duckNonOlStart - 5, dgm), new PointF(duckNonOlStart, dgm - 5),
              new PointF(duckNonOlStart + 5, dgm), new PointF(duckNonOlStart, dgm + 5) };
            g.FillPolygon(dd, diamond);
        }
        // Label with dB value
        float duckDbVal = (CrispPercent / 100f) * -15f;
        using (SolidBrush db2 = new SolidBrush(HandleDuck))
            g.DrawString(String.Format("DUCK {0:F1} dB", duckDbVal), hf, db2, bEndX - 80, dgm - 12);

        hf.Dispose();
    }

    void DrawWave(Graphics g, float[] data, int len, float x, float w, float y, float h, float amp, Color col)
    {
        if (len <= 0 || w <= 0) return;
        int count = Math.Min(len, data.Length);
        float midY = y + h / 2;
        float halfH = h / 2;
        float barW = Math.Max(1f, w / count);
        using (SolidBrush b = new SolidBrush(col))
        {
            for (int i = 0; i < count; i++)
            {
                float bx = x + ((float)i / count) * w;
                float bh = data[i] * halfH * amp;
                g.FillRectangle(b, bx, midY - bh, barW + 0.5f, bh * 2);
            }
        }
    }

    // ── Mouse interaction ──
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        string h = HitTest(e.X, e.Y);
        if (h != null) { dragHandle = h; Capture = true; }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragHandle != null)
        {
            ApplyDrag(e.X, e.Y);
        }
        else
        {
            string h = HitTest(e.X, e.Y);
            Cursor = h == "duck" ? Cursors.SizeNS : h != null ? Cursors.SizeWE : Cursors.Default;
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        dragHandle = null; Capture = false;
        Cursor = Cursors.Default;
    }

    string HitTest(int mx, int my)
    {
        int hit = 8;
        if (hasOverlap && Math.Abs(mx - olStartX) < hit && my > eventY && my < eventY + eventH) return "offset";
        if (Math.Abs(mx - aEndX) < hit && my > rulerH) return "splice";
        // Duck: horizontal line across Event B
        float duckY = eventY + eventH * (CrispPercent / 100f);
        float duckLeftX = hasOverlap ? aEndX : aEndX; // left edge of non-overlap Event B
        if (Math.Abs(my - duckY) < hit && mx > duckLeftX && mx < duckRightX) return "duck";
        return null;
    }

    void ApplyDrag(int mx, int my)
    {
        if (dragHandle == "splice")
        {
            float timeAtMx = (mx - padX) / pxPerSec;
            float spliceSec = Math.Max(0.020f, Math.Min(0.060f, timeAtMx - aStartT));
            int pct = (int)(((spliceSec - 0.020f) / 0.040f) * 100);
            SplicePercent = Math.Max(0, Math.Min(100, pct));
            if (SpliceChanged != null) SpliceChanged(this, EventArgs.Empty);
            Invalidate();
        }
        else if (dragHandle == "offset")
        {
            float currentSplice = 0.020f + (SplicePercent / 100f) * 0.040f;
            float spliceLineX = padX + (aStartT + currentSplice) * pxPerSec;
            float overlapPx = spliceLineX - mx;
            float maxOlPx = currentSplice * pxPerSec;
            float ratio = Math.Max(0, Math.Min(1, overlapPx / maxOlPx));
            OffsetPercent = (int)(ratio * 100);
            if (OffsetChanged != null) OffsetChanged(this, EventArgs.Empty);
            Invalidate();
        }
        else if (dragHandle == "duck")
        {
            float ratio = Math.Max(0, Math.Min(1, (my - eventY) / eventH));
            CrispPercent = (int)(ratio * 100);
            if (CrispChanged != null) CrispChanged(this, EventArgs.Empty);
            Invalidate();
        }
    }
}

// ══════════════════════════════════════════════════════════════
//  FLUENT CONTROLS (from ScreenShake)
// ══════════════════════════════════════════════════════════════
public class CardPanel : Panel
{
    public CardPanel()
    { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); BackColor = Theme.Card; }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath p = RR(r, 8))
        { using (SolidBrush b = new SolidBrush(Theme.Card)) g.FillPath(b, p); using (Pen bp = new Pen(Theme.Border)) g.DrawPath(bp, p); }
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    { using (SolidBrush b = new SolidBrush(Parent != null ? Parent.BackColor : Theme.Bg)) e.Graphics.FillRectangle(b, ClientRectangle); }
    public static GraphicsPath RR(Rectangle r, int rad)
    {
        GraphicsPath p = new GraphicsPath(); int d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }
}

public class FluentSlider : Control
{
    string label; int min, max, val; string unit; bool isDrag = false;
    public event EventHandler ValueChanged;
    public int Value
    {
        get { return val; }
        set { int c = Math.Max(min, Math.Min(max, value)); if (c != val) { val = c; Invalidate(); if (ValueChanged != null) ValueChanged(this, EventArgs.Empty); } }
    }
    public void SetValueSilent(int v) { val = Math.Max(min, Math.Min(max, v)); Invalidate(); }

    public FluentSlider(string l, int mn, int mx, int def, string u)
    { label = l; min = mn; max = mx; val = def; unit = u; Height = 44; SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); Cursor = Cursors.Hand; }

    int TL { get { return Width - 220; } }
    int TR { get { return Width - 70; } }
    int TW { get { return TR - TL; } }
    int V2X(int v) { return TL + (int)((double)(v - min) / (max - min) * TW); }
    int X2V(int x) { double r = (double)(x - TL) / TW; return min + (int)(Math.Max(0, Math.Min(1, r)) * (max - min)); }

    protected override void OnMouseDown(MouseEventArgs e) { isDrag = true; Capture = true; int n = X2V(e.X); if (n != val) { val = n; Invalidate(); if (ValueChanged != null) ValueChanged(this, EventArgs.Empty); } }
    protected override void OnMouseMove(MouseEventArgs e) { if (!isDrag) return; int n = X2V(e.X); if (n != val) { val = n; Invalidate(); if (ValueChanged != null) ValueChanged(this, EventArgs.Empty); } }
    protected override void OnMouseUp(MouseEventArgs e) { isDrag = false; Capture = false; }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        using (SolidBrush lb = new SolidBrush(Theme.TextPrimary)) using (Font f = new Font("Segoe UI", 10f))
            g.DrawString(label, f, lb, 14, 12);

        int ty = Height / 2;
        using (Pen tp = new Pen(Color.FromArgb(60, 60, 60), 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(tp, TL, ty, TR, ty);

        int tx = V2X(val);
        using (Pen ap = new Pen(Theme.Accent, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            if (tx > TL) g.DrawLine(ap, TL, ty, tx, ty);

        using (SolidBrush o = new SolidBrush(Theme.Accent)) g.FillEllipse(o, tx - 8, ty - 8, 16, 16);
        using (SolidBrush i = new SolidBrush(Theme.Bg)) g.FillEllipse(i, tx - 5, ty - 5, 10, 10);
        using (SolidBrush d = new SolidBrush(Theme.Accent)) g.FillEllipse(d, tx - 3, ty - 3, 6, 6);

        string vt = val.ToString() + unit;
        using (SolidBrush vb = new SolidBrush(Theme.Accent)) using (Font f = new Font("Segoe UI", 10f, FontStyle.Bold))
        { SizeF sz = g.MeasureString(vt, f); g.DrawString(vt, f, vb, Width - 14 - sz.Width, 12); }
    }
}

public class FluentButton : Control
{
    bool isPrimary, isHover = false;
    public FluentButton(string t, bool p)
    { Text = t; isPrimary = p; Size = new Size(p ? 100 : 80, 32); SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); Cursor = Cursors.Hand; }
    protected override void OnMouseEnter(EventArgs e) { isHover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { isHover = false; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        Color bg, fg, bd;
        if (isPrimary) { bg = isHover ? Theme.AccentHover : Theme.Accent; fg = Color.Black; bd = bg; }
        else { bg = isHover ? Theme.CardHover : Theme.Card; fg = Theme.TextPrimary; bd = Theme.Border; }
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath p = CardPanel.RR(r, 6)) { using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, p); using (Pen bp = new Pen(bd)) g.DrawPath(bp, p); }
        using (SolidBrush tb = new SolidBrush(fg)) using (Font f = new Font("Segoe UI", 9.5f, isPrimary ? FontStyle.Bold : FontStyle.Regular))
        { SizeF sz = g.MeasureString(Text, f); g.DrawString(Text, f, tb, (Width - sz.Width) / 2, (Height - sz.Height) / 2); }
    }
}

public class FluentIconButton : Control
{
    bool isHover = false;
    public FluentIconButton(string icon)
    { Text = icon; Size = new Size(30, 30); SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); Cursor = Cursors.Hand; }
    protected override void OnMouseEnter(EventArgs e) { isHover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { isHover = false; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        if (isHover) { Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1); using (GraphicsPath p = CardPanel.RR(r, 6)) using (SolidBrush b = new SolidBrush(Theme.Danger)) g.FillPath(b, p); }
        Color fg = isHover ? Color.White : Theme.TextSecond;
        using (SolidBrush tb = new SolidBrush(fg)) using (Font f = new Font("Segoe UI", 10f))
        { SizeF sz = g.MeasureString(Text, f); g.DrawString(Text, f, tb, (Width - sz.Width) / 2, (Height - sz.Height) / 2); }
    }
}

// ── Curve selector: segmented button strip ──
public class CurveSelector : Control
{
    static readonly string[] Labels = { "Linear", "Fast", "Slow", "Sharp", "Smooth" };
    public int SelectedIndex = 4;
    public event EventHandler SelectionChanged;

    public CurveSelector()
    { Height = 32; SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); Cursor = Cursors.Hand; }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        int bw = (Width - 14) / Labels.Length;
        int idx = (e.X - 7) / bw;
        if (idx >= 0 && idx < Labels.Length && idx != SelectedIndex)
        { SelectedIndex = idx; Invalidate(); if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        int bw = (Width - 14) / Labels.Length;
        using (Font f = new Font("Segoe UI", 9f))
        {
            for (int i = 0; i < Labels.Length; i++)
            {
                int x = 7 + i * bw;
                Rectangle r = new Rectangle(x, 0, bw - 3, Height - 1);
                bool sel = i == SelectedIndex;
                Color bg = sel ? Color.FromArgb(24, Theme.Accent) : Theme.Bg;
                Color bd = sel ? Theme.Accent : Theme.Border;
                Color fg = sel ? Theme.Accent : Theme.TextSecond;

                using (GraphicsPath p = CardPanel.RR(r, 5))
                { using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, p); using (Pen bp = new Pen(bd)) g.DrawPath(bp, p); }
                using (SolidBrush tb = new SolidBrush(fg))
                {
                    Font df = sel ? new Font("Segoe UI", 9f, FontStyle.Bold) : f;
                    SizeF sz = g.MeasureString(Labels[i], df);
                    g.DrawString(Labels[i], df, tb, x + (bw - 3 - sz.Width) / 2, (Height - sz.Height) / 2);
                    if (sel) df.Dispose();
                }
            }
        }
    }
}

// ── Dark menu renderer ──
public class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    { using (SolidBrush b = new SolidBrush(Color.FromArgb(38, 38, 38))) e.Graphics.FillRectangle(b, e.AffectedBounds); }
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    { using (Pen p = new Pen(Theme.Border)) e.Graphics.DrawRectangle(p, 0, 0, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1); }
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    { if (e.Item.Selected) using (SolidBrush b = new SolidBrush(Theme.CardHover)) e.Graphics.FillRectangle(b, new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height)); }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    { e.TextColor = e.Item.Selected ? Theme.Accent : Theme.TextPrimary; e.TextFont = new Font("Segoe UI", 9f); base.OnRenderItemText(e); }
    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    { using (SolidBrush b = new SolidBrush(Color.FromArgb(38, 38, 38))) e.Graphics.FillRectangle(b, e.AffectedBounds); }
}

// ══════════════════════════════════════════════════════════════
//  MAIN DIALOG
// ══════════════════════════════════════════════════════════════
public class ChorusCrispDialog : Form
{
    private ComboBox presetCombo;
    private FluentSlider spliceSlider, crispSlider, offsetSlider;
    private CurveSelector curveSelector;
    private VegasPreviewPanel preview;
    private FluentButton deletePresetButton;

    private bool isLoadingPreset = false;
    private List<ChorusCrispPreset> allPresets;
    private bool dragging = false;
    private Point dragOffset;

    public int SplicePosition { get { return spliceSlider.Value; } }
    public int Crispness { get { return 100 - crispSlider.Value; } }
    public int OffsetAmount { get { return 100 - offsetSlider.Value; } }
    public CurveType SelectedCurveType
    {
        get
        {
            switch (curveSelector.SelectedIndex)
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

    public ChorusCrispDialog(int eventCount)
    {
        InitializePresets();

        Text = "ChorusCrisp";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(520, 680);
        BackColor = Theme.Bg;
        DoubleBuffered = true;
        Font = new Font("Segoe UI", 9.5f);

        // ── Title Bar ──
        Panel titleBar = new Panel();
        titleBar.Dock = DockStyle.Top; titleBar.Height = 48; titleBar.BackColor = Theme.TitleBar;
        titleBar.MouseDown += TB_Down; titleBar.MouseMove += TB_Move; titleBar.MouseUp += TB_Up;

        Label titleIcon = MakeDragLabel("~", Theme.Accent, new Font("Segoe UI", 14f), 20, 12, titleBar);
        Label titleText = MakeDragLabel("ChorusCrisp", Theme.TextPrimary, new Font("Segoe UI", 11f, FontStyle.Bold), 40, 13, titleBar);
        string sub = "  -  " + eventCount.ToString() + " event" + (eventCount > 1 ? "s" : "") + " selected";
        Label titleSub = MakeDragLabel(sub, Theme.TextSecond, new Font("Segoe UI", 9f), 152, 16, titleBar);

        FluentIconButton btnClose = new FluentIconButton("X");
        btnClose.Location = new Point(480, 9);
        btnClose.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
        titleBar.Controls.Add(btnClose);
        Controls.Add(titleBar);

        // ── Bottom Bar ──
        Panel bottomBar = new Panel();
        bottomBar.Dock = DockStyle.Bottom; bottomBar.Height = 56; bottomBar.BackColor = Theme.TitleBar;
        bottomBar.Paint += delegate(object s, PaintEventArgs pea) { using (Pen p = new Pen(Theme.Border)) pea.Graphics.DrawLine(p, 0, 0, bottomBar.Width, 0); };

        Label verLabel = new Label();
        verLabel.Text = "Settings saved to %APPDATA%\\ChorusCrisp"; verLabel.ForeColor = Theme.TextSecond;
        verLabel.Font = new Font("Segoe UI", 7.5f); verLabel.AutoSize = true; verLabel.Location = new Point(14, 22);
        verLabel.BackColor = Color.Transparent;
        bottomBar.Controls.Add(verLabel);

        FluentButton btnCancel = new FluentButton("Cancel", false);
        btnCancel.Location = new Point(316, 12);
        btnCancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
        bottomBar.Controls.Add(btnCancel);

        FluentButton btnApply = new FluentButton("Apply Crisp", true);
        btnApply.Location = new Point(404, 12);
        btnApply.Click += delegate { SaveSettings(); DialogResult = DialogResult.OK; Close(); };
        bottomBar.Controls.Add(btnApply);
        Controls.Add(bottomBar);

        // ── Content ──
        Panel content = new Panel();
        content.Dock = DockStyle.Fill; content.AutoScroll = true; content.BackColor = Theme.Bg;

        int y = 16;

        // ── PRESET ──
        y = AddSectionLabel(content, "PRESET", y);
        CardPanel presetCard = CreateCard(content, ref y, 40);

        Label presetLbl = new Label();
        presetLbl.Text = "Preset"; presetLbl.ForeColor = Theme.TextSecond; presetLbl.Font = new Font("Segoe UI", 9f);
        presetLbl.AutoSize = true; presetLbl.Location = new Point(14, 11); presetLbl.BackColor = Color.Transparent;
        presetCard.Controls.Add(presetLbl);

        presetCombo = new ComboBox();
        presetCombo.Location = new Point(66, 7); presetCombo.Size = new Size(246, 26);
        presetCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        presetCombo.Font = new Font("Segoe UI", 9f);
        presetCombo.BackColor = Theme.Bg; presetCombo.ForeColor = Theme.TextPrimary;
        foreach (ChorusCrispPreset p in allPresets) presetCombo.Items.Add(p);
        presetCombo.SelectedIndex = 0;
        presetCombo.SelectedIndexChanged += PresetCombo_Changed;
        presetCard.Controls.Add(presetCombo);

        FluentButton saveBtn = new FluentButton("Save", false);
        saveBtn.Location = new Point(320, 5); saveBtn.Size = new Size(60, 28);
        saveBtn.Click += SavePreset_Click;
        presetCard.Controls.Add(saveBtn);

        deletePresetButton = new FluentButton("Del", false);
        deletePresetButton.Location = new Point(386, 5); deletePresetButton.Size = new Size(55, 28);
        deletePresetButton.Enabled = false;
        deletePresetButton.Click += DeletePreset_Click;
        presetCard.Controls.Add(deletePresetButton);

        // ── PARAMETERS ──
        y = AddSectionLabel(content, "PARAMETERS", y);
        CardPanel paramCard = CreateCard(content, ref y, 152);

        spliceSlider = new FluentSlider("Splice Position", 0, 100, 47, "%");
        spliceSlider.Location = new Point(0, 0); spliceSlider.Size = new Size(460, 44);
        spliceSlider.ValueChanged += Slider_Changed;
        paramCard.Controls.Add(spliceSlider);
        AddSep(paramCard, 44);

        crispSlider = new FluentSlider("Volume Duck", 0, 100, 100, "%");
        crispSlider.Location = new Point(0, 50); crispSlider.Size = new Size(460, 44);
        crispSlider.ValueChanged += Slider_Changed;
        paramCard.Controls.Add(crispSlider);
        AddSep(paramCard, 100);

        offsetSlider = new FluentSlider("Offset", 0, 100, 0, "%");
        offsetSlider.Location = new Point(0, 106); offsetSlider.Size = new Size(460, 44);
        offsetSlider.ValueChanged += Slider_Changed;
        paramCard.Controls.Add(offsetSlider);

        // ── CROSSFADE CURVE ──
        y = AddSectionLabel(content, "CROSSFADE CURVE", y);
        CardPanel curveCard = CreateCard(content, ref y, 40);

        curveSelector = new CurveSelector();
        curveSelector.SelectedIndex = 4;
        curveSelector.Location = new Point(7, 4); curveSelector.Size = new Size(446, 32);
        curveSelector.SelectionChanged += delegate { SyncPreviewFromSliders(); if (!isLoadingPreset) CheckForMatchingPreset(); };
        curveCard.Controls.Add(curveSelector);

        // ── PREVIEW ──
        y = AddSectionLabel(content, "PREVIEW", y);
        CardPanel previewCard = CreateCard(content, ref y, 188);

        preview = new VegasPreviewPanel();
        preview.Location = new Point(2, 2); preview.Size = new Size(456, 184);
        preview.SpliceChanged += Preview_SpliceChanged;
        preview.CrispChanged += Preview_CrispChanged;
        preview.OffsetChanged += Preview_OffsetChanged;
        previewCard.Controls.Add(preview);

        Controls.Add(content);
        content.BringToFront();

        LoadSettings();
    }

    // ── Preset management ──
    void InitializePresets()
    {
        allPresets = new List<ChorusCrispPreset>();
        allPresets.Add(new ChorusCrispPreset("Custom", -1, -1, -1, -1));
        allPresets.Add(new ChorusCrispPreset("Jario Style", 50, 80, 20, 4));
        allPresets.Add(new ChorusCrispPreset("Standard", 40, 80, 10, 0));
        allPresets.Add(new ChorusCrispPreset("Snappy", 35, 67, 5, 1));

        List<ChorusCrispPreset> userPresets = ChorusCrispUserPresets.Load();
        if (userPresets.Count > 0)
        {
            allPresets.Add(ChorusCrispPreset.CreateSeparator("─── User Presets ───"));
            allPresets.AddRange(userPresets);
        }
    }

    void RefreshPresetList()
    {
        isLoadingPreset = true;
        string currentName = null;
        if (presetCombo.SelectedIndex >= 0)
        {
            ChorusCrispPreset sel = allPresets[presetCombo.SelectedIndex];
            if (sel.IsUserPreset) currentName = sel.Name;
        }

        allPresets.Clear(); presetCombo.Items.Clear();
        allPresets.Add(new ChorusCrispPreset("Custom", -1, -1, -1, -1));
        allPresets.Add(new ChorusCrispPreset("Jario Style", 50, 80, 20, 4));
        allPresets.Add(new ChorusCrispPreset("Standard", 40, 80, 10, 0));
        allPresets.Add(new ChorusCrispPreset("Snappy", 35, 67, 5, 1));

        List<ChorusCrispPreset> up = ChorusCrispUserPresets.Load();
        if (up.Count > 0) { allPresets.Add(ChorusCrispPreset.CreateSeparator("─── User Presets ───")); allPresets.AddRange(up); }

        foreach (ChorusCrispPreset p in allPresets) presetCombo.Items.Add(p);

        int idx = 0;
        if (currentName != null)
            for (int i = 0; i < allPresets.Count; i++)
                if (allPresets[i].Name == currentName && allPresets[i].IsUserPreset) { idx = i; break; }
        presetCombo.SelectedIndex = idx;
        isLoadingPreset = false;
        UpdateDeleteState();
    }

    void PresetCombo_Changed(object sender, EventArgs e)
    {
        if (presetCombo.SelectedIndex < 0) return;
        ChorusCrispPreset preset = allPresets[presetCombo.SelectedIndex];
        if (preset.IsSeparator)
        { presetCombo.SelectedIndex = presetCombo.SelectedIndex + 1 < presetCombo.Items.Count ? presetCombo.SelectedIndex + 1 : 0; return; }
        UpdateDeleteState();
        if (preset.SpliceValue < 0) return;

        isLoadingPreset = true;
        spliceSlider.Value = preset.SpliceValue;
        crispSlider.Value = preset.CrispValue;
        offsetSlider.Value = preset.OffsetValue;
        curveSelector.SelectedIndex = preset.CurveIndex;
        curveSelector.Invalidate();
        SyncPreviewFromSliders();
        isLoadingPreset = false;
    }

    void SavePreset_Click(object sender, EventArgs e)
    {
        using (PresetNameDialog dlg = new PresetNameDialog())
        {
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            string name = dlg.PresetName;
            foreach (ChorusCrispPreset p in allPresets)
            {
                if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && !p.IsSeparator)
                {
                    if (!p.IsUserPreset) { MessageBox.Show("Cannot overwrite built-in presets.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                    if (MessageBox.Show("Overwrite '" + name + "'?", "Save", MessageBoxButtons.YesNo) == DialogResult.No) return;
                    ChorusCrispUserPresets.Delete(name); break;
                }
            }
            ChorusCrispUserPresets.Add(new ChorusCrispPreset(name, spliceSlider.Value, crispSlider.Value, offsetSlider.Value, curveSelector.SelectedIndex, true));
            RefreshPresetList();
            for (int i = 0; i < allPresets.Count; i++)
                if (allPresets[i].Name == name && allPresets[i].IsUserPreset) { presetCombo.SelectedIndex = i; break; }
        }
    }

    void DeletePreset_Click(object sender, EventArgs e)
    {
        if (presetCombo.SelectedIndex < 0) return;
        ChorusCrispPreset sel = allPresets[presetCombo.SelectedIndex];
        if (!sel.IsUserPreset) return;
        if (MessageBox.Show("Delete '" + sel.Name + "'?", "Delete", MessageBoxButtons.YesNo) == DialogResult.Yes)
        { ChorusCrispUserPresets.Delete(sel.Name); RefreshPresetList(); presetCombo.SelectedIndex = 0; }
    }

    void UpdateDeleteState()
    { deletePresetButton.Enabled = presetCombo.SelectedIndex >= 0 && allPresets[presetCombo.SelectedIndex].IsUserPreset; }

    // ── Slider ↔ Preview sync ──
    void Slider_Changed(object sender, EventArgs e)
    {
        SyncPreviewFromSliders();
        if (!isLoadingPreset) CheckForMatchingPreset();
    }

    void SyncPreviewFromSliders()
    {
        preview.SplicePercent = spliceSlider.Value;
        preview.CrispPercent = 100 - crispSlider.Value;
        preview.OffsetPercent = 100 - offsetSlider.Value;
        preview.CurveIndex = curveSelector.SelectedIndex;
        preview.Invalidate();
    }

    void Preview_SpliceChanged(object sender, EventArgs e)
    { isLoadingPreset = true; spliceSlider.SetValueSilent(preview.SplicePercent); isLoadingPreset = false; CheckForMatchingPreset(); }
    void Preview_CrispChanged(object sender, EventArgs e)
    { isLoadingPreset = true; crispSlider.SetValueSilent(100 - preview.CrispPercent); isLoadingPreset = false; CheckForMatchingPreset(); }
    void Preview_OffsetChanged(object sender, EventArgs e)
    { isLoadingPreset = true; offsetSlider.SetValueSilent(100 - preview.OffsetPercent); isLoadingPreset = false; CheckForMatchingPreset(); }

    void CheckForMatchingPreset()
    {
        for (int i = 1; i < allPresets.Count; i++)
        {
            ChorusCrispPreset p = allPresets[i];
            if (p.IsSeparator) continue;
            if (spliceSlider.Value == p.SpliceValue && crispSlider.Value == p.CrispValue &&
                offsetSlider.Value == p.OffsetValue && curveSelector.SelectedIndex == p.CurveIndex)
            { isLoadingPreset = true; presetCombo.SelectedIndex = i; isLoadingPreset = false; UpdateDeleteState(); return; }
        }
        isLoadingPreset = true; presetCombo.SelectedIndex = 0; isLoadingPreset = false; UpdateDeleteState();
    }

    // ── Settings ──
    void LoadSettings()
    {
        isLoadingPreset = true;
        ChorusCrispSettings s = ChorusCrispSettings.Load();
        spliceSlider.Value = Math.Max(0, Math.Min(100, s.SpliceValue));
        crispSlider.Value = Math.Max(0, Math.Min(100, s.CrispValue));
        offsetSlider.Value = Math.Max(0, Math.Min(100, s.OffsetValue));
        curveSelector.SelectedIndex = Math.Max(0, Math.Min(4, s.CurveIndex));
        curveSelector.Invalidate();
        SyncPreviewFromSliders();
        isLoadingPreset = false;
        CheckForMatchingPreset();
    }

    void SaveSettings()
    {
        ChorusCrispSettings s = new ChorusCrispSettings();
        s.SpliceValue = spliceSlider.Value; s.CrispValue = crispSlider.Value;
        s.OffsetValue = offsetSlider.Value; s.CurveIndex = curveSelector.SelectedIndex;
        s.Save();
    }

    // ── Title bar drag ──
    void TB_Down(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { dragging = true; dragOffset = e.Location; } }
    void TB_Move(object s, MouseEventArgs e) { if (dragging) { Point p = ((Control)s).PointToScreen(e.Location); Location = new Point(p.X - dragOffset.X, p.Y - dragOffset.Y); } }
    void TB_Up(object s, MouseEventArgs e) { dragging = false; }

    Label MakeDragLabel(string text, Color fg, Font f, int x, int y, Panel parent)
    {
        Label lbl = new Label(); lbl.Text = text; lbl.ForeColor = fg; lbl.Font = f;
        lbl.AutoSize = true; lbl.Location = new Point(x, y); lbl.BackColor = Color.Transparent;
        lbl.MouseDown += TB_Down; lbl.MouseMove += TB_Move; lbl.MouseUp += TB_Up;
        parent.Controls.Add(lbl); return lbl;
    }

    protected override void OnPaint(PaintEventArgs e)
    { base.OnPaint(e); using (Pen p = new Pen(Theme.Border)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1); }

    int AddSectionLabel(Panel p, string text, int y)
    {
        Label lbl = new Label(); lbl.Text = text; lbl.ForeColor = Theme.TextSecond;
        lbl.Font = new Font("Segoe UI", 8f, FontStyle.Bold); lbl.AutoSize = true;
        lbl.Location = new Point(24, y); lbl.BackColor = Color.Transparent;
        p.Controls.Add(lbl); return y + 22;
    }

    CardPanel CreateCard(Panel p, ref int y, int h)
    { CardPanel c = new CardPanel(); c.Location = new Point(20, y); c.Size = new Size(460, h); p.Controls.Add(c); y += h + 8; return c; }

    void AddSep(Panel p, int y)
    { Panel s = new Panel(); s.Location = new Point(14, y); s.Size = new Size(p.Width - 28, 1); s.BackColor = Theme.Border; p.Controls.Add(s); }
}
