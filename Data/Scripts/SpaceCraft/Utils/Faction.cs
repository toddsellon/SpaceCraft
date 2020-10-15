using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.Game;
using Sandbox.Definitions;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Engine.Voxels;
using System;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Voxels;
using SpaceCraft;
using SpaceCraft.Utils;
using VRageMath;
using VRage.Game.Entity;
using VRage.Game.ModAPI.Ingame.Utilities; // MyCommandLine


namespace SpaceCraft.Utils {

  public enum Progression {
    None,
    BasicAssembler,
    BasicRefinery,
    Assembler,
    Refinery,
    Reactor
  };

  public enum Priorties {
    None,
    Gather,
    Attack,
    Defend
  };

  //[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
  //public class Faction:MySessionComponentBase {
  public class Faction {
    public static int LIMIT = 20;

    public string Name;
    public MyCommandLine CommandLine = new MyCommandLine();
    public List<MySpawnGroupDefinition> Groups = new List<MySpawnGroupDefinition>();
    public bool Spawned = false;
    public SerializableVector3 Color;
    public Goal CurrentGoal;
    private IMyCubeBlock RespawnPoint;
    private Progression Progress = Progression.None;
    private string Roadblock = string.Empty;
    private List<Engineer> Engineers = new List<Engineer>();
    private List<Controllable> Controlled = new List<Controllable>();
    private List<IMyEntity> Enemies = new List<IMyEntity>();
    private CubeGrid MainBase;
    private List<string> Resources = new List<string>(){"Stone","Iron","Silicon","Nickel"};
    protected static Random Randy = new Random();
    protected int Tick = 0;
    protected MyPlanet Homeworld;
    private static bool First = true;

    // Main loop
    public void UpdateBeforeSimulation() {
      Tick++;
      //bool ten = Tick % 10 == 0;
      //bool needs = false;

      AssessGoal();

      foreach( Controllable c in Controlled ) {
        c.UpdateBeforeSimulation();
      }

      if( Tick == 99 ) Tick = 0;

      // if( Tick == 99 ) {
      //   Tick = 0;
      //
      //   foreach( Controllable c in Controlled ) {
      //     if( c is CubeGrid && (c as CubeGrid).Need != CubeGrid.Needs.None ) needs = true;
      //   }
      //
      //   if( !needs ) {
      //     AssessProgression();
      //   }
      // }
    }

    public void AssessGoal() {
      if( CurrentGoal == null || CurrentGoal.Step == Steps.Completed ) DetermineNextGoal();

      switch( CurrentGoal.Type ) {
        case Goals.Stabilize:
          if( Tick == 99 )
            Stabilize();
          break;
        case Goals.Construct:
          Construct();
          break;
        case Goals.Attack:
          Attack();
          break;
        case Goals.Defend:
          Defend();
          break;
      }

    }

    public void Stabilize() {
      if( CurrentGoal == null || MainBase == null || MainBase.Grid == null ) return;
      if( MainBase.Grid.Physics.IsMoving ) return;
      if( RespawnPoint == null || !RespawnPoint.IsFunctional ) {
        Mulligan();
        return;
      }
      List<IMySlimBlock> batteries = MainBase.GetBlocks<IMyBatteryBlock>();
      if( batteries.Count == 0 || batteries[0].IsDestroyed ) {
        Mulligan();
        return;
      }

      // if( CommandLine.Switch("scavenger") ) {
      //   CurrentGoal.Complete();
      //   return;
      // }

      if( CurrentGoal.Step == Steps.Pending ) {
        if( MainBase.Grid.GridSizeEnum == MyCubeSize.Small ) {

          if( MainBase.AddLargeGridConverter() ) {
            CurrentGoal.Progress();
          } else {
            Mulligan();
          }
        } else {
          CurrentGoal.Complete();
        }
      } else {
        List<IMySlimBlock> assemblers = MainBase.GetBlocks<IMyAssembler>();
        if( MainBase.SuperGrid == null || MainBase.SuperGrid.MarkedForClose || MainBase.SuperGrid.Closed || assemblers.Count < 2 ) {
          Mulligan();
        } else {
          CurrentGoal.Complete();
        }

      }
    }

    public void Construct() {
      if( CurrentGoal == null ) return;

      if( CurrentGoal.Prefab == null )
        CurrentGoal.Prefab = GetConstructionProject();

      if( CurrentGoal.Step == Steps.Pending && CurrentGoal.Prefab != null ) {
        // Find Placement Location

        // TODO: Correct Orientation
        MatrixD location = GetPlacementLocation( CurrentGoal.Prefab );
        CubeGrid grid = new CubeGrid( CubeGrid.Spawn( CurrentGoal.Prefab, location) );

        if( grid == null || grid.Grid == null ) {
          return;
        }

        CurrentGoal.Entity = AddControllable(grid);

        grid.SetToConstructionSite();

        CurrentGoal.Progress();

      } else if( CurrentGoal.Step == Steps.Started ) {
        // Facilitate Production
        if( (CurrentGoal.Entity as CubeGrid).ConstructionSite == null ) {
          MyAPIGateway.Utilities.ShowMessage( Name, "Completed construction" );
          CurrentGoal.Complete();
          return;
        }
      }
    }

    public Prefab GetConstructionProject() {
      Prefab best = null;
      float priority = 0f;
      foreach( Prefab prefab in Prefab.Prefabs ) {
        float p = Prioritize(prefab);
        if( best == null || p > priority ) {
          best = prefab;
          priority = p;
        }
      }

      return best;
    }


    // https://github.com/KeenSoftwareHouse/SpaceEngineers/blob/a109106fc0ded66bdd5da70e099646203c56550f/Sources/Sandbox.Game/Game/Entities/Blocks/MyOreDetectorComponent.cs
    protected MatrixD GetPlacementLocation( Prefab prefab ) {

      //Dictionary<string, byte[]> maps = MyAPIGateway.Session.GetVoxelMapsArray();

      //if( maps[Homeworld] )
      int MAX = 1000;
      Vector3D position = Vector3D.Zero;

      //if( prefab.IsStatic ) {
        // Place further away

        int rand = Randy.Next(MAX);
        Vector3 p = new Vector3(MAX,rand,rand) + Vector3.Normalize(MainBase.Grid.WorldMatrix.Translation);
        //position = planet.GetClosestSurfacePointLocal( ref p );
        position = Homeworld.GetClosestSurfacePointGlobal( p );
        Vector3D up = (position - Homeworld.WorldMatrix.Translation);
        up.Normalize();
        position = position + (up * 2 );
      //} else {
        // Place close to base
      //}


      MyAPIGateway.Utilities.ShowMessage( Name, "Building " + prefab.ToString() + " at " + position.ToString() );

      // Vector3I min = new Vector3I(0,0,0);
      // Vector3I max = new Vector3I(100,100,100);
      // using (Homeworld.Storage.Pin()) { // Pin() is forbidden :(
      //   Homeworld.Storage.ReadRange(cache, MyStorageDataTypeFlags.ContentAndMaterial, 1, min, max);
      // }
      // Vector3I c;
      // for (c.Z = 0; c.Z < max.Z; ++c.Z)
      //     for (c.Y = 0; c.Y < max.Y; ++c.Y)
      //         for (c.X = 0; c.X < max.X; ++c.X) {
      //             int i = cache.ComputeLinear(ref c);
      //             if (cache.Content(i) > MyVoxelConstants.VOXEL_ISO_LEVEL) {
      //               byte material = cache.Material(i);
      //               MyVoxelMaterialDefinition def = MyDefinitionManager.Static.GetVoxelMaterialDefinition((byte)material);
      //               if( def != null && def.MinedOre != "Stone" ) {
      //                 MyAPIGateway.Utilities.ShowMessage( "Material", def.MinedOre );
      //               }
      //             }
      //         }

      // BoundingSphereD sphere = new BoundingSphereD( MainBase.Grid.WorldMatrix.Translation, (double)5000 );
      // List<IMyEntity> entities = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);
      //
      // foreach( IMyEntity entity in entities ) {
      //   IMyEntity root = entity.GetTopMostParent();
      //   MyObjectBuilder_EntityBase ob = root.GetObjectBuilder();
      //   MyAPIGateway.Utilities.ShowMessage( "Entity", entity.ToString() + " " + ob.Name );
      // }
      return MatrixD.CreateWorld( position );
    }

    public void Attack() {
      if( CurrentGoal == null ) return;
      if( CurrentGoal.Target == null ) {
        return;// TODO: Find new target
      }

      if( CurrentGoal.Step == Steps.Pending && CurrentGoal.Target != null ) {
        // This crashes the game for some reason
        // Order order66 = new Order(){
        //   Type = Orders.Attack,
        //   Target = CurrentGoal.Target
        // };
        // foreach( Controllable c in Controlled ) {
        //   c.Execute( order66, true );
        // }
        CurrentGoal.Progress();
      } else if( CurrentGoal.Step == Steps.Started ) {
        // This part should be working as intended
        // if( CurrentGoal.Target == null || CurrentGoal.Target.MarkedForClose || CurrentGoal.Target.Closed ) {
        //   CurrentGoal.Complete();
        //   foreach( Controllable c in Controlled ) {
        //     if( c.CurrentOrder != null && c.CurrentOrder.Type == Orders.Attack ) {
        //       c.CurrentOrder.Complete();
        //     }
        //   }
        // }
      }
    }

    public void Defend() {
      if( CurrentGoal == null ) return;
      // TODO: Prune list
      if( Enemies.Count == 0 ) {
        CurrentGoal.Complete();
      }
    }

    public Order NeedsOrder( Controllable c ) {
      if( CommandLine.Switch("aggressive") ) {
        return new Order {
          Type = Orders.Scout
        };
      }
      return null;
    }

    public void DetermineNextGoal() {
      if( CommandLine.Switch("aggressive") || CommandLine.Switch("scavenger") ) {
        CurrentGoal = new Goal{
          Type = Goals.Attack
        };
        return;
      }

      CubeGrid building = null;
      foreach( Controllable c in Controlled ) {
        if( c is CubeGrid ) {
          CubeGrid grid = c as CubeGrid;
          if( grid.ConstructionSite != null ) building = grid;
        }
      }

      if( building != null ) {
        CurrentGoal = new Goal{
          Type = Goals.Construct,
          Entity = building,
          Step = Steps.Started
        };
        return;
      }

      CurrentGoal = new Goal{
        Type = Goals.Construct
      };

    }

    public void AssessProgression() {
      if( MainBase == null || MainBase.Grid == null ) {
        MyAPIGateway.Utilities.ShowNotification( MainBase == null ? "AssessProgression() " + Name + " Main base is null" : Name + " Main base has no grid" );
        return;
      }

      IMySlimBlock slim;
      switch( Progress ) {
        case Progression.None:
          if( MainBase.Grid.GridSizeEnum == MyCubeSize.Small ) {
            if( MainBase.Grid.Physics.IsMoving ) return;

            List<IMySlimBlock> batteries = MainBase.GetBlocks<IMyBatteryBlock>();

            if( batteries.Count == 0 || !batteries[0].IsFullIntegrity ) {
              Mulligan();
              return;
            }

            if( MainBase.AddLargeGridConverter() ) {
              batteries = MainBase.GetBlocks<IMyBatteryBlock>();

             if( MainBase.SuperGrid == null || batteries.Count == 0 || !batteries[0].IsFullIntegrity ) {
               Mulligan();
               return;
              }
              Progress = Progression.BasicAssembler;
            } else {
              Mulligan();
              return;
            }

          } else {
            MainBase.TryPlace(new MyObjectBuilder_Assembler{
							SubtypeName = "BasicAssembler"
						});

            MainBase.TryPlace(new MyObjectBuilder_Refinery{
							SubtypeName = "Blast Furnace"
						});
          }
          break;
        case Progression.BasicAssembler:

          break;
        case Progression.BasicRefinery:
        case Progression.Assembler:
        case Progression.Refinery:
        case Progression.Reactor:
          break;
      }
    }

    public void Mulligan() {
      MyAPIGateway.Utilities.ShowMessage( "Mulligan", Name + " took a mulligan" );
      foreach( Controllable c in Controlled ) {
        MyAPIGateway.Entities.RemoveEntity( c.ControlledEntity as IMyEntity );
      }
      Controlled = new List<Controllable>();

      if( !Spawn() ) {
        Mulligan();
      }
    }


    public Controllable AddControllable( Controllable c ) {
      c.Owner = this;
      //c.Init( Session );
      Controlled.Add( c );
      if( c is Engineer ) {
        Engineer e = c as Engineer;
        Engineers.Add( e );

        if( CommandLine.Switch("scavenger") ) {
          e.Execute( new Order() {
            Type = Orders.Scout
          } );
        } else {
          e.Execute( new Order() {
            Type = Orders.Grind,
            Destination = MyAPIGateway.Session.Player.GetPosition()
          } );
        }

      }

      return c;
    }

    public MatrixD GetSpawnLocation() {

      if( RespawnPoint == null ) {
        foreach( Controllable c in Controlled ) {
          if( c is CubeGrid ) {
            CubeGrid grid = c as CubeGrid;
            RespawnPoint = grid.GetRespawnBlock();

            if( RespawnPoint != null ) {
              break;
            }
          }
        }
      }

      if( RespawnPoint != null ) {
        MatrixD matrix = RespawnPoint.CubeGrid.WorldMatrix;
        Vector3D translation = matrix.Translation;
        Vector3D forward = matrix.GetDirectionVector(Base6Directions.Direction.Forward);
        //translation.X += 5;

        return MatrixD.CreateWorld( translation + forward );
      } else {
        // TODO: Respawn Drop Pod
      }


      return MatrixD.Zero;
    }

    public IMyEntity GetClosestEnemy( Controllable to ) {
      IMyEntity source = to.ControlledEntity as IMyEntity;
      if( source == null ) return null;

      Vector3D center = source.WorldMatrix.Translation;
      BoundingSphereD sphere = new BoundingSphereD( center, (double)5000 );
      List<IMyEntity> entities = MyAPIGateway.Entities.GetEntitiesInSphere( ref sphere );
      IMyEntity best = null;
      double distance = 0.0f;
      foreach( IMyEntity entity in entities ) {
        // TODO: Exclude owned grids

        double d = Vector3D.Distance( entity.WorldMatrix.Translation, center );
        if( best == null || d < distance ) {
          best = entity;
          distance = d;
        }
      }

      return best;
    }

    public static MyPlanet GetRandomPlanet() {
      return SpaceCraftSession.Planets[Randy.Next(SpaceCraftSession.Planets.Count)];
		}

    public bool Spawn() {
      return Spawn( Vector3D.Zero );
    }

    public bool Spawn( Vector3D position ) {
      Homeworld = null;
      if( position == Vector3D.Zero ) {
        // Get Random Spawn
        if( CommandLine.Switch("outsider")){
          Homeworld = GetRandomPlanet();
        }

        if( Homeworld == null ) {
          Homeworld = SpaceCraftSession.ClosestPlanet;
        }
        int rand = Randy.Next(Homeworld.Size.X);
        Vector3 p = new Vector3(rand,rand,rand) + Vector3.Normalize(Homeworld.PositionLeftBottomCorner);
        //position = planet.GetClosestSurfacePointLocal( ref p );
        position = Homeworld.GetClosestSurfacePointGlobal( p );
        Vector3D up = (position - Homeworld.WorldMatrix.Translation);
        up.Normalize();
        position = position + (up * 150);
        //MyAPIGateway.Utilities.ShowMessage( Name, "Random Spawn " + position.ToString() );
        //planet.CorrectSpawnLocation( ref position, (double)planet.MaximumRadius );
      } else {
        //MyAPIGateway.Utilities.ShowMessage( Name, "Fixed Spawn " + position.ToString() );
      }

      MainBase = new CubeGrid(CubeGrid.Spawn("TerranPlanetPod", MatrixD.CreateWorld(position)));
      //MainBase.Init(Session);

      AddControllable( MainBase );

      if( MainBase.Grid == null ) {
        Mulligan();
        return false;
      }

      RespawnPoint = MainBase.GetRespawnBlock();

      position.X += 5;

      Engineer engineer = new Engineer( Engineer.Spawn(MatrixD.CreateWorld(position), this ) ) {
        Color = Color
      };

      if( engineer == null ) return false;

      AddControllable( engineer );

      // AddControllable( new Engineer(){
      //   Character = Engineer.Spawn(MatrixD.CreateWorld(position))
      // } as Controllable );

      if( CommandLine.Switch("aggressive") ) {
        CurrentGoal = new Goal{
          Type = Goals.Attack,
          Target = GetClosestEnemy( engineer )
        };
      } else {
        CurrentGoal = new Goal{
          Type = Goals.Stabilize,
          Entity = MainBase
        };
      }

      return true;
    }

    // public override void Init(MyObjectBuilder_SessionComponent session) {
    //   base.Init(session);
    // }

    protected float Prioritize( Prefab prefab ) {
      return prefab.IsStatic ? 1f : 0f;
    }


  }
}
