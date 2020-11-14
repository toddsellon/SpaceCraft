using System;
using VRageMath;
using VRage;
using VRage.Game.Entity;
using VRage.Game.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using SpaceCraft.Utils;

namespace SpaceCraft.Utils {


  public class OBTypes {
    public static MyObjectBuilderType Tool;
    public static MyObjectBuilderType Blueprint;
    public static MyObjectBuilderType Component;
    public static MyObjectBuilderType Ingot;
    public static MyObjectBuilderType Ore;
    public static MyObjectBuilderType Turret;
    public static MyDefinitionId Hydrogen;
    public static MyDefinitionId AnyOre;
    public static MyDefinitionId Stone;

    public static MyBlueprintDefinitionBase StoneBP;
    public static MyBlueprintDefinitionBase Magazine;

    // public static MyObjectBuilder_GasProperties Hydrogen = new MyObjectBuilder_GasProperties(){
    //   SubtypeName = "Hydrogen"
    // };

    private static bool Initialized = false;
    public static void Init() {
      if( Initialized ) return;
      Tool = MyObjectBuilderType.Parse("MyObjectBuilder_PhysicalGunObject");
      Blueprint = MyObjectBuilderType.Parse("MyObjectBuilder_BlueprintDefinition");
      Component = MyObjectBuilderType.Parse("MyObjectBuilder_Component");
      Ingot = MyObjectBuilderType.Parse("MyObjectBuilder_Ingot");
      Ore = MyObjectBuilderType.Parse("MyObjectBuilder_Ore");
      Stone = MyDefinitionId.Parse("MyObjectBuilder_Ore/Stone");
      AnyOre = new MyDefinitionId(Ore);
      Turret = MyObjectBuilderType.Parse("MyObjectBuilder_LargeGatlingTurret");
      Hydrogen = MyDefinitionId.Parse("MyObjectBuilder_GasProperties/Hydrogen");

      StoneBP =	MyDefinitionManager.Static.GetBlueprintDefinition( MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/StoneOreToIngot") );
      Magazine =	MyDefinitionManager.Static.GetBlueprintDefinition( MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/NATO_25x184mmMagazine") );
    }
  }

}
