using System;
using System.Collections;
using System.Collections.Generic;
using VRageMath;
using VRage;
using VRage.Game.Entity;
using VRage.Game.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using SpaceCraft.Utils;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace SpaceCraft.Utils {


  public class CLI {

    //protected QueuedAction Queued;
    protected static ushort Id = 8008;
    protected bool Server;
    protected Dictionary<string,Action<MyCommandLine,Message>> Actions = new Dictionary<string,Action<MyCommandLine,Message>>(StringComparer.OrdinalIgnoreCase);

    public CLI(bool server) {
      Server = server;

      Actions.Add("get",Get);
      Actions.Add("attack",Attack);
      Actions.Add("build",Build);
      Actions.Add("set",Set);
      Actions.Add("spawn",Spawn);
      Actions.Add("complete",Complete);
      Actions.Add("pay",Pay);
      Actions.Add("follow",Follow);
      Actions.Add("war",War);
      Actions.Add("peace",Peace);
      Actions.Add("join",Join);
      Actions.Add("debug",Debug);
      Actions.Add("gps",GPS);
      Actions.Add("control",Control);
      Actions.Add("release",Release);

      MyAPIGateway.Utilities.MessageEntered += MessageEntered;
      MyAPIGateway.Multiplayer.RegisterMessageHandler(Id, MessageHandler);
    }

    public void Destroy() {
      MyAPIGateway.Utilities.MessageEntered -= MessageEntered;
      MyAPIGateway.Multiplayer.UnregisterMessageHandler(Id, MessageHandler);
    }

    public void MessageEntered(string text, ref bool broadcast){
			if(!text.StartsWith("/sc ",StringComparison.OrdinalIgnoreCase)) return;

      broadcast = false;


      if( !Server ) {
        SendMessageToServer( new Message {
          Text = text
        });
        return;
      }

			ParseMessage(text);

		}

    public void MessageHandler(byte[] data){

      Message message = MyAPIGateway.Utilities.SerializeFromBinary<Message>(data);

      if( Server ) {
        ParseMessage(message.Text,message);
      } else {
        MyAPIGateway.Utilities.ShowMessage( message.Sender, message.Text );
      }

		}

    public bool ParseMessage(string text, Message message = null) {
      MyCommandLine cmd = new MyCommandLine();
			if( !cmd.TryParse(text) ) return false;

      string action = cmd.Argument(1);

      if( Actions.ContainsKey(action) ) {
        Actions[action](cmd, message);
        return true;
      }
      Respond("Error","Unknown command " + cmd.Argument(1));
      return false;
    }

    public bool SendMessageToServer(Message message) {
      if( message == null ) return false;
      IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
      message.SteamUserId = player.SteamUserId;
      message.PlayerID = player.IdentityId;
      byte[] data = MyAPIGateway.Utilities.SerializeToBinary<Message>(message);
      return MyAPIGateway.Multiplayer.SendMessageToServer(Id, data);
    }

    public bool SendMessageToClient(Message message) {
      if( message == null || message.SteamUserId == 0 ) return false;
      byte[] data = MyAPIGateway.Utilities.SerializeToBinary<Message>(message);
      return MyAPIGateway.Multiplayer.SendMessageTo(Id, data, message.SteamUserId);
    }

    public void Respond( string sender, string text, Message message = null ) {
      if( message == null )
        MyAPIGateway.Utilities.ShowMessage( sender, text );
      else {
        message.Sender = sender;
        message.Text = text;
        SendMessageToClient(message);
      }
    }


    public void GPS( MyCommandLine cmd, Message message ) {
      // GPS:here:1002758.94895413:133846.064550405:1575992.06520915:#FF75C9F1:
      IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
      Faction faction = SpaceCraftSession.GetFactionContaining(message == null ? player.PlayerID : message.PlayerID);
      if( faction == null ) {
        Respond("Error", "You do not belong to a SpaceCraft faction", message);
        return;
      }

      foreach( Controllable c in faction.Controlled ) {
        //Vector3D position = c.Entity.WorldMatrix.Translation;
        IMyGps gps = MyAPIGateway.Session.GPS.Create(c.Entity.DisplayName, c.Entity.DisplayName, c.Entity.WorldMatrix.Translation, true, false);
        MyAPIGateway.Session.GPS.AddGps(message == null ? player.PlayerID : message.PlayerID, gps);
        //Respond("GPS", "GPS:" + c.Entity.DisplayName + ":" + position.X.ToString() + ":" + position.Y.ToString() + ":" + position.Z.ToString(), message);
      }

    }

    public void Debug( MyCommandLine cmd, Message message ) {
      Convars.Static.Debug = !Convars.Static.Debug;
      Respond("Debug", Convars.Static.Debug ? "On" : "Off", message);
    }

    public void Set( MyCommandLine cmd, Message message ) {
      if( message != null ) {
        Respond("Error","You don't have permission", message);
        return;
      }
      //IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
			//if( player.PromoteLevel != MyPromoteLevel.Admin && player.PromoteLevel != MyPromoteLevel.Owner ) return;
      string convar = cmd.Argument(2);
      Respond(convar, Convars.Static.Set(convar,cmd.Argument(3)), message);
    }

    public void Get( MyCommandLine cmd, Message message ) {
      string convar = cmd.Argument(2);
      Respond(convar, Convars.Static.Get(convar), message);
    }

    public void Attack( MyCommandLine cmd, Message message ) {
      IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
      Faction faction = SpaceCraftSession.GetFactionContaining(player.PlayerID);
      if( faction == null ) return;
      IMyEntity enemy;
      if( !String.IsNullOrWhiteSpace(cmd.Argument(2)) ) {
        Faction target = SpaceCraftSession.GetFaction(cmd.Argument(2));
        enemy = faction.GetClosestEnemy(player.GetPosition(),target == null ? null : target.MyFaction);
      } else {
        enemy = faction.GetClosestEnemy(player.GetPosition());
      }

      if( enemy == null ) return;

      foreach( Controllable c in faction.Controlled ) {
        if( !c.IsStatic && c.Fighter ) {
          c.Execute( new Order {
            Type = Orders.Attack,
            Target = enemy
          }, true);
        }
      }

    }

    public void Build( MyCommandLine cmd, Message message ) {
      IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
      Faction faction = SpaceCraftSession.GetFactionContaining(message == null ? player.PlayerID : message.PlayerID);
      if( faction == null ) {
        Respond("Error", "You do not belong to a SpaceCraft faction", message);
        return;
      }

      Prefab prefab = Prefab.Get(cmd.Argument(2));
      if( prefab == null ) {
        Respond("Error", "Prefab not found " + cmd.Argument(2), message);
        return;
      }

      faction.CurrentGoal = new Goal {
        Type = Goals.Construct,
        Prefab = prefab
      };

      Respond("Building", cmd.Argument(2), message);
    }

    public void Spawn( MyCommandLine cmd, Message message ) {
      if( message != null ) {
        Respond("Error","You don't have permission", message);
        return;
      }
      IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
      Faction faction = String.IsNullOrWhiteSpace(cmd.Argument(3)) ? SpaceCraftSession.GetFactionContaining(player.PlayerID) : SpaceCraftSession.GetFaction(cmd.Argument(3).ToUpper());
      if( faction == null ) {
        Respond("Error", "SpaceCraft faction not found " + cmd.Argument(3), message);
        return;
      }
      Prefab prefab = Prefab.Get(cmd.Argument(2));
      if( prefab == null ) {
        Respond("Error", "Prefab not found " + cmd.Argument(2), message);
        return;
      }
      CubeGrid grid = new CubeGrid( CubeGrid.Spawn(prefab, faction.GetPlacementLocation(prefab), faction) );

      if( grid == null && grid.Grid == null ) {
          Respond("Error", "Failed to spawn " + cmd.Argument(2), message);
          return;
      }

      List<IMySlimBlock> suspensions = grid.GetBlocks<IMyMotorSuspension>();
      long owner = faction.MyFaction == null ? (long)0 : faction.MyFaction.FounderId;
      foreach(IMySlimBlock block in suspensions) {
        IMyMotorSuspension suspension = block.FatBlock as IMyMotorSuspension;
        Block.DoAction(suspension, "Add Wheel");
        if( suspension.RotorGrid != null ) {
          suspension.RotorGrid.DisplayName = faction.Name + " Subgrid";
          grid.Subgrids.Add(suspension.RotorGrid);
          IMySlimBlock wheel = suspension.RotorGrid.GetCubeBlock(Vector3I.Zero) as IMySlimBlock;
          if( wheel == null ) continue;
          wheel.SpawnConstructionStockpile();
          wheel.IncreaseMountLevel(100f,owner);
        }
      }

      grid.CheckFlags();
      faction.TakeControl( grid );
      if( faction.MainBase != null )
        faction.MainBase.ToggleDocked(grid);

      Respond("Spawning", cmd.Argument(2), message);
    }

    public void Complete( MyCommandLine cmd, Message message ) {
      if( message != null ) {
        Respond("Error","You don't have permission", message);
        return;
      }
      IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
      Faction faction = String.IsNullOrWhiteSpace(cmd.Argument(3)) ? SpaceCraftSession.GetFactionContaining(player.PlayerID) : SpaceCraftSession.GetFaction(cmd.Argument(3).ToUpper());
      if( faction == null ) {
        Respond("Error", "SpaceCraft faction not found " + cmd.Argument(3), message);
        return;
      }

      CubeGrid grid = faction.CurrentGoal.Entity as CubeGrid;

      if( grid == null ) {
        Respond("Error", "Your faction is not constructing anything", message);
        return;
      }


      long owner = faction.MyFaction == null ? (long)0 : faction.MyFaction.FounderId;
      List<IMySlimBlock> blocks = grid.GetBlocks<IMySlimBlock>();
      foreach(IMySlimBlock block in blocks) {
        if( block.IsFullIntegrity ) continue;
        //block.SetToConstructionSite();
        block.SpawnConstructionStockpile();
        block.IncreaseMountLevel(100f,owner);
        faction.BlockCompleted(block);
      }
      grid.ConstructionSite = null;
      grid.CheckFlags();
      if( faction.MainBase != null )
        foreach( CubeGrid g in faction.MainBase.Docked ) {
          if( g == null ) continue;
          if( g.CurrentOrder != null )
            g.CurrentOrder.Complete();
        }
      //grid.ConstructionSite = null;

      Respond("Completed", grid.Grid.DisplayName, message);
    }

    // Pays off a battery balance if applicable
    public void Pay( MyCommandLine cmd, Message message ) {
      if( message != null ) {
        Respond("Error","You don't have permission", message);
        return;
      }
      IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
      Faction faction = String.IsNullOrWhiteSpace(cmd.Argument(3)) ? SpaceCraftSession.GetFactionContaining(player.PlayerID) : SpaceCraftSession.GetFaction(cmd.Argument(3).ToUpper());
      if( faction == null ) {
        Respond("Error", "SpaceCraft faction not found " + cmd.Argument(3), message);
        return;
      }

      foreach( Controllable c in faction.Controlled ) {
        CubeGrid grid = c as CubeGrid;
        if( grid != null )
          grid.Balance = null;
      }

      faction.CurrentGoal.Balance = null;

      Respond("Pay", "Paid off all balances", message);
    }

    public void Follow( MyCommandLine cmd, Message message ) {
      IMyPlayer player = message == null ? MyAPIGateway.Session.LocalHumanPlayer : SpaceCraftSession.GetPlayer(message.PlayerID);
      if( player == null ) {
        Respond("Error", "Player not found", message);
        return;
      }
      Faction faction = SpaceCraftSession.GetFactionContaining(player.PlayerID);
      if( faction == null ) {
        Respond("Error", "You do not belong to a SpaceCraft faction", message);
        return;
      }

      faction.Follow(player);
      Respond("Following", player.DisplayName, message);
    }

    public void Join( MyCommandLine cmd, Message message ) {
      IMyPlayer player = message == null ? MyAPIGateway.Session.LocalHumanPlayer : SpaceCraftSession.GetPlayer(message.PlayerID);
      if( player == null ) {
        Respond("Error", "Player not found", message);
        return;
      }
      Faction faction = SpaceCraftSession.GetFaction(cmd.Argument(2).ToUpper());
      if( faction == null ) {
        Respond("Error", cmd.Argument(2) + " faction could not be found", message);
        return;
      }

      MyAPIGateway.Session.Factions.KickPlayerFromFaction(player.PlayerID);
      if( message == null ) // Force join (I think)
        MyAPIGateway.Session.Factions.AddPlayerToFaction(player.PlayerID,faction.MyFaction.FactionId);
      else {
        MyAPIGateway.Session.Factions.SendJoinRequest(faction.MyFaction.FactionId, player.PlayerID);
        MyAPIGateway.Session.Factions.AcceptJoin(faction.MyFaction.FactionId, player.PlayerID);
      }

      IMyFaction current = MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.PlayerID);
      Respond("Join", current == faction.MyFaction ? "Joined faction " + cmd.Argument(2) : "Failed to join faction", message);
    }

    public void War( MyCommandLine cmd, Message message ) {
      IMyPlayer player = message == null ? MyAPIGateway.Session.LocalHumanPlayer : SpaceCraftSession.GetPlayer(message.PlayerID);
      if( player == null ) {
        Respond("Error", "Player not found", message);
        return;
      }
      Faction faction = SpaceCraftSession.GetFactionContaining(player.PlayerID);
      if( faction == null ) {
        Respond("Error", "You do not belong to a SpaceCraft faction", message);
        return;
      }

      Faction target = SpaceCraftSession.GetFaction(cmd.Argument(2).ToUpper());
      if( target == null ) {
        Respond("Error", "Could not find faction to declare war", message);
        return;
      }

      MyAPIGateway.Session.Factions.DeclareWar(faction.MyFaction.FactionId, target.MyFaction.FactionId);
      Respond("War", "were declared on " + target.Name, message);
    }

    public void Peace( MyCommandLine cmd, Message message ) {
      IMyPlayer player = message == null ? MyAPIGateway.Session.LocalHumanPlayer : SpaceCraftSession.GetPlayer(message.PlayerID);
      if( player == null ) {
        Respond("Error", "Player not found", message);
        return;
      }
      Faction faction = SpaceCraftSession.GetFactionContaining(player.PlayerID);
      if( faction == null ) {
        Respond("Error", "You do not belong to a SpaceCraft faction", message);
        return;
      }
      Faction target = SpaceCraftSession.GetFaction(cmd.Argument(2).ToUpper());
      if( target == null ) {
        Respond("Error", "Could not find faction to declare war", message);
        return;
      }
    }

    public void Control( MyCommandLine cmd, Message message ) {
      if( MyAPIGateway.Session.LocalHumanPlayer != SpaceCraftSession.GetPlayer(message.PlayerID) ) {
        Respond("Error", "You do not have permission", message);
        return; // Host/API Only
      }
      IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
      if( player == null ) {
        Respond("Error", "Player not found", message);
        return;
      }

      long id;
      if( !Int64.TryParse(cmd.Argument(2), out id) ) return;

      IMyEntity entity = null;
      if( !MyAPIGateway.Entities.TryGetEntityById(id, out entity) ) {
        Respond("Error", "Entity " + id.ToString() + " not found", null);
        return;
      }

      Faction faction = String.IsNullOrWhiteSpace(cmd.Argument(3)) ? SpaceCraftSession.GetFactionContaining(player.PlayerID) : SpaceCraftSession.GetFaction(cmd.Argument(3).ToUpper());
      if( faction == null ) {
        Respond("Error", "Could not find faction " + cmd.Argument(3), message);
        return;
      }

      if( entity is IMyCubeGrid ) {
        CubeGrid grid = new CubeGrid(entity as IMyCubeGrid);
        faction.TakeControl( grid );
        if( faction.MainBase != null )
          faction.MainBase.ToggleDocked(grid);
        return;
      } else if( entity is IMyCharacter ) {
        faction.TakeControl( new Engineer(faction, entity as IMyCharacter) );
        return;
      }

      Respond("Error", "Could not take control of " + entity.DisplayName, message);

    }

    public void Release( MyCommandLine cmd, Message message ) {
      if( MyAPIGateway.Session.LocalHumanPlayer != SpaceCraftSession.GetPlayer(message.PlayerID) ) {
        Respond("Error", "You do not have permission", message);
        return; // Host/API Only
      }
      IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
      if( player == null ) {
        Respond("Error", "Player not found", message);
        return;
      }

      long id;
      if( !Int64.TryParse(cmd.Argument(2), out id) ) return;

      IMyEntity entity = null;
      if( !MyAPIGateway.Entities.TryGetEntityById(id, out entity) ) {
        Respond("Error", "Entity " + id.ToString() + " not found", null);
        return;
      }

      Faction faction = String.IsNullOrWhiteSpace(cmd.Argument(3)) ? SpaceCraftSession.GetFactionContaining(player.PlayerID) : SpaceCraftSession.GetFaction(cmd.Argument(3).ToUpper());
      if( faction == null ) {
        Respond("Error", "Could not find faction " + cmd.Argument(3), message);
        return;
      }

      Controllable removed = faction.ReleaseControl(entity);
      CubeGrid grid = removed as CubeGrid;
      if( grid != null && grid.DockedTo != null )
        grid.DockedTo.ToggleDocked(grid);

      if( removed == null )
        Respond("Error", "Could release control of " + entity.DisplayName, message);

    }



  }

}
