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
    public static MyObjectBuilderType Magazine;
    public static MyDefinitionId Hydrogen;
    public static MyDefinitionId AnyOre;
    public static MyDefinitionId Stone;
    public static MyDefinitionId Ice;
    //public static MyDefinitionId Magazine;


    public static MyBlueprintDefinitionBase StoneBP;
    //public static MyBlueprintDefinitionBase MagazineBP;

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
      Magazine = MyObjectBuilderType.Parse("MyObjectBuilder_AmmoMagazine");
      Stone = MyDefinitionId.Parse("MyObjectBuilder_Ore/Stone");
      Ice = MyDefinitionId.Parse("MyObjectBuilder_Ore/Ice");

      AnyOre = new MyDefinitionId(Ore);
      Turret = MyObjectBuilderType.Parse("MyObjectBuilder_LargeGatlingTurret");
      Hydrogen = MyDefinitionId.Parse("MyObjectBuilder_GasProperties/Hydrogen");
      //Magazine = MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/NATO_25x184mmMagazine");
      StoneBP =	MyDefinitionManager.Static.GetBlueprintDefinition( MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/StoneOreToIngot") );
      //MagazineBP =	MyDefinitionManager.Static.GetBlueprintDefinition( Magazine );
    }
  }

}
