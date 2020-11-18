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

  public enum Tech : ushort {
    Primitive,
    Established,
    Advanced,
    Space,
    Future
  };

  //[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
  //public class Faction:MySessionComponentBase {
  public class Faction {

    public class Stats {
      public float Grids = 0f;
      public float Static = 0f;
      public float Workers = 0f;
      public float Fighters = 0f;
      public float Factories = 0f;
      public float Refineries = 0f;
      public int Spacecraft = 0;
      public bool InSpace = false; // TODO
      public Dictionary<string,float> Ratio = new Dictionary<string,float>{};
      public Dictionary<string,float> Desired = new Dictionary<string,float>{};
    }

    public static int LIMIT = 20;

    public int Engineers = 0;

    public string Name;
    public MyCommandLine CommandLine = new MyCommandLine();
    public List<MySpawnGroupDefinition> Groups = new List<MySpawnGroupDefinition>();
    public bool Spawned = false;
    public SerializableVector3 Color = new SerializableVector3(0f,-0.8f,-0.306840628f);
    public Goal CurrentGoal = new Goal{ Type = Goals.Stabilize };
    private IMyCubeBlock RespawnPoint;
    private Progression Progress = Progression.None;
    private string Roadblock = string.Empty;
    public List<Controllable> Controlled = new List<Controllable>();
    private List<IMyEntity> Enemies = new List<IMyEntity>();
    public CubeGrid MainBase;
    //private List<string> Resources = new List<string>(){"Stone","Iron","Silicon","Nickel","Cobalt"};
    private List<string> Resources = new List<string>(){"Stone","Iron","Silicon","Nickel"};
    protected static Random Randy = new Random();
    protected int Tick = 0;
    public MyPlanet Homeworld;
    public List<MyPlanet> Colonized = new List<MyPlanet>();
    private static bool First = true;
    public Tech Tier = Tech.Primitive;
    public IMyFaction MyFaction;
    public IMyIdentity Founder;
    public string SpawnPrefab;
    public Stats MyStats = new Stats();
    public Vector3D Origin = Vector3D.Zero;
    public IMyPlayer Following;
    // public Vector3I Scouted = Vector3I.Zero;
    //
    // public enum Axes {
    //   X,
    //   Y,
    //   Z
    // }
    //
    // public uint ScoutAxis = Axes.X;

    // Main loop
    public void UpdateBeforeSimulation() {
      Tick++;

      if( Enemies.Count > 0 && (Enemies[0] == null || Enemies[0].MarkedForClose) ) {
        Enemies.RemoveAt(0);
      }
      AssessGoal();

      Controllable remove = null;
      foreach( Controllable c in Controlled ) {
        if( c is CubeGrid && c.Entity == null )
          remove = c;
        else
          c.UpdateBeforeSimulation();
      }

      if( remove != null ) Controlled.Remove( remove );

      if( Tick == 99 ) {
        Tick = 0;

        if( Engineers < Convars.Static.Engineers ) {
          TakeControl( new Engineer(this) );
        } else if( Engineers > Convars.Static.Engineers ) {
          RemoveEngineer();
        }

      }
    }

    public void Follow( IMyPlayer player ) {
      Following = player;
      foreach( Controllable c in Controlled ) {
        if( c is Engineer ) {
          c.Execute( new Order {
            Type = Orders.Follow,
            Player = player,
            Range = 20f
          }, true );
        }
      }
    }

    public void DetectedEnemy( IMyEntity enemy ) {
      if( !Enemies.Contains( enemy ) )
        Enemies.Add( enemy );
    }

    public void RemoveEngineer() {
      Engineer remove = null;
      foreach( Controllable c in Controlled ) {
        if( c is Engineer ) {
          remove = c as Engineer;
          break;
        }
      }
      if( remove != null ) {
        remove.Character.Kill();
        Controlled.Remove( remove );
        Engineers--;
      }
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
        case Goals.Colonize:
          Colonize();
          break;
      }

    }

    public void Colonize(MyPlanet planet) {
      if( planet == null || Colonized.Contains(planet)) return;
      Colonized.Add(planet);
    }

    public void Colonize() {
      if( CurrentGoal == null ) return;
      if( CurrentGoal.Entity == null ) CurrentGoal.Entity = GetSpacecraft();
      if( CurrentGoal.Entity == null ) return;

      if( CurrentGoal.Step == Steps.Pending ) {
        if( Convars.Static.Debug ) MyAPIGateway.Utilities.ShowMessage( "Colonize", "Started Colonization" );
        (CurrentGoal.Entity as CubeGrid).AddQueueItems( CurrentGoal.Prefab );
        CurrentGoal.Progress();
      } else if( CurrentGoal.Step == Steps.Started ) {
        if( !(CurrentGoal.Entity as CubeGrid).IsProducing ) {
          if( Convars.Static.Debug ) MyAPIGateway.Utilities.ShowMessage( "Colonize", "Production finished" );
          CurrentGoal.Entity.Execute( new Order {
            Type = Orders.Move,
            Target = CurrentGoal.Target,
            Range = 100f
          }, true );
        }
        CurrentGoal.Progress();
      } else {
        if( CurrentGoal.Entity.CurrentOrder == null || CurrentGoal.Entity.CurrentOrder.Step == Steps.Completed || CurrentGoal.Entity.CurrentOrder.Type != Orders.Move ) {
          if( Convars.Static.Debug ) MyAPIGateway.Utilities.ShowMessage( "Colonize", "Goal reached" );
          MainBase = CurrentGoal.Entity as CubeGrid;
          MatrixD location = GetPlacementLocation( CurrentGoal.Prefab, MainBase );
          CubeGrid grid = new CubeGrid( CubeGrid.Spawn( CurrentGoal.Prefab, location, this) );

          if( grid == null || grid.Grid == null ) {
            return;
          }

          TakeControl(grid);
          CurrentGoal.Entity = grid;

          grid.SetToConstructionSite();

          MainBase.ToggleDocked( grid );
          //MainBase.AddQueueItems( CurrentGoal.Prefab );
          MainBase.FindConstructionSite();

          CurrentGoal = new Goal {
            Type = Goals.Construct,
            Prefab = CurrentGoal.Prefab,
            Entity = grid,
            Step = Steps.Started
          };
        }
      }
    }

    public void Stabilize() {
      if( CurrentGoal == null || MainBase == null ) return;
      if( MainBase.Grid == null || MainBase.Grid.Closed || MainBase.Grid.MarkedForClose ) {
        Mulligan();
        return;
      }
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

      if( CurrentGoal.Prefab == null && CurrentGoal.Entity == null ) {

        CurrentGoal.Prefab = CurrentGoal.Prefab ?? GetConstructionProject();

        CurrentGoal.Balance = CurrentGoal.Prefab == null ? null : CurrentGoal.Prefab.GetBalance();

        MainBase = GetBestRefinery();
        if( MainBase.DockedTo == null ) {
          MainBase.Balance = CurrentGoal.Balance;
          MainBase.AddQueueItems(CurrentGoal.Prefab.GetBattery(),true);
        } else {
          MainBase.DockedTo.Balance = CurrentGoal.Balance;
          MainBase.DockedTo.AddQueueItems(CurrentGoal.Prefab.GetBattery(),true);
        }


      }

      if( CurrentGoal.Step == Steps.Pending && CurrentGoal.Prefab != null ) {

        // Wait for balance to be paid
        if( CurrentGoal.Balance != null && CurrentGoal.Balance.Count > 0 ) return;

        MatrixD location = GetPlacementLocation( CurrentGoal.Prefab );
        CubeGrid grid = new CubeGrid( CubeGrid.Spawn( CurrentGoal.Prefab, location, this) );

        if( grid == null || grid.Grid == null ) {
          return;
        }

        TakeControl(grid);
        CurrentGoal.Entity = grid;

        grid.SetToConstructionSite();
        MainBase = GetBestRefinery();
        MainBase.ToggleDocked( grid );
        //MainBase.AddQueueItems( CurrentGoal.Prefab );
        MainBase.FindConstructionSite();
        CurrentGoal.Progress();

      } else if( CurrentGoal.Step == Steps.Started ) {
        // Facilitate Production
        CubeGrid grid = CurrentGoal.Entity as CubeGrid;
        if( grid == null || grid.ConstructionSite == null ) {
          if( Convars.Static.Debug ) MyAPIGateway.Utilities.ShowMessage( Name, "Completed construction" );
          // if( grid.DockedTo != null && !grid.Drills )
          //   grid.DockedTo.ToggleDocked( grid );
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
    public MatrixD GetPlacementLocation( Prefab prefab, CubeGrid last = null ) {
      if( last == null ) last = GetLastCreated();
      //Dictionary<string, byte[]> maps = MyAPIGateway.Session.GetVoxelMapsArray();

      //if( maps[Homeworld] )
      int MAX = 100;


      //Vector3D 	MyAPIGateway.Entities.FindFreePlace(Vector3D basePos, float radius, int maxTestCount=20, int testsPerDistance=5, float stepSize=1)

        MatrixD m = last.Grid.WorldMatrix;
        Vector3D p = m.Translation;
        MyPlanet planet = SpaceCraftSession.GetClosestPlanet(p);
        // if( prefab.IsStatic && Tier >= Tech.Space ) {
        //   // TODO: Determine resource wanted
        //   string resource = "Uranium";
        //   if( Resources.Contains("Uranium") && !Resources.Contains("Platinum") ) {
        //     resource = "Platinum";
        //   }
        //   planet = SpaceCraftSession.GetClosestPlanet(p, Colonized, resource);
        //   Colonized.Add( planet );
        // } else {
        //   planet = SpaceCraftSession.GetClosestPlanet(p);
        // }
        //Quaternion q = Quaternion.CreateFromRotationMatrix(m);
        //q.W += Controlled.Count;

        Vector3 blocked = last.Grid.LocalAABB.Size; // size of last base
        BoundingBox box = prefab.Definition.BoundingBox; // box around prefab

        Vector3D offset = m.Forward + m.Up;
        if( prefab.IsStatic ) {
          p = p + (m.Forward*100) + (m.Up*100);
          //p = p + blocked + (offset*(size+10)*Controlled.Count);
        } else {
          p = p + blocked + (box.Size*2);
        }

        Vector3D position = planet.GetClosestSurfacePointGlobal( p );
        Vector3D up = Vector3D.Normalize(position - planet.WorldMatrix.Translation);
        Vector3D perp = Vector3D.CalculatePerpendicularVector(up);

        if( !prefab.IsStatic )
          position = position + (up * (box.Height*.75) );
        MatrixD reference = planet.WorldMatrix;

        MyPositionAndOrientation origin = prefab.PositionAndOrientation.HasValue ? prefab.PositionAndOrientation.Value : MyPositionAndOrientation.Default;

        MatrixD matrix = MatrixD.CreateWorld( position, perp, up );
        //MatrixD matrix = MatrixD.CreateWorld( position, origin.Forward,  origin.Up );

        //MatrixD rotationDelta = MatrixD.Invert(origin.GetMatrix()) * rotation;
        //rotation = rotation * rotationDelta;


        // Vector3D 	SwapYZCoordinates (Vector3D v)
        //MatrixD matrix = MatrixD.CreateWorld( position, Vector3D.CalculatePerpendicularVector(up), up );


        if( prefab.SubtypeId == "Terran Battlecruiser" )
          matrix = MatrixD.CreateWorld( position, matrix.Right, matrix.Down );
        else if( prefab.IsStatic )
  			   matrix = MatrixD.CreateWorld(position, matrix.Backward, matrix.Left);


        //matrix = MatrixD.CreateWorld( position, rotation.Forward, rotation.Up );
        // MatrixD rotation = MatrixD.AlignRotationToAxes(ref matrix, ref reference);
        // if( prefab.SubtypeId == "Terran Battlecruiser" )
        // matrix = MatrixD.CreateWorld( position, rotation.Right, rotation.Backward );
        // else
        //   //matrix = MatrixD.CreateWorld( position, prefab.IsStatic ? rotation.Up : rotation.Right, prefab.IsStatic ? rotation.Left : rotation.Forward );
        //   matrix = MatrixD.CreateWorld( position, prefab.IsStatic ? rotation.Up : rotation.Right, prefab.IsStatic ? rotation.Left : rotation.Forward );
        //matrix = MatrixD.CreateWorld( position, rotation.Up, rotation.Left );


        //matrix = MatrixD.CreateWorld( position, up, Vector3D.CalculatePerpendicularVector(up) );

        //Matrix hitGridRotation = hitGrid.WorldMatrix.GetOrientation();
        //Base6Directions.Direction direction = matrix.GetClosestDirection(up);
        //matrix.Up = up;
        //Vector3D direction = matrix.GetDirectionVector(Base6Directions.Direction.Up);
        //matrix.SetDirectionVector( direction, up );
      //} else {
        // Place close to base
      //}

      if( Convars.Static.Debug )
        MyAPIGateway.Utilities.ShowMessage( Name, "Building " + prefab.ToString() + " at " + position.ToString() );

      return matrix;
    }

    public Controllable GetControllable( IMyEntity entity ) {
      foreach( Controllable c in Controlled ) {
        if( c.Entity == entity ) return c;
      }
      return null;
    }

    public void Attack() {
      if( CurrentGoal == null ) return;
      if( CurrentGoal.Target == null && Enemies.Count > 0 ) {
        return;// Waiting for scouting intel
      } else if( CurrentGoal.Target == null ) {
        CurrentGoal.Target = Enemies[0];
      }

      if( CurrentGoal.Step == Steps.Pending && CurrentGoal.Target != null ) {
        // This crashes the game for some reason
        foreach( Controllable c in Controlled ) {
          c.Execute( new Order{
            Type = Orders.Attack,
            Target = CurrentGoal.Target
          }, true );
        }
        CurrentGoal.Progress();
      } else if( CurrentGoal.Step == Steps.Started ) {

        if( CurrentGoal.Target == null || CurrentGoal.Target.MarkedForClose || CurrentGoal.Target.Closed ) {
          CurrentGoal.Complete();
          foreach( Controllable c in Controlled ) {
            if( c.CurrentOrder != null && c.CurrentOrder.Type == Orders.Attack ) {
              c.CurrentOrder.Complete();
            }
          }
        }
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
      if( c == null || CurrentGoal == null ) return null;

      switch( CurrentGoal.Type ) {

        case Goals.Stabilize:
        case Goals.Construct:
          if( c == CurrentGoal.Entity ) break;
          // Determine if unit should transfer items
          /*if( c.Cargo && !c.IsStatic && CurrentGoal.Entity != MainBase && GetWithdrawlSource(c) != null ) {
            return null;
          } else*/ if( c.Drills && !CommandLine.Switch("scavenger") ) {
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
          if( !CommandLine.Switch("defensive") ) break;
          if( Enemies.Count == 0 ) {
            IMyEntity enemy = GetClosestEnemy(c.Entity.WorldMatrix.Translation);
            if( enemy == null) break;
            Enemies.Add(enemy);
          }
          return new Order {
            Type = Orders.Attack,
            Target = Enemies[0]
          };
            // if( Enemies.Count == 0 ) {
            //   c.OrderQueue = GetScoutOrders();
            //   if( c.OrderQueue.Count > 0 ) {
            //     Order o = c.OrderQueue[0];
            //     c.OrderQueue.RemoveAt(0);
            //     return o;
            //   }
            // } else return new Order {
            //   Type = Orders.Attack,
            //   Target = Enemies[0]
            // };

          break;
      }

      if( c.Entity == null ) return null;
      //Vector3D position = c.Entity.WorldMatrix.Translation;
      Vector3D position = GetBestRefinery().Entity.WorldMatrix.Translation;
      //Vector3D position = c.Wheels || c.Atmosphere ? c.Entity.WorldMatrix.Translation : GetBestRefinery().Entity.WorldMatrix.Translation;
      MyPlanet planet = SpaceCraftSession.GetClosestPlanet( position );
      if( planet == null ) return null;

      // if( c is Engineer && Stats.Workers > 0 ) {
      //   return new Order {
      //     Type = Orders.Scout
      //   };
      // }

      return new Order {
        Type = Orders.Drill,
        //Target = planet,
        Resources = GetDrillResources( planet, c ),
        Destination = planet.GetClosestSurfacePointGlobal(position),
        // TODO: Determine best drill location
        //Destination = Homeworld.GetClosestSurfacePointGlobal(c.Entity.WorldMatrix.Translation),
        Range = 20f,
        //Entity = GetBestRefinery(c)
      };
    }

    public CubeGrid GetBestRefinery( Controllable c = null ) {
      CubeGrid refinery = null;
      double distance = 0f;
      foreach( Controllable o in Controlled ) {
        if( o.RefineryTier == 0 ) continue;

        double d = c == null ? 0 : Vector3D.Distance( c.Entity.WorldMatrix.Translation, o.Entity.WorldMatrix.Translation );
        if( refinery == null ) {
          refinery = o as CubeGrid;
          distance = d;
          continue;
        }

        if( o.RefineryTier > refinery.RefineryTier || (o.RefineryTier == refinery.RefineryTier && d < distance)  ) {
          refinery = o as CubeGrid;
          distance = d;
        }
      }

      return refinery;
    }

    public List<Order> GetScoutOrders() {
      List<Order> orders = new List<Order>();
      if( MainBase == null || MainBase.Grid == null ) return orders;
      if( Origin == Vector3D.Zero ) Origin = MainBase.Grid.WorldMatrix.Translation;

      List<Vector3D> destinations = new List<Vector3D>();
      Vector3I d = new Vector3I(0,0,0);
      for( d.X = -1; d.X < 2; d.X++ )
        for( d.Y = -1; d.Y < 2; d.Y++ )
          for( d.Z = -1; d.Z < 2; d.Z++ ) {
            Vector3D destination = Origin + (d*500);
            MyPlanet planet = SpaceCraftSession.GetClosestPlanet(destination);
            destination = planet.GetClosestSurfacePointGlobal(destination);
            Vector3D up = Vector3D.Normalize(destination - planet.WorldMatrix.Translation);
            //Vector3D up = Vector3D.Normalize(destination - planet.PositionComp.WorldVolume.Center);
            destination = destination + (up*150);
            if( !destinations.Contains(destination) ) {
              orders.Add( new Order{
                Type = Orders.Scout,
                Destination = destination,
                Planet = planet,
                Range = 100f
              });
              destinations.Add(destination);
            }
          }

      return orders;
    }

    public void Detected( IMyEntity detected ) {
      if( Enemies.Contains(detected) ) return;
      if( detected is IMyCharacter ) {
        foreach( Controllable c in Controlled ) {
          if( c.Entity == detected ) return;
        }
        Enemies.Add( detected );
      } else if( detected is IMyCubeGrid ) {
        IMyCubeGrid grid = detected as IMyCubeGrid;
        List<long> owners = new List<long>(grid.BigOwners);
        owners.AddRange(grid.SmallOwners);
        foreach( long owner in owners ) {
          IMyFaction faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(owner);
          if( faction == null ) continue;
          if( MyRelationsBetweenFactions.Enemies == MyAPIGateway.Session.Factions.GetRelationBetweenFactions(MyFaction.FactionId, faction.FactionId) ) {
            Enemies.Add(detected);
            return;
          }
        }
      }

    }

    public Dictionary<string,VRage.MyFixedPoint> GetDrillResources( MyPlanet planet, Controllable c ) {
      Dictionary<string,VRage.MyFixedPoint> resources = new Dictionary<string,VRage.MyFixedPoint>();
      CubeGrid grid = c as CubeGrid;
      if( grid != null && grid.DockedTo != null && grid.DockedTo.RefineryTier == 1 ) {
        resources.Add("Stone",(VRage.MyFixedPoint)1*Convars.Static.Difficulty);
        return resources;
      }
      switch( Tier ) {
        case Tech.Primitive:
          resources.Add("Stone",(VRage.MyFixedPoint)1*Convars.Static.Difficulty);
          break;
        //case Tech.Established:
        case Tech.Advanced:
          resources.Add("Silver",(VRage.MyFixedPoint)0.01*Convars.Static.Difficulty);
          resources.Add("Gold",(VRage.MyFixedPoint)0.01*Convars.Static.Difficulty);
          goto default;
        default:
          resources.Add("Iron",(VRage.MyFixedPoint)0.2*Convars.Static.Difficulty);
          resources.Add("Nickel",(VRage.MyFixedPoint)0.01*Convars.Static.Difficulty);
          resources.Add("Cobalt",(VRage.MyFixedPoint)0.002*Convars.Static.Difficulty);
          resources.Add("Ice",(VRage.MyFixedPoint)0.01*Convars.Static.Difficulty);
          resources.Add("Magnesium",(VRage.MyFixedPoint)0.01*Convars.Static.Difficulty);
          resources.Add("Silicon",(VRage.MyFixedPoint)0.01*Convars.Static.Difficulty);
          resources.Add("Stone",(VRage.MyFixedPoint)0.005*Convars.Static.Difficulty);
          break;
      }
      return resources;
    }

    public CubeGrid GetWithdrawlSource( Controllable controllable ) {
      int available = controllable.AvailableVolume.ToIntSafe();
      float volume = (float)available;

      IMySlimBlock target = (CurrentGoal.Entity as CubeGrid).ConstructionSite;
      if( target == null ) return null;

      Dictionary<string,int> total = new Dictionary<string,int>();
      target.GetMissingComponents(total);

      Dictionary<string,int> missing = new Dictionary<string,int>(total);
      if( Convars.Static.Debug ) MyAPIGateway.Utilities.ShowMessage( "Missing", string.Join(",", missing.Keys ) );

      foreach(Controllable c in Controlled ) {
        if( !(c is CubeGrid) ) continue;
        CubeGrid grid = c as CubeGrid;
        if( CurrentGoal.Type == Goals.Construct && grid == CurrentGoal.Entity ) continue;
        Dictionary<string,int> surplus = c.GetSurplus();
        if( Convars.Static.Debug ) MyAPIGateway.Utilities.ShowMessage( "Surplus", string.Join(",", surplus.Keys ) );
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

    public CubeGrid GetLastCreated() {
      CubeGrid last = null;
      foreach( Controllable c in Controlled ) {
        if( c is CubeGrid )
          last = c as CubeGrid;
      }
      return last;
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

      MyStats = new Stats();

      CubeGrid building = null;
      CubeGrid needs = null;
      foreach( Controllable c in Controlled ) {
        if( c is CubeGrid ) {
          CubeGrid grid = c as CubeGrid;
          MyStats.Grids++;
          if( grid.ConstructionSite != null ) building = grid;

          if( grid.IsStatic ) MyStats.Static++;
          else if( grid.Drills ) MyStats.Workers++;
          if( grid.Fighter ) MyStats.Fighters++;
          if( grid.FactoryTier > 0 ) MyStats.Factories++;
          if( grid.RefineryTier > 0 ) MyStats.Refineries++;
          if( grid.Spacecraft ) MyStats.Spacecraft++;
          // else if( building == null ) {
          //   grid.AssessNeed();
          // }
        }
      }

      if( MyStats.Grids == 0 ) {
        Mulligan();
        return;
      }

      // if( MyStats.Grids == Convars.Static.Grids ) {
      //
      // }

      MyStats.Ratio.Add("Workers", MyStats.Workers/MyStats.Grids);
      MyStats.Ratio.Add("Fighters", MyStats.Fighters/MyStats.Grids);
      MyStats.Ratio.Add("Factories", MyStats.Factories/MyStats.Grids);
      MyStats.Ratio.Add("Refineries", MyStats.Refineries/MyStats.Grids);
      MyStats.Ratio.Add("Static", MyStats.Static/MyStats.Grids);

      MyStats.Desired.Add("Workers", .5f);
      MyStats.Desired.Add("Fighters", CommandLine.Switch("aggressive") ? .75f : .5f );
      MyStats.Desired.Add("Factories", .45f);
      MyStats.Desired.Add("Refineries", .45f);
      MyStats.Desired.Add("Static", CommandLine.Switch("aggressive") ? .75f : .5f );

      //MyAPIGateway.Utilities.ShowMessage( "MyStats", "Workers: " + MyStats.Workers.ToString() + " (" + MyStats.Ratio["Workers"] + "/" + MyStats.Desired["Workers"] + ")" );

      if( building != null ) {
        CurrentGoal = new Goal{
          Type = Goals.Construct,
          Entity = building,
          Step = Steps.Started
        };
        return;
      }

      if( MyStats.Grids >= Convars.Static.Grids ) { // Limit reached
        CurrentGoal = new Goal{
          Type = CommandLine.Switch("defensive") ? Goals.Defend : Goals.Attack
        };
        // if( CurrentGoal.Type == Goals.Attack && Enemies.Count == 0 ) {
        //   IMyEntity enemy = GetClosestEnemy(MainBase.Entity.WorldMatrix.Translation);
        //   if( enemy == null) break;
        //   Enemies.Add(enemy);
        // }
        return;
      }

      if( Tier == Tech.Advanced ) {
        // Get to space
        if( MyStats.Spacecraft == 0 ) {
          // Build Spacecraft
          CurrentGoal = new Goal{
            Type = Goals.Construct,
            Prefab = Prefab.Get("Terran Battlecruiser")
          };
          return;
        } else {
          CubeGrid sc = GetSpacecraft();
          MyPlanet target = SpaceCraftSession.GetClosestPlanet( sc.Grid.WorldMatrix.Translation, new List<MyPlanet>{Homeworld}, "Ice" );
          MyAPIGateway.Utilities.ShowMessage( "Colonize", sc.ToString() + ": " + target.ToString() );
          CurrentGoal = new Goal{
            Type = Goals.Colonize,
            Entity = sc,
            Target = target,
            Prefab = Prefab.Get("Terran SCV (Space)")
          };
        }
      }

      CurrentGoal = new Goal{
        Type = Goals.Construct
      };

    }

    public CubeGrid GetSpacecraft() {
      foreach( Controllable c in Controlled ) {
        if( c.Spacecraft ) return c as CubeGrid;
      }
      return null;
    }

    public void Mulligan() {
      if( Convars.Static.Debug ) MyAPIGateway.Utilities.ShowMessage( "Mulligan", Name + " took a mulligan" );
      foreach( Controllable c in Controlled ) {
        if( c.Entity == null ) continue;
        MyAPIGateway.Entities.RemoveEntity( c.Entity );
      }
      Controlled = new List<Controllable>();
      Colonized = new List<MyPlanet>();
      Engineers = 0;
      Tier = Tech.Primitive;
      CurrentGoal = new Goal{
        Type = Goals.Stabilize
      };
      if( !Spawn() ) {
        Mulligan();
      }
    }


    public Controllable TakeControl( Controllable c ) {
      c.Owner = this;

      //c.Init( Session );
      Controlled.Add( c );

      if( c is Engineer )
        Engineers++;

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

    public IMyEntity GetClosestEnemy( Vector3D point, IMyFaction faction = null ) {
      //BoundingSphereD sphere = new BoundingSphereD( center, (double)5000 );
      //List<IMyEntity> entities = MyAPIGateway.Entities.GetEntitiesInSphere( ref sphere );
      IMyEntity best = null;
      double distance = 0.0f;
      foreach( IMyEntity entity in Enemies ) {
        IMyFaction owner = null;
        if( faction != null ) {
          if( entity is IMyCubeGrid ) {
            IMyCubeGrid grid = entity as IMyCubeGrid;
            owner = MyAPIGateway.Session.Factions.TryGetPlayerFaction(grid.GridSizeEnum == MyCubeSize.Large ? grid.BigOwners[0] : grid.SmallOwners[0]);
            if( owner != faction ) continue;
          }/* else if( entity is IMyCharacter ) {
            owner = MyAPIGateway.Session.Factions.TryGetPlayerFaction();
            if( owner != faction ) continue;
          }*/
        }
        double d = Vector3D.Distance( entity.WorldMatrix.Translation, point );
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
        Colonized.Add( Homeworld );
        //int rand = Randy.Next(Homeworld.Size.X);
        Vector3 p = new Vector3(Randy.Next(Homeworld.Size.X),Randy.Next(Homeworld.Size.Y),Randy.Next(Homeworld.Size.Z)) + Vector3.Normalize(Homeworld.PositionLeftBottomCorner);
        //position = planet.GetClosestSurfacePointLocal( ref p );
        position = Homeworld.GetClosestSurfacePointGlobal( p );
        Homeworld.CorrectSpawnLocation(ref position,250f);
        Vector3D up = (position - Homeworld.WorldMatrix.Translation);
        up.Normalize();
        position = position + (up * 150);
      }

      MainBase = new CubeGrid(CubeGrid.Spawn(String.IsNullOrWhiteSpace(SpawnPrefab) ? "Terran Planet Pod" : SpawnPrefab, MatrixD.CreateWorld(position), this));
      //MainBase.Init(Session);

      TakeControl( MainBase );

      if( MainBase.Grid == null ) {
        Mulligan();
        return false;
      }

      RespawnPoint = MainBase.GetRespawnBlock();

      position.X += 5;

      TakeControl( new Engineer(this) );

      return true;
    }

    public bool IsSubgrid( IMyCubeGrid grid ) {
      foreach( Controllable c in Controlled ) {
        CubeGrid g = c as CubeGrid;
        if( g != null ) {
          if(g.Subgrids.Contains(grid)) return true;

          // Determine if this grid is connected to that one
          List<IMySlimBlock> blocks = new List<IMySlimBlock>();
          grid.GetBlocks(blocks);

          foreach(IMySlimBlock block in blocks ) {
            if( block.FatBlock == null ) continue;
            IMyMotorStator stator = block.FatBlock as IMyMotorStator;
            if( stator != null && stator.RotorGrid == g.Grid ) {
              g.Subgrids.Add(grid);
              return true;
            }

          }
        }
      }

      return false;
    }

    // public override void Init(MyObjectBuilder_SessionComponent session) {
    //   base.Init(session);
    // }

    protected float Prioritize( Prefab prefab ) {
      //if( Tier < prefab.Tier ) return 0f;

      foreach( string resource in prefab.Cost.Keys ) {
        // Doesn't have access to resources
        if( !Resources.Contains(resource) ) {

          switch( resource ) {
            case "Cobalt":
              // Make exception if prefab has desired refinery
              if( prefab.RefineryTier < 2 )return 0f;
              break;
            case "Silver":
            case "Gold":
              // Make exception if prefab has desired refinery
              if( prefab.RefineryTier < 3 || Tier == Tech.Primitive )return 0f;
              break;
            default:
              return 0f;
          }

        }
      }
      float priority = (float)prefab.Price; // Build highest grid you can afford to reduce quantity of grids

      if( MyStats.InSpace && !prefab.Spacecraft && !prefab.IsStatic ) return 0f;

      if( CommandLine.Switch("grounded") && !prefab.Wheels && !prefab.IsStatic ) return 0f;

      //if( CommandLine.Switch("aerial") && !prefab.Flying && !prefab.IsStatic ) return 0f;


      if( prefab.FactoryTier > 0 ) {
        priority *= MyStats.Ratio["Factories"] < MyStats.Desired["Factories"] ? 2f : 1f;
      }
      if( prefab.RefineryTier > 0 ) {
        priority *= MyStats.Ratio["Refineries"] < MyStats.Desired["Refineries"] ? 2f : 1f;
      }

      if( prefab.Worker ) {
        priority *= MyStats.Ratio["Workers"] < MyStats.Desired["Workers"] ? (prefab.IsStatic ? 1f : 25f) : 1f;
      } else {
        priority *= MyStats.Ratio["Workers"] < MyStats.Desired["Workers"] ? 0 : 1f;
      }

      // if( prefab.Worker ) {
      //   priority *= MyStats.Ratio["Workers"] < MyStats.Desired["Workers"] ? 2f : 1f;
      // }

      if( prefab.Fighter ) {

          priority *= MyStats.Ratio["Fighters"] <= MyStats.Desired["Fighters"] ? 2f : 1f;

      }

      if( prefab.IsStatic ) {

          priority *= MyStats.Ratio["Static"] >= MyStats.Desired["Static"] ? .5f : 1f;

      }


      priority *= prefab.Atmosphere && !prefab.IsStatic && Tier < Tech.Space ? 2 : 1;


      return priority;
    }

    public void DetermineTechTier() {
      foreach( Controllable c in Controlled ) {
        CubeGrid g = c as CubeGrid;
        if( g != null )
          g.CheckFlags();
      }
      CubeGrid refinery = GetBestRefinery();
      if( refinery == null ) {
        CurrentGoal = new Goal {
          Type = Goals.Stabilize
        };
        Tier = Tech.Primitive; // Shouldn't be happening
        return;
      }
      switch( refinery.RefineryTier ) {
        case 3:
          Tier = Tech.Advanced;
          Resources.Add("Silver");
          Resources.Add("Gold");
          Resources.Add("Cobalt");
          Resources.Add("Magnesium");
          break;
        case 2:
          Tier = Tech.Established;
          Resources.Add("Cobalt");
          Resources.Add("Magnesium");
          break;
          break;
        default:
          Tier = Tech.Primitive;
          break;
      }
    }

    public void BlockCompleted( IMySlimBlock block ) {
      if( block.FatBlock == null ) return;
      if( block.FatBlock is IMyRefinery ) {
        if( Tier == Tech.Primitive ) {
          Tier = Tech.Established;
          Resources.Add("Cobalt");
          Resources.Add("Magnesium");
        }

        if( block.BlockDefinition.Id.SubtypeName == "LargeRefinery" && !Resources.Contains("Silver")) {
          Resources.Add("Silver");
          Resources.Add("Gold");
          Tier = Tech.Advanced;
          //if( Tier == Tech.Established ) Tier = Tech.Space;
          // if( MyStats.InSpace ) {
          //   Resources.Add("Platinum");
          //   Resources.Add("Uranium");
          // }
        }
      }
    }


  }
}
