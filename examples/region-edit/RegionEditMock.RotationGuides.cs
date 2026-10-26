using Godot;
namespace Rookframe.Manager.App;

// Disposable native drawing over the projected floor plane, like RegionBrushOverlay.
public partial class RegionEditMock
{
    private bool _rotationGuide,_rotationHovered;
    private float _rotationGuideStart;
    private bool RotationGuideVisible=>_builder&&_tool==""&&_rooms.Count>0&&!_looks.Visible&&(_rotationHovered||_rotationGuide&&Pending);

    private void ClearRotationGuides(){_rotationGuide=false;_rotationHovered=false;}

    internal void DrawRotationGuides(Control canvas)
    {
        if(!RotationGuideVisible)return;
        var room=Current;var radius=room.Ring.Max(p=>p.Length())+.65f;var floor=room.Floor+.25f;
        var center=Project(V(room.At,floor));
        Vector2 point(float angle,float distance)=>Project(V(room.At+Vector2.FromAngle(angle)*distance,floor));
        var faint=new Color("e7e7dd",.3f);var ink=new Color("e7e7dd");var gold=new Color("d5a46b");
        var ring=Enumerable.Range(0,97).Select(i=>point(i*Mathf.Tau/96,radius)).ToArray();
        canvas.DrawPolyline(ring,faint,1.5f,true);
        for(int i=0;i<8;i++)
        {
            var angle=i*RotationStep;
            canvas.DrawDashedLine(center,point(angle,radius),faint,1,5,true);
            canvas.DrawLine(point(angle,radius*.95f),point(angle,radius*1.06f),ink,1.5f,true);
        }
        if(_rotationGuide)
            canvas.DrawDashedLine(center,point(_rotationGuideStart,radius),new Color("e7e7dd",.65f),1.5f,5,true);
        var snapped=Mathf.Abs(Mathf.AngleDifference(room.Angle,Mathf.Snapped(room.Angle,RotationStep)))<.0001f;
        var color=snapped?gold:ink;
        var tip=point(room.Angle,radius*1.06f);
        canvas.DrawLine(center,tip,new Color(0,0,0,.6f),5,true);
        canvas.DrawLine(center,tip,color,2.5f,true);
        canvas.DrawCircle(center,3,color);
        if(snapped)canvas.DrawArc(point(room.Angle,radius),6,0,Mathf.Tau,24,color,2,true);
        var degrees=Mathf.Wrap(Mathf.RadToDeg(room.Angle),0,360);
        if(degrees>359.95f)degrees=0;
        var text=degrees.ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)+"°";
        var font=_caption.GetThemeFont("font");const int size=18;
        var width=font.GetStringSize(text,HorizontalAlignment.Left,-1,size).X;
        var outward=(tip-center).Normalized();
        var label=tip+outward*22+new Vector2(-width/2,6);
        // If the HUD leaves no space below the rim, move the readout alongside
        // the tip instead of squeezing it onto the handle and snap marker.
        if(label.Y>_editHud.Position.Y-12)
            label=tip+new Vector2(outward.X>=0?18:-width-18,6);
        label.X=Mathf.Clamp(label.X,_left.Position.X+_left.Size.X+8,_rail.Position.X-width-8);
        label.Y=Mathf.Clamp(label.Y,42,_editHud.Position.Y-12);
        canvas.DrawStringOutline(font,label,text,HorizontalAlignment.Left,-1,size,5,new Color(0,0,0,.85f));
        canvas.DrawString(font,label,text,HorizontalAlignment.Left,-1,size,color);
    }
}
