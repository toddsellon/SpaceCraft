using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using VRage.Game;
using VRageMath;
namespace SpaceCraft.Utils {

  public class Prefab {
    public string SubtypeId = String.Empty;
    public string Faction = String.Empty;
    public int Count = 0; // Block count
    public int Price = 0; // Component count
    public Dictionary<string,int> Components = new Dictionary<string,int>();
    public Dictionary<string,int> Cost = new Dictionary<string,int>(); // Ingot cost
    public bool IsStatic = false;
    public int FactoryTier = 0;
    public int RefineryTier = 0;
    public bool IsCargo = false;
    public bool IsRespawn = false;
    public bool Wheels = false;
    public bool Flying = false;
    public bool Spacecraft = false;
    public bool Fighter = false;
    public bool Worker = false;
    public bool Atmosphere = false;
    public Tech Teir = Tech.Primitive;

    public MyPrefabDefinition Definition;
    public MyPositionAndOrientation? PositionAndOrientation;

    public static List<Prefab> Prefabs = new List<Prefab>();

    public void Init() {
      if( SubtypeId == String.Empty && Definition == null ) return;

      if( Definition == null )
        Definition = MyDefinitionManager.Static.GetPrefabDefinition(SubtypeId);

      if( Definition == null ) return;

      Prefabs.Add( this );

      if( Definition.CubeGrids == null ) return;

      foreach( MyObjectBuilder_CubeGrid grid in Definition.CubeGrids ) {
        if( grid == null ) continue;
        if( grid.IsStatic ) IsStatic = true;
        PositionAndOrientation = grid.PositionAndOrientation;
        Count += grid.CubeBlocks.Count;

        foreach( MyObjectBuilder_CubeBlock block in grid.CubeBlocks ) {
          MyCubeBlockDefinition def = MyDefinitionManager.Static.GetCubeBlockDefinition(block);

          if( block is MyObjectBuilder_CargoContainer ) IsCargo = true;
          if( block is MyObjectBuilder_MedicalRoom || block is MyObjectBuilder_SurvivalKit ) {
            FactoryTier = Math.Max(FactoryTier,1);
            RefineryTier = Math.Max(RefineryTier,1);
            IsRespawn = true;
          }
          if( block is MyObjectBuilder_Assembler ) {
            FactoryTier = block.SubtypeName == "LargeAssembler" ? 3 : Math.Max(FactoryTier,2);
          }
          if( block is MyObjectBuilder_Refinery ) {
            RefineryTier = block.SubtypeName == "LargeRefinery" ? 3 : Math.Max(RefineryTier,2);
          }
          if( block is MyObjectBuilder_MotorSuspension ) Wheels = true;
          if( block is MyObjectBuilder_Drill ) Worker = true;
          if( block is MyObjectBuilder_WindTurbine ) Atmosphere = true;
          if( block is MyObjectBuilder_Reactor && !Cost.ContainsKey("Uranium") ) {
            Cost.Add("Uranium",1);
          }
          if( block is 	MyObjectBuilder_LargeGatlingTurret || block is 	MyObjectBuilder_LargeMissileTurret || block is 	MyObjectBuilder_SmallGatlingGun || block is 	MyObjectBuilder_SmallMissileLauncher )
            Fighter = true;
          if( block is MyObjectBuilder_Thrust ) {
            if( def != null )
              switch( def.Id.SubtypeName ) {
                case "LargeBlockLargeAtmosphericThrust":
                case "LargeBlockSmallAtmosphericThrust":
                case "SmallBlockLargeAtmosphericThrust":
                case "SmallBlockSmallAtmosphericThrust":
                  Flying = true;
                  break;
                case "SmallBlockSmallThrust":
                case "SmallBlockLargeThrust":
                case "LargeBlockSmallThrust":
                case "LargeBlockLargeThrust":
                  Spacecraft = true;
                  break;
                default:
                  Flying = true;
                  Spacecraft = true;
                  break;
              }
          }

          if( def != null ) {
            foreach( var component in def.Components ) {
              Price += component.Count;
              string subtype = component.Definition.Id.SubtypeName;
              MyBlueprintDefinitionBase blueprint = null;
      				MyDefinitionManager.Static.TryGetComponentBlueprintDefinition(component.Definition.Id, out blueprint);
              if( Components.ContainsKey(subtype) ) {
                Components[subtype] += component.Count;
              } else {
                Components.Add(subtype,component.Count);
              }
              if( blueprint != null) {
                foreach( var item in blueprint.Prerequisites ) {
                  subtype = item.Id.SubtypeName;
                  if( Cost.ContainsKey(subtype) ) {
                    Cost[subtype] += item.Amount.ToIntSafe();
                  } else {
                    Cost.Add(subtype,item.Amount.ToIntSafe());
                  }
                }
              }
            }

          }

        }


      }

      if( Worker ) Fighter = false;
      if( !IsStatic && Flying && !Spacecraft ) Atmosphere = true;
    }

    public static Prefab Get( string subtypeId ) {
      foreach( Prefab p in Prefabs ) if( p.SubtypeId == subtypeId ) return p;
      return null;
    }

    public static Prefab Add( string subtypeId, string faction ) {
      Prefab prefab = Prefab.Get(subtypeId);
      if( prefab == null ) {
        prefab = new Prefab{
          SubtypeId = subtypeId,
          Faction = faction
        };

        prefab.Init();
      }
      return prefab;
    }

    public override string ToString() {
      return base.ToString() + " " + SubtypeId;
    }
  }

}
