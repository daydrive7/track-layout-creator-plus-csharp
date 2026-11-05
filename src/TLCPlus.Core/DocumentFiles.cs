using System.Security.Cryptography;
using System.Text.Json;

namespace TLCPlus;

public static class DocumentFiles
{
    sealed class Metadata
    {
        public Metadata() { }
        public int Version { get; set; } = 1;
        public string TrkSha256 { get; set; } = "";
        public bool ManualHeight { get; set; }
        public double ManualHeightIn { get; set; } = 1;
        public double ManualHeightOut { get; set; } = 1;
        public bool ManualHeightEase { get; set; } = true;
        public double SmoothMix { get; set; } = 1;
        public List<Decoration>? Decorations { get; set; } = [];
        public List<RoadPlacement>? RoadPlacements { get; set; } = [];
    }

    public static void SaveLayout(string path, Project project)
    {
        if (!ValidRoadPlacements(project.RoadPlacements, project.Settings.Scenery))
            throw new InvalidDataException("Road placements have invalid types, scenery, positions or side flags.");
        byte[] bytes = TrkFormat.Write(project);
        File.WriteAllBytes(path, bytes);

        var settings = project.Settings;
        var metadata = new Metadata
        {
            TrkSha256 = Convert.ToHexString(SHA256.HashData(bytes)),
            ManualHeight = settings.ManualHeight,
            ManualHeightIn = settings.ManualHeightIn,
            ManualHeightOut = settings.ManualHeightOut,
            ManualHeightEase = settings.ManualHeightEase,
            SmoothMix = settings.SmoothMix,
            Decorations = project.Decorations,
            RoadPlacements = project.RoadPlacements
        };
        string extra = path + ".tlcplus.json";
        bool needsMetadata = project.Decorations.Count > 0 || project.RoadPlacements.Count > 0 || settings.ManualHeight ||
            settings.ManualHeightIn != 1 || settings.ManualHeightOut != 1 ||
            !settings.ManualHeightEase || settings.SmoothMix != 1;
        if (needsMetadata || File.Exists(extra))
            File.WriteAllText(extra, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static Project LoadLayout(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        var project = TrkFormat.Read(bytes);
        string extra = path + ".tlcplus.json";
        if (!File.Exists(extra)) return project;

        try
        {
            var metadata = JsonSerializer.Deserialize<Metadata>(File.ReadAllText(extra));
            // A companion belongs only to the exact TRK5 bytes it was saved with.
            if (metadata == null || metadata.Version != 1 ||
                metadata.TrkSha256 != Convert.ToHexString(SHA256.HashData(bytes)))
                return project;

            var roadPlacements = metadata.RoadPlacements ?? [];
            if (!ValidRoadPlacements(roadPlacements, project.Settings.Scenery)) return project;

            var settings = project.Settings;
            settings.ManualHeight = metadata.ManualHeight;
            settings.ManualHeightIn = metadata.ManualHeightIn;
            settings.ManualHeightOut = metadata.ManualHeightOut;
            settings.ManualHeightEase = metadata.ManualHeightEase;
            settings.SmoothMix = metadata.SmoothMix;
            project.Decorations = metadata.Decorations ?? [];
            project.RoadPlacements = roadPlacements;
        }
        catch (JsonException)
        {
            // The layout can still open if its optional companion is malformed.
        }
        return project;
    }
    static bool ValidRoadPlacements(List<RoadPlacement>? placements, int scenery)
    {
        if (placements == null) return false;
        foreach (var placement in placements)
        {
            if (placement == null || placement.Scenery != scenery ||
                !double.IsFinite(placement.Start) || placement.Start < 0 ||
                placement.Flag is not (0 or 1) || string.IsNullOrWhiteSpace(placement.TypeId))
                return false;
            try { _ = RoadTypeCatalog.Find(scenery, placement.TypeId); }
            catch (InvalidDataException) { return false; }
        }
        return true;
    }
}
