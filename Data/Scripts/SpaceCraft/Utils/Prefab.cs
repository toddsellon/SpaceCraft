using System;
using System.Linq;
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
    public bool Bot = false;
    public MyBotDefinition BotDefinition;
    public Tech Teir = Tech.Primitive;
    public Races Race = Races.Terran;
    public static readonly SerializableVector3 DefaultColor = new SerializableVector3(0.575f,0.150000036f,0.199999958f);

    public MyPrefabDefinition Definition;
    public MyPositionAndOrientation? PositionAndOrientation;

    public static List<Prefab> Prefabs = new List<Prefab>();

    public void ChangeColor( SerializableVector3 color ) {
      ChangeColor( color, DefaultColor );
    }

    public void ChangeColor( SerializableVector3 color, SerializableVector3 from ) {
      foreach( MyObjectBuilder_CubeGrid grid in Definition.CubeGrids ) {
        if( grid == null ) continue;
        foreach( MyObjectBuilder_CubeBlock block in grid.CubeBlocks ) {
          if( block.ColorMaskHSV == from )
						block.ColorMaskHSV = color;
        }
      }
    }

    public void Init() {
      if( SubtypeId == String.Empty && Definition == null && !Bot ) return;

      if( Definition == null )
        Definition = MyDefinitionManager.Static.GetPrefabDefinition(SubtypeId);

      if( Definition == null && !Bot ) return;

      Prefabs.Add( this );

      if( BotDefinition != null ) {

        MyAnimalBotDefinition animal = BotDefinition as MyAnimalBotDefinition;
        MyHumanoidBotDefinition human = BotDefinition as MyHumanoidBotDefinition;
        if( animal == null && human == null ) return;
        if( animal != null ) {
          switch( animal.DisplayNameString ) {
            default:
              Count = Price = 50;
              Components.Add("Organic",25);
              // Components.Add("ControlUnit",10);
              Cost.Add("Organic",75);
              Fighter = true;
              Race = Races.Zerg;
              break;
            // case "Mutalusk":
            //   Count = 1000;
            //   Components.Add("Organic",500);
            //   Components.Add("VentralSacks",5);
            //   Cost.Add("Organic",500);
            //   Cost.Add("Gold",10);
            //   Cost.Add("Silver",15);
            //
            //   Fighter = true;
            //   Flying = true;
            //   Spacecraft = true;
            //   break;
            case "Ultralusk":
              Count = Price = 10000;
              Components.Add("Organic",5000);
              Components.Add("ZergCarapace",150);
              Components.Add("MetabolicGlands",15);
              Cost.Add("Organic",5000);
              Cost.Add("Cobalt",500);
              Cost.Add("Uranium",15);
              Cost.Add("Platinum",15);
              Race = Races.Zerg;
              Fighter = true;
              break;
          }
        }
        return;
      }

      if( Definition.CubeGrids == null ) return;

      //List<MyObjectBuilder_CubeGrid> copiedPrefab = new List<MyObjectBuilder_CubeGrid>();

      int i = 0;
      foreach( MyObjectBuilder_CubeGrid grid in Definition.CubeGrids ) {
        //MyAPIGateway.Entities.RemapObjectBuilder(grid);
        if( grid == null ) continue;
        if( grid.IsStatic ) IsStatic = true;
        PositionAndOrientation = grid.PositionAndOrientation;
        Count += grid.CubeBlocks.Count;
        MyObjectBuilder_CubeGrid clone = grid.Clone() as MyObjectBuilder_CubeGrid;
        clone.EntityId = 0;



        // MyObjectBuilder_CubeGrid clone = grid.Clone() as MyObjectBuilder_CubeGrid;
        // clone.EntityId = 0;
        // copiedPrefab.Add(clone);
        //
        // foreach( MyObjectBuilder_CubeBlock block in clone.CubeBlocks ) {
        //   block.EntityId = 0;
        // }

        //foreach( MyObjectBuilder_CubeBlock block in grid.CubeBlocks.ToList() ) {
        foreach( MyObjectBuilder_CubeBlock block in clone.CubeBlocks.ToList() ) {


          block.ShareMode = MyOwnershipShareModeEnum.Faction;
          MyCubeBlockDefinition def = MyDefinitionManager.Static.GetCubeBlockDefinition(block);
          block.EntityId = 0;
          if( def != null ) { // Calculate cost of block
            foreach( var component in def.Components ) {
              Price += component.Count;
              string subtype = component.Definition.Id.SubtypeName;
              MyBlueprintDefinitionBase blueprint = null;
      				MyDefinitionManager.Static.TryGetComponentBlueprintDefinition(component.Definition.Id, out blueprint);
              if( blueprint == null )
                blueprint = SpaceCraftSession.GetBlueprintDefinition(component.Definition.Id.SubtypeName);

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

          if( block is MyObjectBuilder_CargoContainer ) {
            IsCargo = true;
            continue;
          }
          if( block is MyObjectBuilder_MedicalRoom || block is MyObjectBuilder_SurvivalKit ) {
            FactoryTier = Math.Max(FactoryTier,1);
            RefineryTier = Math.Max(RefineryTier,1);
            IsRespawn = true;
            continue;
          }
          if( block is MyObjectBuilder_Assembler ) {
            switch(block.SubtypeName) {
              case "LargeAssembler":
              case "LargeProtossAssembler":
              case "LargeZergAssembler":
                FactoryTier = 3;
                break;
              default:
                FactoryTier = Math.Max(FactoryTier,2);
                break;
            }
            //FactoryTier = (block.SubtypeName == "LargeAssembler" || block.SubtypeName == "LargeProtossAssembler") ? 3 : Math.Max(FactoryTier,2);
            continue;
          }
          if( block is MyObjectBuilder_Refinery ) {
            switch(block.SubtypeName) {
              case "LargeRefinery":
              case "LargeProtossRefinery":
              case "LargeZergRefinery":
                RefineryTier = 3;
                break;
              default:
                RefineryTier = Math.Max(RefineryTier,2);
                break;
            }
            // RefineryTier = (block.SubtypeName == "LargeRefinery" || block.SubtypeName == "LargeProtossRefinery") ? 3 : Math.Max(RefineryTier,2);
            continue;
          }
          if( block is MyObjectBuilder_MotorSuspension ) {
            Wheels = true;
            continue;
          }
          if( block is MyObjectBuilder_Drill ) {
            Worker = true;
            continue;
          }
          if( block is MyObjectBuilder_WindTurbine ) {
            Atmosphere = true;
            continue;
          }
          if( block is MyObjectBuilder_Reactor && !Cost.ContainsKey("Uranium") ) {
            Cost.Add("Uranium",1);
            continue;
          }
          if( block is MyObjectBuilder_UserControllableGun ) {
            Fighter = true;
            if( (block is MyObjectBuilder_LargeMissileTurret || block is MyObjectBuilder_SmallMissileLauncher) && !Cost.ContainsKey("Uranium") ) {
              Cost.Add("Uranium",1);
            }
            continue;
          }
          // if( block is MyObjectBuilder_LargeMissileTurret || block is MyObjectBuilder_SmallMissileLauncher ) {
          //   Fighter = true;
          //   if( !Cost.ContainsKey("Uranium") )
          //     Cost.Add("Uranium",1);
          //   continue;
          // }
          // if( block is 	MyObjectBuilder_LargeGatlingTurret || block is MyObjectBuilder_SmallGatlingGun ) {
          //   Fighter = true;
          //   continue;
          // }
          if( block is MyObjectBuilder_Thrust ) {
            if( def != null )
              switch( def.Id.SubtypeName ) {
                case "LargeBlockLargeAtmosphericThrust":
                case "LargeBlockSmallAtmosphericThrust":
                case "SmallBlockLargeAtmosphericThrust":
                case "SmallBlockSmallAtmosphericThrust":
                case "SmallBlockSmallAtmosphericThrustSciFi":
                case "SmallBlockLargeAtmosphericThrustSciFi":
                case "LargeBlockSmallAtmosphericThrustSciFi":
                case "LargeBlockLargeAtmosphericThrustSciFi":
                  Flying = true;
                  break;
                case "SmallBlockSmallThrust":
                case "SmallBlockLargeThrust":
                case "LargeBlockSmallThrust":
                case "LargeBlockLargeThrust":
                case "SmallBlockSmallThrustSciFi":
                case "SmallBlockLargeThrustSciFi":
                case "LargeBlockSmallThrustSciFi":
                case "LargeBlockLargeThrustSciFi":
                  Spacecraft = true;
                  break;
                default:
                  Flying = true;
                  Spacecraft = true;
                  break;
              }
          }

          MyObjectBuilder_CubeBlock replacement = Clone(block);
          clone.CubeBlocks.Remove(block);
          clone.CubeBlocks.Add(replacement);

        }

        Definition.CubeGrids[i] = clone;

        i++;
      }


      // Copy over original definition
      // for( int i = 0; i < Definition.CubeGrids.Length; i++ ) {
      //   Definition.CubeGrids[i] = copiedPrefab[i];
      // }



      if( Worker || IsStatic ) Fighter = false;
      if( !IsStatic && Flying && !Spacecraft ) Atmosphere = true;
    }

    public static MyObjectBuilder_CubeBlock Clone( MyObjectBuilder_CubeBlock block ) {
      MyObjectBuilder_CubeBlock replacement = MyObjectBuilderSerializer.CreateNewObject(block.TypeId, block.SubtypeName) as MyObjectBuilder_CubeBlock;
      replacement.BlockOrientation = block.BlockOrientation;
      replacement.Min = block.Min;
      replacement.ColorMaskHSV = block.ColorMaskHSV;
      replacement.Owner = block.Owner;
      return replacement;
    }

    // Returns the cost of the first battery, minumum required to spawn
    public Dictionary<string,int> GetBalance() {
      if( Definition == null ) return new Dictionary<string,int>(Components);

      MyObjectBuilder_BatteryBlock battery = GetBattery();
      if( battery == null ) return null;

      return Block.GetCost(battery);
    }

    public MyObjectBuilder_BatteryBlock GetBattery() {
      foreach( MyObjectBuilder_CubeGrid grid in Definition.CubeGrids ) {
        foreach(MyObjectBuilder_CubeBlock block in grid.CubeBlocks) {
          if( block is MyObjectBuilder_BatteryBlock )
            return block as MyObjectBuilder_BatteryBlock;
        }
      }
      return null;
    }

    public static Prefab Get( string subtypeId ) {
      foreach( Prefab p in Prefabs ) if( p.SubtypeId == subtypeId ) return p;
      return null;
    }

    public static Prefab Add( string subtypeId, Races race, string faction ) {
      Prefab prefab = Prefab.Get(subtypeId);
      if( prefab == null ) {
        prefab = new Prefab{
          SubtypeId = subtypeId,
          Faction = faction,
          Race = race
        };
        prefab.Init();
        // MyAPIGateway.Utilities.ShowMessage( "Prefab.Add", subtypeId + ": " + String.Join(",",prefab.Cost.Keys) );
        // MyAPIGateway.Utilities.ShowMessage( "Prefab.Add", subtypeId + ": " + (prefab. IsStatic ? "Static" : "Not static") );
      }
      return prefab;
    }

    public override string ToString() {
      return base.ToString() + " " + SubtypeId;
    }
  }

}
