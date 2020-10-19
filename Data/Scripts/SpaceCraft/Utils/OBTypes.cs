using System;
using VRageMath;
using VRage;
using VRage.Game.Entity;
using SpaceCraft.Utils;

namespace SpaceCraft.Utils {


  public class OBTypes {
    public static MyObjectBuilderType Tool;
    public static MyObjectBuilderType Blueprint;
    public static MyObjectBuilderType Component;
    public static MyObjectBuilderType Ingot;
    public static MyObjectBuilderType Ore;

    private static bool Initialized = false;
    public static void Init() {
      if( Initialized ) return;
      Tool = MyObjectBuilderType.Parse("MyObjectBuilder_PhysicalGunObject");
      Blueprint = MyObjectBuilderType.Parse("MyObjectBuilder_BlueprintDefinition");
      Component = MyObjectBuilderType.Parse("MyObjectBuilder_Component");
      Ingot = MyObjectBuilderType.Parse("MyObjectBuilder_Ingot");
      Ore = MyObjectBuilderType.Parse("MyObjectBuilder_Ore");
    }
  }

}
