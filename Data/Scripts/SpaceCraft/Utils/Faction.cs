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

  public enum Tech {
    Primitive,
    Established,
    Space,
    Advanced
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
    public Goal CurrentGoal = new Goal{ Type = Goals.Stabilize };
    private IMyCubeBlock RespawnPoint;
    private Progression Progress = Progression.None;
    private string Roadblock = string.Empty;
    private List<Controllable> Controlled = new List<Controllable>();
    private List<IMyEntity> Enemies = new List<IMyEntity>();
    private CubeGrid MainBase;
    private List<string> Resources = new List<string>(){"Stone","Iron","Silicon","Nickel"};
    protected static Random Randy = new Random();
    protected int Tick = 0;
    public MyPlanet Homeworld;
    private static bool First = true;
    public Tech Teir = Tech.Primitive;

    // Main loop
    public void UpdateBeforeSimulation() {
      Tick++;

      AssessGoal();

      foreach( Controllable c in Controlled ) {
        c.UpdateBeforeSimulation();
      }

      if( Tick == 99 ) Tick = 0;
    }

    public void DetectedEnemy( IMyEntity enemy ) {
      if( !Enemies.Contains( enemy ) )
        Enemies.Add( enemy );
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

        MainBase.AddQueueItems( CurrentGoal.Prefab );

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
      int MAX = 100;

      //if( prefab.IsStatic ) {
        // Place further away

        int rand = Randy.Next(MAX);
        Vector3 p = new Vector3(MAX,rand,rand) + Vector3.Normalize(MainBase.Grid.WorldMatrix.Translation);
        //position = planet.GetClosestSurfacePointLocal( ref p );
        Vector3D position = Homeworld.GetClosestSurfacePointGlobal( p );
        Vector3D up = Vector3D.Normalize(position - Homeworld.WorldMatrix.Translation);

        if( !prefab.IsStatic )
          position = position + (up * 2 );
        //MatrixD matrix = MatrixD.CreateWorld( position );
        MatrixD reference = Homeworld.WorldMatrix;

        MyPositionAndOrientation origin = prefab.PositionAndOrientation.HasValue ? prefab.PositionAndOrientation.Value : MyPositionAndOrientation.Default;

        MatrixD matrix = MatrixD.CreateWorld( position );
        //MatrixD matrix = MatrixD.CreateWorld( position, origin.Forward,  origin.Up );
        MatrixD rotation = MatrixD.AlignRotationToAxes(ref matrix, ref reference);
        //MatrixD rotationDelta = MatrixD.Invert(origin.GetMatrix()) * rotation;
        //rotation = rotation * rotationDelta;


        // Vector3D 	SwapYZCoordinates (Vector3D v)
        //MatrixD matrix = MatrixD.CreateWorld( position, Vector3D.CalculatePerpendicularVector(up), up );

        //matrix = MatrixD.CreateWorld( position, rotation.Forward, rotation.Up );
        matrix = MatrixD.CreateWorld( position, rotation.Up, rotation.Left );

        //matrix = MatrixD.CreateWorld( position, up, Vector3D.CalculatePerpendicularVector(up) );

        //Matrix hitGridRotation = hitGrid.WorldMatrix.GetOrientation();
        //Base6Directions.Direction direction = matrix.GetClosestDirection(up);
        //matrix.Up = up;
        //Vector3D direction = matrix.GetDirectionVector(Base6Directions.Direction.Up);
        //matrix.SetDirectionVector( direction, up );
      //} else {
        // Place close to base
      //}


      MyAPIGateway.Utilities.ShowMessage( Name, "Building " + prefab.ToString() + " at " + position.ToString() );

      return matrix;
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
      if( c == null ) {
        MyAPIGateway.Utilities.ShowMessage( "NeedsOrder", "Controllable is null" );
        return null;
      }
      if( CurrentGoal != null ) switch( CurrentGoal.Type ) {

        case Goals.Construct:
          if( c == CurrentGoal.Entity ) break;
          // Determine if unit should transfer items
          if( c.Cargo && !c.IsStatic && CurrentGoal.Entity != MainBase && GetWithdrawlSource(c) != null ) {
            return null;
          } else if( c.Drills ) {
            break;
          }
          goto case Goals.Defend;

        case Goals.Defend:
          if( CurrentGoal.Type == Goals.Defend && Enemies.Count == 0 ) {
            CurrentGoal.Complete();
            break;
          }
          goto case Goals.Attack;

        case Goals.Attack:
          if( !CommandLine.Switch("defensive") ) {
            return new Order {
              Type = Enemies.Count == 0 ? Orders.Scout : Orders.Attack
            };
          }
          break;
      }

      if( c.Entity == null ) {
        MyAPIGateway.Utilities.ShowMessage( "NeedsOrder", c.ToString() + ": c.Entity is null" );
        return null;
      }
      Vector3D position = c.Entity.WorldMatrix.Translation;
      MyPlanet planet = SpaceCraftSession.GetClosestPlanet( position );
      if( planet == null ) {
        MyAPIGateway.Utilities.ShowMessage( "NeedsOrder", c.ToString() + ": planet is null" );
        return null;
      }
      return new Order {
        Type = Orders.Drill,
        Destination = planet.GetClosestSurfacePointGlobal(position),
        // TODO: Determine best drill location
        //Destination = Homeworld.GetClosestSurfacePointGlobal(c.Entity.WorldMatrix.Translation),
        Range = 20f
      };
    }

    public CubeGrid GetWithdrawlSource( Controllable controllable ) {
      int available = controllable.AvailableVolume.ToIntSafe();
      float volume = (float)available;

      IMySlimBlock target = (CurrentGoal.Entity as CubeGrid).ConstructionSite;
      if( target == null ) return null;

      Dictionary<string,int> total = new Dictionary<string,int>();
      target.GetMissingComponents(total);

      Dictionary<string,int> missing = new Dictionary<string,int>(total);
      MyAPIGateway.Utilities.ShowMessage( "Missing", string.Join(",", missing.Keys ) );

      foreach(Controllable c in Controlled ) {
        if( !(c is CubeGrid) ) continue;
        CubeGrid grid = c as CubeGrid;
        if( CurrentGoal.Type == Goals.Construct && grid == CurrentGoal.Entity ) continue;
        Dictionary<string,int> surplus = c.GetSurplus();
        MyAPIGateway.Utilities.ShowMessage( "Surplus", string.Join(",", surplus.Keys ) );
        foreach( string st in total.Keys ) {
          if( surplus.ContainsKey( st ) ) {
            MyPhysicalItemDefinition def = MyDefinitionManager.Static.GetPhysicalItemDefinition( MyDefinitionId.Parse("MyObjectBuilder_Component/" + st) );
            if( surplus[st] >= missing[st] ) {
              volume -= def.Volume * (missing[st]-surplus[st]);
              missing[st] = 0;
            } else {
              volume -= def.Volume * surplus[st];
              missing[st] -= surplus[st];
            }
          }
        }

        bool fulfilled = true;
        foreach( int amount in missing.Values ) {
          if( amount > 0 ) {
            fulfilled = false;
            break;
          }
        }

        // VRage.MyFixedPoint v = volume;
        // foreach( string st in surplus.Keys ) {
        //   MyPhysicalItemDefinition def = MyDefinitionManager.Static.GetPhysicalItemDefinition(  MyDefinitionId.Static.Parse(st) );
        //   v -= def.Volume * surplus[ob];
        //   if( missing.HasKey( def.Id.SubtypeName ) ) {
        //     if( missing[def.Id.SubtypeName] > surplus[def.Id.SubtypeName] )
        //   }
        // }
        // TODO: Check if order is fulfilled, not just volume check
        //if( target.IsFullyDismounted && v / volume > 0.1  ) {
        if( fulfilled || volume <= 0 ) {
          controllable.Drop( OBTypes.Ore );
          controllable.Execute( new Order {
            Type = Orders.Withdraw,
            Entity = grid,
            Target = grid.Grid
          });
          return grid;
        }
      }

      return null;
    }

    public CubeGrid GetClosestGrid( Controllable from ) {
      CubeGrid best = null;
      double distance = 0;
      foreach( Controllable c in Controlled ) {
        if( c == from || c is Engineer ) continue;
        CubeGrid grid = c as CubeGrid;
        double d = Vector3D.Distance( grid.Grid.WorldMatrix.Translation, from.Entity.WorldMatrix.Translation );
        if( best == null || d < distance ) {
          best = grid;
          d = distance;
        }
      }

      return best;
    }

    public void DetermineNextGoal() {
      // if( CommandLine.Switch("aggressive") || CommandLine.Switch("scavenger") ) {
      //   CurrentGoal = new Goal{
      //     Type = Goals.Attack
      //   };
      //   return;
      // }

      CubeGrid building = null;
      CubeGrid needs = null;
      foreach( Controllable c in Controlled ) {
        if( c is CubeGrid ) {
          CubeGrid grid = c as CubeGrid;
          if( grid.ConstructionSite != null ) building = grid;
          // else if( building == null ) {
          //   grid.AssessNeed();
          // }
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

      //if( Enemies.Count == 0 || CommandLine.Switch("defensive") ) {
        CurrentGoal = new Goal{
          Type = Goals.Construct
        };
      // } else {
      //   CurrentGoal = new Goal{
      //     Type = Goals.Attack,
      //     Target = Enemies[0]
      //   };
      // }

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
        MyAPIGateway.Entities.RemoveEntity( c.Entity );
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
        MatrixD matrix = RespawnPoint.WorldMatrix;
        Vector3D translation = matrix.Translation;
        Vector3D forward = matrix.GetDirectionVector(Base6Directions.Direction.Forward);
        //translation.X += 5;

        return MatrixD.CreateWorld( translation + (forward*3) );
      } else {
        Mulligan();
      }


      return MatrixD.Zero;
    }

    public IMyEntity GetClosestEnemy( Controllable to ) {
      IMyEntity source = to.Entity;
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
        //int rand = Randy.Next(Homeworld.Size.X);
        Vector3 p = new Vector3(Randy.Next(Homeworld.Size.X),Randy.Next(Homeworld.Size.Y),Randy.Next(Homeworld.Size.Z)) + Vector3.Normalize(Homeworld.PositionLeftBottomCorner);
        //position = planet.GetClosestSurfacePointLocal( ref p );
        position = Homeworld.GetClosestSurfacePointGlobal( p );
        Vector3D up = (position - Homeworld.WorldMatrix.Translation);
        up.Normalize();
        position = position + (up * 150);
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
