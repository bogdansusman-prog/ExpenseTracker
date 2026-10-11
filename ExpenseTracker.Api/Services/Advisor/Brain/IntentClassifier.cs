namespace ExpenseTracker.Api.Services.Advisor.Brain;

/// <summary>Prediction with how sure the classifier is (0..1) and its second guess.</summary>
public record IntentPrediction(Intent Intent, double Confidence, Intent Runner);

/// <summary>
/// Multinomial Naive Bayes, written from scratch. It learns from example questions
/// (<see cref="TrainingData"/>): every word stem and pair of stems is a feature, and a new
/// question gets the intent whose examples make its words most likely.
/// Unknown words are matched to the closest known word, so typos still count.
/// </summary>
public sealed class IntentClassifier
{
    private readonly Dictionary<Intent, Dictionary<string, int>> _featureCounts = new();
    private readonly Dictionary<Intent, int> _totalFeatures = new();
    private readonly Dictionary<Intent, int> _documents = new();
    private readonly HashSet<string> _vocabulary = new();
    private readonly List<string> _unigrams = new();
    private int _documentCount;

    public IntentClassifier(IEnumerable<(Intent Intent, string Text)> examples)
    {
        foreach (var (intent, text) in examples)
        {
            _documentCount++;
            _documents[intent] = _documents.GetValueOrDefault(intent) + 1;

            if (!_featureCounts.TryGetValue(intent, out var counts))
            {
                counts = new Dictionary<string, int>();
                _featureCounts[intent] = counts;
            }

            foreach (var feature in Features(TextTools.Tokens(text)))
            {
                counts[feature] = counts.GetValueOrDefault(feature) + 1;
                _totalFeatures[intent] = _totalFeatures.GetValueOrDefault(intent) + 1;

                if (_vocabulary.Add(feature) && !feature.Contains('_'))
                {
                    _unigrams.Add(feature);
                }
            }
        }
    }

    public IntentPrediction Predict(string text)
    {
        var stems = TextTools.Tokens(text).Select(TextTools.Stem).Select(Correct).ToList();
        var features = StemFeatures(stems).Where(_vocabulary.Contains).ToList();

        if (features.Count == 0 || _documentCount == 0)
        {
            return new IntentPrediction(Intent.Unknown, 0, Intent.Unknown);
        }

        // Σ log P(feature | intent), with add-one (Laplace) smoothing. The prior is uniform on purpose:
        // an intent with more examples should not win short questions just because it has more examples.
        var scores = new Dictionary<Intent, double>();
        foreach (var (intent, counts) in _featureCounts)
        {
            var score = 0.0;
            var denominator = _totalFeatures[intent] + _vocabulary.Count;

            foreach (var feature in features)
            {
                score += Math.Log((counts.GetValueOrDefault(feature) + 1.0) / denominator);
            }

            scores[intent] = score;
        }

        // Softmax turns log scores into probabilities that sum to 1.
        var max = scores.Values.Max();
        var exp = scores.ToDictionary(pair => pair.Key, pair => Math.Exp(pair.Value - max));
        var sum = exp.Values.Sum();
        var ranked = exp.OrderByDescending(pair => pair.Value).ToList();

        return new IntentPrediction(
            ranked[0].Key,
            ranked[0].Value / sum,
            ranked.Count > 1 ? ranked[1].Key : Intent.Unknown);
    }

    /// <summary>Word stems plus pairs of neighbouring stems ("luna_trecut"), which carry more meaning together.</summary>
    public static IEnumerable<string> Features(IReadOnlyList<string> tokens) =>
        StemFeatures(tokens.Select(TextTools.Stem).ToList());

    private static IEnumerable<string> StemFeatures(IReadOnlyList<string> stems)
    {
        foreach (var stem in stems)
        {
            yield return stem;
        }

        for (var i = 0; i + 1 < stems.Count; i++)
        {
            yield return stems[i] + "_" + stems[i + 1];
        }
    }

    /// <summary>A word the classifier never saw is replaced by the closest known one ("abonamnte" → "abonament").</summary>
    private string Correct(string stem)
    {
        if (_vocabulary.Contains(stem) || stem.Length < 4)
        {
            return stem;
        }

        var closest = _unigrams
            .Where(known => TextTools.Similar(known, stem))
            .OrderBy(known => TextTools.Distance(known, stem))
            .FirstOrDefault();

        return closest ?? stem;
    }
}
