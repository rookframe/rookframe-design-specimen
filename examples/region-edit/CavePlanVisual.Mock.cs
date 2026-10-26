using Godot;
namespace Rookframe.Manager.App;
public partial class CavePlanVisual
{
    internal Node3D MockArrow(Vector3 axis, bool doubleEnded)
    {
        var node = new Node3D();
        BuildPointGrip(node, doubleEnded ? 1 : 20);
        node.Quaternion = new Quaternion(Vector3.Up, axis);
        return node;
    }
}
