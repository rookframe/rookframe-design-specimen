using Godot;
using Rookframe.Manager.Core;
using D = Godot.Collections.Dictionary;
using A = Godot.Collections.Array;
namespace Rookframe.Manager.App;

// Throwaway in-memory content adapter. No World, files, Participants or RPCs are created.
internal sealed class MockData
{
    internal const string Personal = "11111111-1111-4111-8111-111111111111";
    internal static MockData Current = null!;
    internal A Entries = [];
    internal LocalAppearancePalette Preferences = new();
    internal readonly Dictionary<string, RegionLook> Looks = [];
    internal RegionLook? Copied;
    internal string CopiedSubject = "region";
    internal string? CopiedAtomic;
    internal event Action? Changed;
    internal MockData()
    {
        Current = this;
        Entries = WorldApplicationRoot.ContentLibraryEntries(RookframeApplicationContent.CreateContent()
            .Where(e => e.Type is "wall_style" or "surface_finish" or "architectural_look").ToArray());
        foreach (var subject in new[] { "region", "building" })
        {
            var state = Preferences.For(subject, Entries.Select(v => v.AsGodotDictionary()));
            var tavern = Preferences.Palette.Add(subject);
            Preferences.Palette.Update(subject, tavern with { Name = "Timber", Icon = "wood-beam" });
            var stone = Preferences.Palette.Add(subject);
            Preferences.Palette.Update(subject, stone with { Name = "Stone halls", Icon = "brick-wall" });
            foreach (var e in AllLooks)
            {
                var category = Text(e, "localId") == "tavern-look" ? tavern.Id : stone.Id;
                Preferences.Palette.Include(subject, category, Ref(e), true);
            }
        }
        var original = BuiltIn("Tavern") with { ExteriorWall = Source("rough-stone-wall") };
        Save("The Copper Kettle", original with { Floor = Source("oak-planks") }, Categories[2].Id);
        Save("Lower crypt", BuiltIn("Dungeon"), Categories[3].Id);
        Save("Sunlit hall", BuiltIn("Castle") with { Floor = Source("oak-planks") }, Categories[3].Id);
        Preferences.Palette.Include("region", AppearancePalette.Pinned, Ref(AllLooks.First()), true);
    }
    internal static string Source(string id) => RookframeApplicationContent.SourceId + ":" + id;
    internal D? Content(string package, string local) => Entries.Select(v => v.AsGodotDictionary())
        .FirstOrDefault(e => Text(e, "packageId") == package && Text(e, "localId") == local);
    internal D Entry(string reference) => Entries.Select(v => v.AsGodotDictionary()).First(e => Ref(e) == reference);
    internal static string Ref(D e) => LocalAppearancePalette.Reference(e);
    internal static string Text(D e, string key) => LocalAppearancePalette.Text(e, key);
    internal IEnumerable<D> AllLooks => Entries.Select(v => v.AsGodotDictionary()).Where(e => Text(e, "type") == "architectural_look");
    internal AppearanceCategory[] Categories => Preferences.For("region", Entries.Select(v => v.AsGodotDictionary())).Categories.ToArray();
    internal string Display(D e) => Preferences.Palette.For("region", []).Names.GetValueOrDefault(Ref(e), Text(e, "displayName"));
    internal RegionLook BuiltIn(string name) => RegionLook.Load(Source(name.ToLowerInvariant() + "-look"), Content);
    internal RegionLook Look(D e) => RegionLook.Load(Ref(e), Content);
    internal string Save(string name, RegionLook look, string category, string? reference = null)
    {
        reference ??= Personal + ":look-" + Guid.NewGuid().ToString("N")[..8];
        Looks[reference] = look;
        var existing = Entries.Select(v => v.AsGodotDictionary()).FirstOrDefault(e => Ref(e) == reference);
        if (existing is null)
        {
            existing = new D { ["reference"] = reference, ["packageId"] = Personal, ["localId"] = reference.Split(':')[1],
                ["packageName"] = "My Looks", ["type"] = "architectural_look", ["available"] = true };
            Entries.Add(existing);
        }
        existing["displayName"] = name;
        Preferences.Palette.Name("region", reference, name);
        foreach (var subject in new[] { "region", "building" })
        {
            var state = Preferences.For(subject, Entries.Select(v => v.AsGodotDictionary()));
            if(state.Categories.Any(c=>c.Id=="all")) Preferences.Palette.Include(subject, "all", reference, true);
            var match = state.Categories.FirstOrDefault(c => c.Id == category || c.Name == Categories.FirstOrDefault(x => x.Id == category)?.Name);
            if (match is not null) Preferences.Palette.Include(subject, match.Id, reference, true);
        }
        Changed?.Invoke();
        return reference;
    }
    internal void Delete(string reference)
    {
        Looks.Remove(reference);
        var e = Entry(reference); Entries.Remove(e);
        foreach (var subject in new[] { "region", "building" })
            foreach (var c in Preferences.For(subject, Entries.Select(v => v.AsGodotDictionary())).Categories.ToArray())
                Preferences.Palette.Include(subject, c.Id, reference, false);
        Changed?.Invoke();
    }
    internal bool CopiedSaved => Copied is not null && AllLooks.Any(e => Look(e) == Copied);
    internal static bool TryLook(string reference, out RegionLook look)
    {
        look = null!;
        return Current is not null && Current.Looks.TryGetValue(reference, out look!);
    }
}

internal static class MockUi
{
    internal const string Ui = "res://rookframe/application/ui/";
    internal static Theme Controls => GD.Load<Theme>("res://rookframe/ui/theme/action_panel_theme.tres");
    internal static Texture2D Icon(string name) => GD.Load<Texture2D>("res://rookframe/ui/icons/" + name + ".svg");
    internal static Button Button(string icon, string name, Action action)
    {
        var b = new Button { Theme = Controls, ThemeTypeVariation = "RookframeActionIcon", Icon = Icon(icon),
            CustomMinimumSize = new(44,44), ExpandIcon = true, IconAlignment = HorizontalAlignment.Center,
            TooltipText = name, AccessibilityName = name, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        b.AddThemeConstantOverride("icon_max_width", 24); b.Pressed += action; return b;
    }
    internal static Label Label(string text, int size = 22)
    {
        var l = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        l.AddThemeFontSizeOverride("font_size", size); return l;
    }
    internal static void Clear(Node n) { foreach (var c in n.GetChildren()) { n.RemoveChild(c); c.QueueFree(); } }
    internal static RegionPaletteButton Disc(D entry, MockData data, int diameter = 64)
    {
        var disc = GD.Load<PackedScene>(Ui + "RegionPaletteButton.tscn").Instantiate<RegionPaletteButton>();
        disc.VisualDiameter = diameter;
        disc.CustomMinimumSize = Vector2.One * (diameter * RegionPaletteButton.ArtworkScale);
        disc.Size = disc.CustomMinimumSize;
        disc.TooltipText = disc.AccessibilityName = data.Display(entry);
        var swatch = GD.Load<PackedScene>(Ui + "RegionPaletteSwatch.tscn").Instantiate<RegionPaletteSwatch>();
        disc.AddChild(swatch);
        swatch.Scale = Vector2.One * (diameter / 120f);
        swatch.Position = (disc.Size - Vector2.One * diameter) / 2;
        disc.Ready += () => swatch.SetEntry(entry, data.Content);
        return disc;
    }
    internal static Control LookDisc(RegionLook look, MockData data, int diameter = 112)
    {
        var disc = GD.Load<PackedScene>(Ui + "RegionPaletteButton.tscn").Instantiate<RegionPaletteButton>();
        disc.VisualDiameter = diameter;
        disc.CustomMinimumSize = Vector2.One * (diameter * RegionPaletteButton.ArtworkScale);
        disc.Size = disc.CustomMinimumSize;
        disc.MouseFilter = Control.MouseFilterEnum.Ignore;
        var pairs = look.Encode().ToArray();
        for (var i = 0; i < pairs.Length; i++)
        {
            var s = GD.Load<PackedScene>(Ui + "RegionPaletteSwatch.tscn").Instantiate<RegionPaletteSwatch>();
            disc.AddChild(s); s.SetSector(i, pairs.Length); s.Scale = Vector2.One * (diameter / 120f);
            s.Position = (disc.Size - Vector2.One * diameter) / 2;
            var e = data.Entry(pairs[i].Value.AsString());
            disc.Ready += () => s.SetEntry(e, data.Content);
        }
        return disc;
    }
}
