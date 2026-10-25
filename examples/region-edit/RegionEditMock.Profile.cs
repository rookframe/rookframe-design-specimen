using Godot;
using System.Diagnostics;
namespace Rookframe.Manager.App;

// Explicit local measurement of the same render/update path used during dragging.
// This never starts a World or injects operating-system input.
public partial class RegionEditMock
{
    private async void ProfileDrag()
    {
        await ToSignal(GetTree().CreateTimer(1),Godot.Timer.SignalName.Timeout);
        var mode=OS.GetCmdlineUserArgs().Contains("--profile-height")?"height":"move";
        var original=Current;var initial=Copy();var update=new List<double>();var frames=new List<double>();
        var clock=Stopwatch.StartNew();double previous=clock.Elapsed.TotalMilliseconds;
        for(int i=0;i<90;i++)
        {
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            var frame=clock.Elapsed.TotalMilliseconds;if(i>5)frames.Add(frame-previous);previous=frame;
            var start=clock.Elapsed.TotalMilliseconds;
            _rooms[_selected]=mode=="height"?original with{Height=original.Height+.8f*Mathf.Sin(i*.08f)}:original with{At=original.At+SnapMove(initial,new[]{_selected},new Vector2(.35f*Mathf.Sin(i*.08f),.2f*Mathf.Cos(i*.08f)))};
            Rebuild();
            if(i>5)update.Add(clock.Elapsed.TotalMilliseconds-start);
        }
        double percentile(List<double> values,double p){values.Sort();return values[(int)((values.Count-1)*p)];}
        var updateP95=percentile(update,.95);var frameP95=percentile(frames,.95);
        var result=new{mode,updateMedianMs=percentile(update,.5),updateP95Ms=updateP95,frameMedianMs=percentile(frames,.5),frameP95Ms=frameP95,pass=updateP95<8&&frameP95<34};
        var json=System.Text.Json.JsonSerializer.Serialize(result);
        GD.Print("REGION_DRAG_PROFILE "+json);
        var path=OS.GetEnvironment("REGION_EVIDENCE");if(path.Length==0)path="/tmp/rfg401-region-edit";System.IO.Directory.CreateDirectory(path);System.IO.File.WriteAllText(path+"/drag-profile.json",json);
        GetTree().Quit(result.pass?0:1);
    }
}
