using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.Game;
using Sandbox.Definitions;
using Sandbox.Common.ObjectBuilders.Definitions;
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
            /*IMySlimBlock slim = MainBase.TryPlace( new MyObjectBuilder_MotorAdvancedStator{
              SubtypeName = "SmallAdvancedStator",
              Orientation =  Quaternion.CreateFromForwardUp(Vector3.Down, Vector3.Right),
              //BuildPercent = 0.0f,
              //ConstructionInventory = new MyObjectBuilder_Inventory()
            } );*/

            // IMySlimBlock slim = MainBase.TryPlace( new MyObjectBuilder_MotorAdvancedStator{
            //   SubtypeName = "SmallAdvancedStator",
            //   Orientation =  Quaternion.CreateFromForwardUp(Vector3.Down, Vector3.Right),
            //   Min = new Vector3I(4,-1,2)
            // } );

            IMySlimBlock slim = MainBase.TryPlace( new MyObjectBuilder_MotorAdvancedStator{
              SubtypeName = "SmallAdvancedStator",
              Orientation =  Quaternion.CreateFromForwardUp(Vector3.Left, Vector3.Forward),
              Min = new Vector3I(-1,-1,-3),
              //BuildPercent = 0.0f,
              ConstructionInventory = new MyObjectBuilder_Inventory()
            } );

            /*IMySlimBlock slim = MainBase.TryPlace( new MyObjectBuilder_Assembler{
              SubtypeName = "BasicAssembler",
              Min = RespawnPoint.Position + new Vector3I(0,0,3)
            });*/

            // IMySlimBlock slim = MainBase.TryPlace( new MyObjectBuilder_CargoContainer{
            //   SubtypeName = "SmallBlockMediumContainer",
            //   Min = RespawnPoint.Position + new Vector3I(0,1,0)
            // });

            if( slim == null ) {
              MyAPIGateway.Utilities.ShowMessage( "Faction", "FAILED to AddLargeGridConverter!!!!!!!!!!!!!!" );
            } else {
              MyAPIGateway.Utilities.ShowMessage( "Faction", "Successfully added large grid converter" );

              slim.CubeGrid.AddBlock( new MyObjectBuilder_MotorAdvancedRotor{
                SubtypeName = "SmallAdvancedRotor",
                BuildPercent = 0.0f
              },false );
              Progress = Progression.BasicAssembler;
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


  }
}
