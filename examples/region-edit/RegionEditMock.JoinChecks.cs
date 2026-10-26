using Godot;
namespace Rookframe.Manager.App;

// Replays the reported assembly through the mock's actual retained visuals.
// This diagnostic is outside the production routine test gate.
public partial class RegionEditMock
{
    private async void CheckJoins()
    {
        await ToSignal(GetTree().CreateTimer(.5),Godot.Timer.SignalName.Timeout);
        var cases=new List<object>();var failures=0;
        void check(string name,bool pass,int count){cases.Add(new{name,pass,count});if(!pass)failures++;}
        int timber(float low,float high)=>_roomVisuals.Values.Sum(v=>v.Shell.Decorations.Sum(d=>d.Modules.Values.Sum(m=>m.Vertices.Count(p=>
        {
            if(v.Shell.HeightPreview is {} h){if(m.Top)p.Y+=(float)(v.Plan.UpperOffset-h.Plan.UpperOffset);else if(p.Y>v.Plan.UpperOffset)return false;}
            var world=v.Root.Transform*p;
            return (v.State.Name=="Great hall"?world.X>.001f:world.X<-.001f)&&Math.Abs(world.X)<.25f&&Math.Abs(world.Z-.3937618f)<1.4f&&world.Y>low&&world.Y<high;
        }))));
        Reset("joins");var joined=Copy();
        var lower=timber(.25f,2.65f);check("joined divider has no duplicate exterior timber",lower==0,lower);
        var upper=timber(3.2f,4.8f);check("exposed taller wall retains exterior timber",upper>0,upper);
        _rooms[1]=Current with{At=new(4,.3937618f)};Rebuild();
        var restored=timber(.25f,2.65f);check("separating restores the hall exterior",restored>0,restored);
        Set(joined);var rejoined=timber(.25f,2.65f);check("rejoining refreshes both walls",rejoined==0,rejoined);
        Set(joined);_rooms[1]=Current with{Height=3.10f};Rebuild();
        var capOverlap=timber(2.801f,2.999f);check("height preview keeps timber out of the lower ceiling",capOverlap==0,capOverlap);
        Set(joined);
        foreach(var delta in new[]{.05f,.10f}){for(var i=0;i<_rooms.Count;i++)_rooms[i]=joined[i] with{Height=joined[i].Height+delta};Rebuild();}
        var stale=timber(.25f,3.099f);check("changing both heights updates the exterior cut",stale==0,stale);
        var json=System.Text.Json.JsonSerializer.Serialize(new{failures,cases});GD.Print("REGION_JOIN_CHECK "+json);
        var path=OS.GetEnvironment("REGION_EVIDENCE");if(path.Length==0)path="/tmp/rfg401-region-edit";System.IO.Directory.CreateDirectory(path);System.IO.File.WriteAllText(path+"/join-check.json",json);
        GetTree().Quit(failures==0?0:1);
    }
}
