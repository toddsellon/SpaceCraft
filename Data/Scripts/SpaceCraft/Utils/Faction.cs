using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.Game;
using Sandbox.Definitions;
using System;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
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



  public class Faction {
    public string Name;
    public MyCommandLine CommandLine = new MyCommandLine();
    public List<MySpawnGroupDefinition> Groups = new List<MySpawnGroupDefinition>();

    private IMyCubeBlock RespawnPoint;
    private Progression Progress = Progression.None;
    private string Roadblock = string.Empty;
    private List<Engineer> Engineers = new List<Engineer>();
    private List<Controllable> Controlled = new List<Controllable>();
    private List<MyEntity> Enemies = new List<MyEntity>();
    private CubeGrid MainBase;
    private List<string> Resources = new List<string>(){"Stone","Iron","Silicon","Nickel"};

    public void AssessProgression() {
      switch( Progress ) {
        case Progression.None:
          if( MainBase.Grid.GridSizeEnum == MyCubeSize.Small ) {
            IMySlimBlock slim = AddLargeGridConverter( MainBase );

            if( slim == null ) {
              MyAPIGateway.Utilities.ShowMessage( "Faction", "FAILED to AddLargeGridConverter!!!!!!!!!!!!!!" );
            } else {
              MyAPIGateway.Utilities.ShowMessage( "Faction", "Successfully added large grid converter" );
            }

          }
          break;
        case Progression.BasicAssembler:
        case Progression.BasicRefinery:
        case Progression.Assembler:
        case Progression.Refinery:
        case Progression.Reactor:
          break;
      }
    }

    public void UpdateBeforeSimulation() {
      foreach( Controllable c in Controlled ) {
        c.UpdateBeforeSimulation();
      }
    }

    public void UpdateBeforeSimulation10() {
      foreach( Controllable c in Controlled ) {
        c.UpdateBeforeSimulation10();
      }
    }

    public void UpdateBeforeSimulation100() {
      bool needs = false;
      foreach( Controllable c in Controlled ) {
        c.UpdateBeforeSimulation100();

        if( c is CubeGrid && (c as CubeGrid).Need != CubeGrid.Needs.None ) needs = true;
      }

      if( !needs ) {
        AssessProgression();
      }

    }

    public void AddControllable( Controllable c ) {
      c.Owner = this;
      c.Init();
      Controlled.Add( c );
      if( c is Engineer ) {
        Engineer e = c as Engineer;
        Engineers.Add( e );

        if( CommandLine.Switch("scavenger") ) {
          e.IssueOrder( new Order() {
            Type = Orders.Scout
          } );
        } else {
          e.IssueOrder( new Order() {
            Type = Orders.Grind,
            Destination = MyAPIGateway.Session.Player.GetPosition()
          } );
        }

      } else if( MainBase == null && c is CubeGrid ) {
        MainBase = c as CubeGrid;
        if( RespawnPoint == null ) {
          RespawnPoint = MainBase.GetRespawnBlock();
        }

      }
    }

    public MatrixD Translate(MatrixD matrix, Vector3D direction) {
      matrix.Translation = direction;
      return matrix;
      //return MatrixD.Add(matrix, MatrixD.CreateWorld(offset));
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
        //return Translate(matrix, new Vector3D(5,0,0) );
        //return Translate(RespawnPoint.CubeGrid.WorldMatrix, RespawnPoint.CubeGrid.WorldMatrix.GetDirectionVector(Base6Directions.Direction.Forward));
      } else {
        // TODO: Respawn Drop Pod
      }


      return MatrixD.Zero;
    }


    public IMySlimBlock AddLargeGridConverter( CubeGrid grid ) {
      // Create Adv Rotor
      Vector3I pos = Vector3I.Zero;
      grid.FindOpenSlot(out pos, MyCubeSize.Small);

      MyObjectBuilder_CubeBlock rotor = new MyObjectBuilder_CubeBlock() {
        EntityId = 0,
        //BlockOrientation = new SerializableBlockOrientation(Base6Directions.Direction.Up, Base6Directions.Direction.Backward),
        //SubtypeName = "SmallAdvancedStator",
        SubtypeName = "SmallBlockSmallGenerator",
        Name = string.Empty,
        //Min = new SerializableVector3I(-1,1,-1),
        Min = pos,
        Owner = 0,
        ShareMode = MyOwnershipShareModeEnum.None,
        DeformationRatio = 0,
        //BuildPercent = 0.0f,
        //ConstructionInventory = new MyObjectBuilder_Inventory()
      };

      IMySlimBlock slim = grid.Grid.AddBlock( rotor, false );

      if( slim != null ) {
        slim.SetToConstructionSite();
      }

      return slim;
    }


  }
}
