namespace Relic.Gameplay;

public static class ConsoleVariables
{
    // HDR
    public static bool R_HDR { get; set; } = true;
    public static float R_Exposure { get; set; } = 1.1f;
    // Bloom
    public static bool R_Bloom { get; set; } = true;
    public static float R_BloomIntensity { get; set; } = 1.0f;
    public static float R_BloomThreshold { get; set; } = 1.0f;
    // Tone mapping
    public static string R_ToneMap { get; set; } = "ACES"; // Reinhard or ACES

    public static void Set(string name, string value)
    {
        switch (name.ToLowerInvariant())
        {
            case "r_hdr": if (bool.TryParse(value, out var b)) R_HDR = b; else if (value == "1") R_HDR = true; else if (value == "0") R_HDR = false; break;
            case "r_exposure": if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f)) R_Exposure = f; break;
            case "r_bloom": if (bool.TryParse(value, out var bb)) R_Bloom = bb; else if (value == "1") R_Bloom = true; else if (value == "0") R_Bloom = false; break;
            case "r_bloom_intensity": if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fi)) R_BloomIntensity = fi; break;
            case "r_bloom_threshold": if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ft)) R_BloomThreshold = ft; break;
            case "r_tonemap": R_ToneMap = value; break;
        }
    }

    public static string Get(string name) => name.ToLowerInvariant() switch
    {
        "r_hdr" => R_HDR.ToString(),
        "r_exposure" => R_Exposure.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "r_bloom" => R_Bloom.ToString(),
        "r_bloom_intensity" => R_BloomIntensity.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "r_bloom_threshold" => R_BloomThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "r_tonemap" => R_ToneMap,
        _ => ""
    };
}
