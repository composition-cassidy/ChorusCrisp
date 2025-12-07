using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ScriptPortal.Vegas;
using ChorusCrisp;

public class EntryPoint
{
    public void FromVegas(Vegas vegas)
    {
        List<TrackEvent> selectedEvents = GetSelectedAudioEvents(vegas);

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

        double spliceTime = AudioProcessor.CalculateSpliceTime(splicePercent);
        double duckDb = AudioProcessor.CalculateDuckDb(crispPercent);

        ProcessSelectedEvents(selectedEvents, spliceTime, duckDb, offsetPercent, fadeType);
    }

    private List<TrackEvent> GetSelectedAudioEvents(Vegas vegas)
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

        return selectedEvents;
    }

    private void ProcessSelectedEvents(List<TrackEvent> selectedEvents, double spliceTime, 
        double duckDb, double offsetPercent, CurveType fadeType)
    {
        int successCount = 0;
        int errorCount = 0;
        string lastError = "";

        using (UndoBlock undo = new UndoBlock("Chorus Crisp"))
        {
            foreach (TrackEvent ev in selectedEvents)
            {
                try
                {
                    AudioProcessor.ProcessEvent(ev, spliceTime, duckDb, offsetPercent, fadeType);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    lastError = ex.Message;
                }
            }
        }

        ShowCompletionMessage(successCount, errorCount, lastError, spliceTime, duckDb, offsetPercent, fadeType);
    }

    private void ShowCompletionMessage(int successCount, int errorCount, string lastError, 
        double spliceTime, double duckDb, double offsetPercent, CurveType fadeType)
    {
        string message = String.Format("Processed {0} clip(s)!\n\nSplice at: {1:F3}s\nVolume duck: {2:F1} dB\nOffset: {3}%\nFade type: {4}",
            successCount, spliceTime, duckDb, (int)(offsetPercent * 100), fadeType);
        
        if (errorCount > 0)
        {
            message += String.Format("\n\n{0} clip(s) had errors:\n{1}", errorCount, lastError);
        }
        
        MessageBox.Show(message, "Chorus Crisp Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
