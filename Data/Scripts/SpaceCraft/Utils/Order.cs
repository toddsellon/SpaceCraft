using System;
using VRageMath;
using VRage;
using VRage.Game.Entity;
using VRage.ModAPI;

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
    Scout,
    Attack
  };

  public class Order {
    public Orders Type;
    public Vector3D Destination;
    public IMyEntity Target;
    public Steps Step = Steps.Pending;
    public string SubtypeName = String.Empty;

    public void Progress() {
      if( Step < Steps.Completed )
        Step++;
    }

    public void Complete() {
      Step = Steps.Completed;
    }

    public override string ToString() {
      return base.ToString() + " " + Type;
    }
  }

}
