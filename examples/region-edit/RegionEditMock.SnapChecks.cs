using Godot;
namespace Rookframe.Manager.App;

// Focused experiment checks, deliberately outside the production test suite.
public partial class RegionEditMock
{
    private async void CheckSnap()
    {
        await ToSignal(GetTree().CreateTimer(.5),Godot.Timer.SignalName.Timeout);
        var cases=new List<object>();var failures=0;
        void check(string name,bool pass){cases.Add(new{name,pass});if(!pass)failures++;}
        float units(Vector2 direction,float pixels)=>pixels/Project(V(direction,0)).DistanceTo(Project(Vector3.Zero));
        var baseline=Copy();var four=units(Vector2.Right,4);var ten=units(Vector2.Right,10);var twenty=units(Vector2.Right,20);
        _snap=null;var snap=SnapMove(baseline,[1],new(four,0));check("close wall joins without overlap",snap.Length()<.001f&&_snap is not null&&!Collision(baseline[1] with{At=baseline[1].At+snap},baseline[0]));
        snap=SnapMove(baseline,[1],new(four,.5f));check("slide freely along wall",snap.DistanceTo(new(0,.5f))<.001f);
        snap=SnapMove(baseline,[1],new(ten,0));check("small pointer drift keeps contact",snap.Length()<.001f&&_snap is not null);
        snap=SnapMove(baseline,[1],new(twenty,0));check("pull away releases",snap.DistanceTo(new(twenty,0))<.001f&&_snap is null);
        snap=SnapMove(baseline,[1],new(ten,0));check("outside capture range stays free",snap.DistanceTo(new(ten,0))<.001f&&_snap is null);
        snap=SnapMove(baseline,[1],new(-four,0));check("small accidental overlap resolves to contact",snap.Length()<.001f&&_snap is not null);
        _snap=null;var angle=.43f;var rotated=baseline.Select(r=>r with{At=r.At.Rotated(angle),Angle=angle}).ToList();var direction=Vector2.Right.Rotated(angle);snap=SnapMove(rotated,[1],direction*units(direction,4));check("rotated parallel walls join",snap.Length()<.001f&&_snap is not null);
        _snap=null;var separate=baseline.Select(r=>r).ToList();separate[1]=separate[1] with{Floor=6};snap=SnapMove(separate,[1],new(four,0));check("different vertical levels do not attract",_snap is null);
        _snap=null;separate=baseline.Select(r=>r).ToList();separate[1]=separate[1] with{At=new(2,5)};SnapMove(separate,[1],new(four,0));check("corner-only contact does not join",_snap is null);
        _snap=null;var blocked=baseline.Select(r=>r).ToList();blocked.Add(new("Blocker",new(4.2f,0),Rectangle(1,1)));SnapMove(blocked,[1],new(four,0));check("reject contact that overlaps another room",_snap is null);
        _snap=null;var group=baseline.Select(r=>r).ToList();group.Add(new("Second moving room",new(2,4),Rectangle(4,4)));snap=SnapMove(group,[1,2],new(four,.2f));check("one offset keeps whole Building together",snap.DistanceTo(new(0,.2f))<.001f&&_snap is not null);
        var json=System.Text.Json.JsonSerializer.Serialize(new{failures,cases});GD.Print("REGION_SNAP_CHECK "+json);
        var path=OS.GetEnvironment("REGION_EVIDENCE");if(path.Length==0)path="/tmp/rfg401-region-edit";System.IO.Directory.CreateDirectory(path);System.IO.File.WriteAllText(path+"/snap-check.json",json);
        GetTree().Quit(failures==0?0:1);
    }
}
