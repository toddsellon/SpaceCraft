using System;
using System.Collections.Generic;
using VRageMath;
using VRage;
using VRage.Game.Entity;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Definitions;

namespace SpaceCraft.Utils.MES {

  public class MES {

    private static ushort Id = 8877;
    private static ulong PublishedFileId = 1521905890;
    private static bool Enabled = false;
    private static string[] EncounterTypes = { "SpaceCargoShips", "RandomEncounters", "PlanetaryCargoShips", "PlanetaryInstallations", "BossEncounters", "OtherNPCs" };

    public static void Init() {
      // foreach (var mod in MyAPIGateway.Session.Mods) {
      //   if (mod.PublishedFileId == PublishedFileId) {
      //     Enabled = true;
      //     break;
      //   }
      // }
      //
      // if( !Enabled ) return;


      ///MES.Settings.General.NpcDistanceCheckTimerTrigger

      foreach( string type in EncounterTypes ) {
        SyncData message = new SyncData {
          //ChatMessage = "/MES.Settings."+type+".CleanupUseDistance.false"
          ChatMessage = "/MES.Settings."+type+".UseCleanupSettings.false"
        };
        Send( message );
      }

      // SyncData message = new SyncData {
      //   ChatMessage = "/MES.Settings.OtherNPCs.CleanupUseDistance.false"
      // };
      //
      // Send( message );

    }

    public static bool Send(SyncData message) {
      IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
      if( message == null || player == null ) return false;

      message.PlayerId = player.IdentityId;
      message.SteamUserId = player.SteamUserId;
      message.Instruction = "MESChatMsg";

      try {
        byte[] data = MyAPIGateway.Utilities.SerializeToBinary<SyncData>(message);
        return MyAPIGateway.Multiplayer.SendMessageToServer(Id, data);
      } catch(Exception exc) {
        return false;
      }

      return true;
    }

  }

}
