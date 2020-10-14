using VRageMath;
using VRage;
using VRage.Game.Entity;

namespace SpaceCraft.Utils {

  public enum Orders : ushort {
    Move,
    Weld,
    Grind,
    Shoot,
    Drill,
    Patrol,
    Refuel,
    Unload,
    Scout
  };

  public class Order {
    public Orders Type;
    public Vector3D Destination;
    public MyEntity Target;

    public override string ToString() {
      string type = "";
      switch( Type ) {
        case Orders.Move:
          type = "Move";
          break;
        case Orders.Weld:
          type = "Weld";
          break;
        case Orders.Grind:
          type = "Grind";
          break;
        case Orders.Shoot:
          type = "Shoot";
          break;
        case Orders.Drill:
          type = "Drill";
          break;
        case Orders.Patrol:
          type = "Patrol";
          break;
        case Orders.Refuel:
          type = "Refuel";
          break;
        case Orders.Unload:
          type = "Unload";
          break;
        case Orders.Scout:
          type = "Scout";
          break;
      }
      return base.ToString() + "["+type+"] " + ( Target == null ? Destination.ToString() : Target.WorldMatrix.GetDirectionVector(Base6Directions.Direction.Forward).ToString() );
    }
  }

}
