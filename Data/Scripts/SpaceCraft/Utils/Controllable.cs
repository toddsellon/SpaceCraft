using SpaceCraft.Utils;
using System;
using System.Collections.Generic;
using Sandbox.Game.World;
using VRage.Game.Entity;

namespace SpaceCraft.Utils {

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

    public void IssueOrder( Order order, bool force = false ) {

      if( force ) {
        OrderQueue.Clear();
        CurrentOrder = null;
      }

      if( CurrentOrder == null ) {
        CurrentOrder = order;
        StartOrder();
      } else {
        OrderQueue.Add(order);
      }

    }

    private void ControlledEntity_OnClosing(MyEntity entity = null) {
        if( ControlledEntity == null ) return; // Already freed

        TakeControl(null);
    }

    public void CompleteOrder() {
      MyAPIGateway.Utilities.ShowNotification("Order Completed " + CurrentOrder.ToString() );
      if( OrderQueue.Count > 0 ) {
        CurrentOrder = OrderQueue[0];
        OrderQueue.Remove(CurrentOrder);
        StartOrder();
      } else {
        CurrentOrder = null;
      }

    }

    public virtual void StartOrder() {
    }
    public virtual void Init() {
    }
    public virtual void UpdateBeforeSimulation() {
    }
    public virtual void UpdateBeforeSimulation10() {
    }
    public virtual void UpdateBeforeSimulation100() {
    }

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
