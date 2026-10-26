using Godot;
namespace Rookframe.Manager.App;

// Disposable snapping experiment. Moving never rotates a room; angle snapping
// applies only while the rotation handle is being dragged.
public partial class RegionEditMock
{
    private const float SnapCapturePixels=8,SnapReleasePixels=14;
    private const float RotationStep=Mathf.Pi/4,RotationCapture=Mathf.Pi/45,RotationRelease=7*Mathf.Pi/180;
    private sealed record CornerSnap(int MovingVertex,int FixedVertex,Vector2 Point,float Pixels);
    private sealed record WallSnap(int Moving,int MovingEdge,int Fixed,int FixedEdge,Vector2 Offset,Vector2 A,Vector2 B,float Floor,float Pixels,CornerSnap? Corner=null);
    private WallSnap? _snap;
    private float? _rotationSnap;

    private float SnapRotation(float angle)
    {
        if(_rotationSnap is{} held&&Mathf.Abs(Mathf.AngleDifference(held,angle))<=RotationRelease)
            return angle+Mathf.AngleDifference(angle,held);
        var nearest=Mathf.Snapped(angle,RotationStep);
        _rotationSnap=Mathf.Abs(angle-nearest)<=RotationCapture?nearest:null;
        return _rotationSnap??angle;
    }

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
        if(_snap is{} wall)
        {
            _snap=SnapCorners(start,moving,delta,wall,held);
            if(_snap.Corner is null)
            {
                var ring=start[wall.Moving].Ring;var direction=(ring[(wall.MovingEdge+1)%ring.Length]-ring[wall.MovingEdge]).Rotated(start[wall.Moving].Angle).Normalized();
                // Another room in a selected Building may supply the corner on
                // this same sliding direction. Preserve the current wall contact.
                _snap=candidates.Where(c=>c!=wall&&c.Offset.DistanceTo(wall.Offset)<.001f).Where(c=>
                {
                    var points=start[c.Moving].Ring;
                    var along=(points[(c.MovingEdge+1)%points.Length]-points[c.MovingEdge]).Rotated(start[c.Moving].Angle).Normalized();
                    return Math.Abs(direction.Cross(along))<.0001f;
                }).Select(c=>SnapCorners(start,moving,delta,c,held)).Where(c=>c.Corner is not null).OrderBy(c=>c.Corner!.Pixels).FirstOrDefault()??_snap;
            }
        }
        return delta+(_snap?.Offset??Vector2.Zero);
    }

    private WallSnap SnapCorners(List<Room> start,int[] moving,Vector2 delta,WallSnap wall,WallSnap? held)
    {
        var ap=Polygon(start[wall.Moving]);var bp=Polygon(start[wall.Fixed]);
        var a=wall.MovingEdge;var nextA=(a+1)%ap.Length;var b=wall.FixedEdge;var nextB=(b+1)%bp.Length;
        var tangent=(ap[nextA]-ap[a]).Normalized();var length=ap[nextA].DistanceTo(ap[a]);
        var sameWall=held is not null&&held.Moving==wall.Moving&&held.MovingEdge==a&&held.Fixed==wall.Fixed&&held.FixedEdge==b;
        var aligned=new List<WallSnap>();
        foreach(var av in new[]{a,nextA})foreach(var bv in new[]{b,nextB})
        {
            var moved=ap[av]+delta+wall.Offset;
            var along=tangent*(bp[bv]-moved).Dot(tangent);
            var pixels=Project(V(moved+along,wall.Floor)).DistanceTo(Project(V(moved,wall.Floor)));
            var latched=sameWall&&held!.Corner is{} old&&old.MovingVertex==av&&old.FixedVertex==bv;
            if(pixels>(latched?SnapReleasePixels:SnapCapturePixels))continue;
            var offset=wall.Offset+along;var origin=ap[a]+delta+offset;
            var p=(bp[b]-origin).Dot(tangent);var q=(bp[nextB]-origin).Dot(tangent);
            var lo=Math.Max(0,Math.Min(p,q));var hi=Math.Min(length,Math.Max(p,q));
            // Alignment keeps a shared wall; touching at only one point is not a join.
            if(hi-lo<.1f)continue;
            if(moving.Any(m=>Enumerable.Range(0,start.Count).Any(n=>!moving.Contains(n)&&Collision(start[m] with{At=start[m].At+delta+offset},start[n]))))continue;
            aligned.Add(wall with{Offset=offset,A=origin+tangent*lo,B=origin+tangent*hi,Corner=new(av,bv,bp[bv],pixels)});
        }
        return aligned.FirstOrDefault(c=>sameWall&&held!.Corner is{} old&&c.Corner!.MovingVertex==old.MovingVertex&&c.Corner.FixedVertex==old.FixedVertex)
            ??aligned.OrderBy(c=>c.Corner!.Pixels).FirstOrDefault()??wall;
    }
}
