using SpotifyAPI.Web;

namespace core.dj;

/// <summary>
/// Orders tracks for smooth DJ transitions by key (Camelot wheel) and tempo.
///
/// Needs Spotify audio features, which Spotify stopped serving to apps created after
/// November 2024. Check that your app still gets them before building a command on this.
/// </summary>
public static class DjMixHelper
{
    // Camelot wheel: maps Spotify's (key, mode) → Camelot position (1-12, A=minor B=major)
    // Spotify key: 0=C, 1=C#, 2=D, 3=D#, 4=E, 5=F, 6=F#, 7=G, 8=G#, 9=A, 10=A#, 11=B
    // mode: 0=minor, 1=major
    private static readonly Dictionary<(int key, int mode), (int number, char letter)> CamelotMap = new()
    {
        { (0,  1), (8,  'B') }, // C major
        { (1,  1), (3,  'B') }, // C# major
        { (2,  1), (10, 'B') }, // D major
        { (3,  1), (5,  'B') }, // Eb major
        { (4,  1), (12, 'B') }, // E major
        { (5,  1), (7,  'B') }, // F major
        { (6,  1), (2,  'B') }, // F# major
        { (7,  1), (9,  'B') }, // G major
        { (8,  1), (4,  'B') }, // Ab major
        { (9,  1), (11, 'B') }, // A major
        { (10, 1), (6,  'B') }, // Bb major
        { (11, 1), (1,  'B') }, // B major
        { (0,  0), (5,  'A') }, // C minor
        { (1,  0), (12, 'A') }, // C# minor
        { (2,  0), (7,  'A') }, // D minor
        { (3,  0), (2,  'A') }, // Eb minor
        { (4,  0), (9,  'A') }, // E minor
        { (5,  0), (4,  'A') }, // F minor
        { (6,  0), (11, 'A') }, // F# minor
        { (7,  0), (6,  'A') }, // G minor
        { (8,  0), (1,  'A') }, // Ab minor
        { (9,  0), (8,  'A') }, // A minor
        { (10, 0), (3,  'A') }, // Bb minor
        { (11, 0), (10, 'A') }, // B minor
    };

    // Returns a 0.0–1.0 incompatibility score for key transitions (0 = perfect, 1 = worst)
    private static float KeyDistance(TrackAudioFeatures a, TrackAudioFeatures b)
    {
        if (!CamelotMap.TryGetValue((a.Key, a.Mode), out var ca) ||
            !CamelotMap.TryGetValue((b.Key, b.Mode), out var cb))
        {
            return 0.5f; // unknown key, neutral penalty
        }

        // Same position: perfect
        if (ca == cb) return 0f;

        // Parallel (same number, different letter): great for energy shifts
        if (ca.number == cb.number) return 0.1f;

        // Adjacent on the wheel (±1): smooth transition
        int diff = Math.Abs(ca.number - cb.number);
        int wrappedDiff = Math.Min(diff, 12 - diff);
        if (wrappedDiff == 1) return 0.2f;

        // Two steps away: acceptable
        if (wrappedDiff == 2) return 0.5f;

        // Anything else is a clash
        return 1.0f;
    }

    // Returns a 0.0–1.0 incompatibility score for tempo transitions
    private static float TempoDistance(TrackAudioFeatures a, TrackAudioFeatures b)
    {
        if (a.Tempo <= 0 || b.Tempo <= 0) return 0.5f;

        float ratio = Math.Max(a.Tempo, b.Tempo) / Math.Min(a.Tempo, b.Tempo);

        // Within 6%: very smooth
        if (ratio <= 1.06f) return 0f;

        // Double/half time mixing is common in DJ sets
        float doubleRatio = Math.Max(a.Tempo, b.Tempo) / (Math.Min(a.Tempo, b.Tempo) * 2f);
        if (Math.Abs(doubleRatio - 1f) <= 0.06f) return 0.15f;

        // Normalise: 30% difference = score of 1
        return Math.Min((ratio - 1f) / 0.30f, 1.0f);
    }

    private static float TransitionScore(TrackAudioFeatures a, TrackAudioFeatures b)
    {
        // Key compatibility weighted higher than tempo
        return KeyDistance(a, b) * 0.65f + TempoDistance(a, b) * 0.35f;
    }

    public static List<(FullTrack track, TrackAudioFeatures features)> OrderForDjMix(
        List<(FullTrack track, TrackAudioFeatures features)> items)
    {
        if (items.Count <= 1) return items;

        var remaining = new List<(FullTrack track, TrackAudioFeatures features)>(items);
        var ordered = new List<(FullTrack track, TrackAudioFeatures features)>(items.Count);

        // Start from the track closest to the median tempo to anchor the set. The median is taken
        // over known tempos only; with none known, the first track is as good a start as any.
        var knownTempos = items
            .Select(x => x.features.Tempo)
            .Where(t => t > 0)
            .Order()
            .ToList();
        float medianTempo = knownTempos.Count > 0 ? knownTempos[knownTempos.Count / 2] : 0f;

        var seed = remaining.MinBy(x => Math.Abs(x.features.Tempo - medianTempo));
        ordered.Add(seed);
        remaining.Remove(seed);

        while (remaining.Count > 0)
        {
            var last = ordered[^1].features;
            var next = remaining.MinBy(x => TransitionScore(last, x.features));
            ordered.Add(next);
            remaining.Remove(next);
        }

        return ordered;
    }
}
