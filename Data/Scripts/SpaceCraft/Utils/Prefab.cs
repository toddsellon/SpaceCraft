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
    public bool IsStatic = false;
    public bool IsFactory = false;
    public bool IsRefinery = false;
    public bool IsCargo = false;
    public bool IsRespawn = false;
    public bool Wheels = false;
    public bool Flying = false;
    public bool Spacecraft = false;

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
          if( block is MyObjectBuilder_MedicalRoom || block is MyObjectBuilder_SurvivalKit ) IsRespawn = true;
          if( block is MyObjectBuilder_Assembler ) IsFactory = true;
          if( block is MyObjectBuilder_Refinery ) IsRefinery = true;
          if( block is MyObjectBuilder_MotorSuspension ) Wheels = true;
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
            }

          }

        }


      }
    }

    public static Prefab Get( string subtypeId ) {
      foreach( Prefab p in Prefabs ) if( p.SubtypeId == subtypeId ) return p;
      return null;
    }

    public static Prefab Add( string subtypeId, string faction ) {
      if( null == Prefab.Get(subtypeId) ) {
        Prefab prefab = new Prefab{
          SubtypeId = subtypeId,
          Faction = faction
        };

        prefab.Init();

        return prefab;
      }
      return null;
    }

    public override string ToString() {
      return base.ToString() + " " + SubtypeId;
    }
  }

}
