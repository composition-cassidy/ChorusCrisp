using System;
using System.IO;

namespace ChorusCrisp
{
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
        
        public ChorusCrispPreset(string name, int splice, int crisp, int offset, int curve)
        {
            Name = name;
            SpliceValue = splice;
            CrispValue = crisp;
            OffsetValue = offset;
            CurveIndex = curve;
        }
        
        public override string ToString() { return Name; }
    }

    public static class PresetManager
    {
        public static ChorusCrispPreset[] GetDefaultPresets()
        {
            // Presets: Name, SpliceSlider, CrispSlider (inverted), OffsetSlider (inverted), CurveIndex
            // CurveIndex: 0=Linear, 1=Fast, 2=Slow, 3=Sharp, 4=Smooth
            
            // For CrispSlider: duckDb = (100 - sliderValue) / 100 * -15
            // -3 dB -> (100 - x)/100 * -15 = -3 -> x = 80
            // -5 dB -> (100 - x)/100 * -15 = -5 -> x = 67
            
            // For OffsetSlider: offset% = 100 - sliderValue
            // 80% -> sliderValue = 20
            // 90% -> sliderValue = 10
            // 95% -> sliderValue = 5
            
            return new ChorusCrispPreset[]
            {
                new ChorusCrispPreset("Custom", -1, -1, -1, -1),
                new ChorusCrispPreset("Jario Style", 50, 80, 20, 4),
                new ChorusCrispPreset("Standard", 40, 80, 10, 0),
                new ChorusCrispPreset("Snappy", 35, 67, 5, 1)
            };
        }
    }
}
