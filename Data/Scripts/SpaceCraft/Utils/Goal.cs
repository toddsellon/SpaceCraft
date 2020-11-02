using VRageMath;
using VRage;
using VRage.Game.Entity;
using SpaceCraft.Utils;

namespace SpaceCraft.Utils {

  public enum Goals : ushort {
    Stabilize,
    Construct,
    Attack,
    Defend,
    Colonize
  };

  public enum Steps : ushort {
    Pending,
    Started,
    Commencing,
    Completed
  };

  public class Goal {

    public Goals Type;
    public IMyEntity Target;
    public Steps Step = Steps.Pending;
    public Prefab Prefab;
    public Controllable Entity;

    public void Progress() {
      if( Step < Steps.Completed )
        Step++;
    }

    public void Complete() {
      Step = Steps.Completed;
    }

    public override string ToString() {

      return base.ToString() + " " + Type.ToString() + " " + Target.ToString();
    }
  }

}
