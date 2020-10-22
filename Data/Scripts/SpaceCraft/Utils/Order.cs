using System;
using System.Collections.Generic;
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
    Scout,
    Attack,
    Deposit,
    Withdraw
  };

  public class Order {
    public Orders Type;
    public Vector3D Destination = Vector3D.Zero;
    public IMyEntity Target;
    public Controllable Entity;
    public Steps Step = Steps.Pending;
    public Dictionary<string,VRage.MyFixedPoint> Resources;
    public double Range = 10f;
    public int Tick = 0;
    public MyObjectBuilderType Filter = MyObjectBuilderType.Invalid;

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
