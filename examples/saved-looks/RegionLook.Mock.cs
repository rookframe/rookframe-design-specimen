namespace Rookframe.Manager.App;

// Prototype adapter for the proposed five-slot Look. Production source is untouched.
internal sealed partial record RegionLook
{
    internal static bool MockInteriorFace(int boundary, string face) =>
        boundary < 0 ? face == "front" : face == "back";

    private RoomDraft ApplyMockWallFaces(RoomDraft plan)
    {
        var outside = ExteriorWall ?? Wall; // Legacy supplied Looks retain both-face painting.
        var faces = plan.SurfaceAppearances;
        if (Wall is not null || outside is not null)
        {
            var choices = plan.CurrentWallAppearances();
            foreach (var wall in RoomPortal.Walls(plan))
                foreach (var face in new[] { "front", "back" })
                {
                    var reference = MockInteriorFace(wall.Boundary, face) ? Wall : outside;
                    if (reference is not null)
                        choices[RoomDraft.WallSurface(wall.Boundary, wall.Span, face)] = reference;
                }
            faces = choices;
        }
        return plan with
        {
            Wall = Wall ?? plan.Wall, Floor = Floor ?? plan.Floor,
            Ceiling = Ceiling ?? plan.Ceiling, Foundation = Foundation ?? plan.Foundation,
            SurfaceAppearances = faces
        };
    }
}
