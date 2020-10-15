using SpaceCraft.Utils;
using System;
using System.Collections.Generic;
using Sandbox.Game.World;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage;
using VRageMath;
using VRage.Game.Entity;
using VRage.Game.ModAPI.Interfaces;
//using IMyControllableEntity = Sandbox.Game.Entities.IMyControllableEntity;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace SpaceCraft.Utils {

  //[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
  //public class Controllable : MySessionComponentBase, IMyEntityController {
  public class Controllable : IMyEntityController {

    public Order CurrentOrder;
    public List<Order> OrderQueue;

    public IMyControllableEntity ControlledEntity { get; protected set; }

    protected bool Flying = false;
    protected bool Drill = false;
    protected bool Welder = false;
    protected bool Grider = false;
    protected bool Wheels = false;
    public bool Destroyed = false;
    public Faction Owner;

    private Action<MyEntity> m_controlledEntityClosing;
    public event Action<IMyControllableEntity, IMyControllableEntity> ControlledEntityChanged;

    // https://github.com/KeenSoftwareHouse/SpaceEngineers/blob/a109106fc0ded66bdd5da70e099646203c56550f/Sources/Sandbox.Game/Game/World/MyEntityController.cs
    public void TakeControl( IMyControllableEntity entity ) {
      if (ControlledEntity == entity) return;
      if (entity != null && entity.ControllerInfo.Controller != null) return; // Entity controlled by another controller, release it first

      IMyControllableEntity old = ControlledEntity;

      if (old != null) {
        //var camera = old.GetCameraEntitySettings(); // TODO
        //old.Entity.OnClosing -= ControlledEntity_OnClosing;
        //old.ControllerInfo.Controller = null; // This will call OnControlReleased
        ControlledEntity = null;
      }

      if (entity != null) {
        ControlledEntity = entity;
        //ControlledEntity.Entity.OnClosing += ControlledEntity_OnClosing;
        //ControlledEntity.ControllerInfo.Controller = this; // This will call OnControlAcquired
      }

      if (old != entity && ControlledEntityChanged != null) ControlledEntityChanged(old, entity);
    }

    public int Prioritize( IMyCharacter character ) {
      return 1000;
    }

    public int Prioritize( IMyCubeGrid grid ) {
      return 999;
    }

    public int Prioritize( IMySlimBlock slim ) {
			if( slim.FatBlock == null ) return 0;
      IMyCubeBlock block = slim.FatBlock;
      //BlockDefinition

      //MyCubeBlockDefinition GetCubeBlockDefinition (MyDefinitionId id)
      //MyObjectBuilder_DefinitionBase def = MyDefinitionManager.Static.GetObjectBuilder(slim.BlockDefinition);
      //MyCubeBlockDefinition def = MyDefinitionManager.Static.GetCubeBlockDefinition(slim.BlockDefinition.Id);
      string subtypeName = slim.BlockDefinition.Id.SubtypeName;
      if( block is IMyAssembler ) {
        //MyAPIGateway.Utilities.ShowMessage( "Prioritize", "slim.BlockDefinition.DisplayNameString" + slim.BlockDefinition.DisplayNameString );
        //switch( slim.BlockDefinition.DisplayNameString ) {
        switch(subtypeName) {
          case "LargeAssembler":
            return 100;
          case "BasicAssembler":
            return 50;
        }
        return 49;
      }

      if( block is IMyRefinery ) {
        //return 48;
        return subtypeName == "LargeRefinery" ? 99 : 48;
      }

      if( block is IMyProductionBlock ) {
        return 47;
      }

      return 1;
		}

    public void Stop() {
      OrderQueue.Clear();
      CurrentOrder = null;
    }

    public bool Execute( Order order, bool force = false ) {

      if( force ) Stop();

      if( CurrentOrder == null && order != null ) {
        CurrentOrder = order;
        return true;
      } else if( order != null ) {
        OrderQueue.Add(order);
        return true;
      }

      return false;

    }

    public virtual void Move() {
      if( CurrentOrder.Target == null && CurrentOrder.Destination == null ) {
        CurrentOrder = null;
        return;
      }

      Vector3D destination = CurrentOrder.Target == null ? CurrentOrder.Destination : CurrentOrder.Target.WorldMatrix.GetDirectionVector(Base6Directions.Direction.Forward);
      ControlledEntity.MoveAndRotate( Vector3.Normalize(destination), Vector2.Zero, 0.0f );
		}

    public void BeginShoot( MyShootActionEnum action ) {
      ((Sandbox.Game.Entities.IMyControllableEntity)ControlledEntity).BeginShoot(action);
    }

    public Order Next() {
      if( OrderQueue.Count > 0 ) {
        Order o = OrderQueue[0];
        OrderQueue.Remove(o);
        return o;
      } else {
        return Owner.NeedsOrder(this);
      }

    }

    private void ControlledEntity_OnClosing(MyEntity entity = null) {
        if( ControlledEntity == null ) return; // Already freed

        TakeControl(null);
    }

    public virtual void Init( MyObjectBuilder_SessionComponent session ) {
			//base.Init(session);
		}

    public virtual void UpdateBeforeSimulation() {
    }

    public virtual List<IMyInventory> GetInventory( List<IMySlimBlock> blocks = null ) {
      return new List<IMyInventory>();
    }



    // public Vector3D UpVector {
    //   get {
    //     return (position - ControlledEntity.WorldMatrix.Translation).Normalize();
    //   }
    // }

    private IMyEntityController m_controller;
    public IMyEntityController Controller
    {
      get
      {
         return m_controller;
      }
      set
      {
         if (m_controller != value)
         {
             if (m_controller != null)
             {
                 //if (ControlReleased != null) ControlReleased(m_controller);

                 m_controller = null;
             }

             if (value != null)
             {
                 m_controller = value;

                 //if (ControlAcquired != null) ControlAcquired(m_controller);
             }
         }
      }
    }

    /*public string ToString() {

			try {

				return Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary<Engineer>(this));

			} catch(Exception exc) {

			}

			return string.Empty;

		}*/

  }



}
