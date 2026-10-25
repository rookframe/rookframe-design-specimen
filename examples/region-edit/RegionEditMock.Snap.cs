using Godot;
namespace Rookframe.Manager.App;

// Disposable wall-contact experiment. Distances are pixels, so the pull feels
// equally light at each reviewed window size. No grid or automatic rotation.
public partial class RegionEditMock
{
    private const float SnapCapturePixels=8,SnapReleasePixels=14;
    private sealed record WallSnap(int Moving,int MovingEdge,int Fixed,int FixedEdge,Vector2 Offset,Vector2 A,Vector2 B,float Floor,float Pixels);
    private WallSnap? _snap;

    private Vector2 SnapMove(List<Room> start,int[] moving,Vector2 delta)
    {
        var candidates=new List<WallSnap>();
        foreach(var i in moving)
        {
            var room=start[i];var ap=Polygon(room).Select(p=>p+delta).ToArray();
            for(int j=0;j<start.Count;j++)
            {
                if(moving.Contains(j))continue;
                var target=start[j];
                if(Math.Min(room.Floor+room.Height,target.Floor+target.Height)-Math.Max(room.Floor,target.Floor)<=.03f)continue;
                var bp=Polygon(target);var floor=Math.Max(room.Floor,target.Floor)+.04f;
                for(int a=0;a<ap.Length;a++)for(int b=0;b<bp.Length;b++)
                {
                    var edge=ap[(a+1)%ap.Length]-ap[a];var other=bp[(b+1)%bp.Length]-bp[b];
                    if(edge.Length()<.1f||other.Length()<.1f)continue;
                    var tangent=edge.Normalized();
                    if(Math.Abs(tangent.Cross(other.Normalized()))>.0001f)continue;
                    var normal=new Vector2(-tangent.Y,tangent.X);
                    var offset=normal*(bp[b]-ap[a]).Dot(normal);
                    var pixels=Project(V(ap[a]+offset,floor)).DistanceTo(Project(V(ap[a],floor)));
                    var latched=_snap is{} old&&old.Moving==i&&old.MovingEdge==a&&old.Fixed==j&&old.FixedEdge==b;
                    if(pixels>(latched?SnapReleasePixels:SnapCapturePixels))continue;
                    var origin=ap[a]+offset;var p=(bp[b]-origin).Dot(tangent);var q=(bp[(b+1)%bp.Length]-origin).Dot(tangent);
                    var lo=Math.Max(0,Math.Min(p,q));var hi=Math.Min(edge.Length(),Math.Max(p,q));
                    if(hi-lo<.1f)continue;
                    // One offset moves the entire selected Building. Another room
                    // must never be pushed into an overlap to obtain this contact.
                    if(moving.Any(m=>Enumerable.Range(0,start.Count).Any(n=>!moving.Contains(n)&&Collision(start[m] with{At=start[m].At+delta+offset},start[n]))))continue;
                    candidates.Add(new(i,a,j,b,offset,origin+tangent*lo,origin+tangent*hi,floor,pixels));
                }
            }
        }
        var held=_snap;
        _snap=candidates.Where(c=>held is not null&&c.Moving==held.Moving&&c.MovingEdge==held.MovingEdge&&c.Fixed==held.Fixed&&c.FixedEdge==held.FixedEdge).FirstOrDefault()
            ??candidates.OrderBy(c=>c.Pixels).FirstOrDefault();
        return delta+(_snap?.Offset??Vector2.Zero);
    }
}
