using System;
using System.Linq;
using System.Collections.Generic;
using VRageMath;
using VRage;
using VRage.Utils;
using VRage.Game.Entity;
using VRage.Game.Components;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Game;
using Sandbox.Game.EntityComponents;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Components;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.Game.Entities.Character;

namespace SpaceCraft.Utils {

  public enum Buff {
    Terrazine
  };

  public class Buffs {

    public static Dictionary<IMyCharacter,List<Buff>> Characters = new Dictionary<IMyCharacter,List<Buff>>();

    public static Guid GuidBuffs = new Guid("22886351-E7FA-4DB2-9783-138637404D45");

    public static void Restore( IMyCharacter character ) {
      if( character == null || character.Storage == null || !character.Storage.ContainsKey(GuidBuffs) ) return;

      string[] buffs = character.Storage[GuidBuffs].Split(',');
      foreach( string buff in buffs ) {
        Buff type = Buff.Terrazine;
        if( Buff.TryParse(buff, out type) ) {
          ApplyBuff(character, type, true);
        }
      }

    }

    public static void ApplyBuff( IMyCharacter character, Buff buff, bool initializing = false, IMyPlayer player = null ) {
      if( !Characters.ContainsKey(character) )
        Characters[character] = new List<Buff>();

      if( Characters[character].Contains(buff) ) return; // No stacking

      Characters[character].Add(buff);
      BuffApplied(character, buff, player);

      if(!initializing) {
        character.Storage = character.Storage ?? new MyModStorageComponent();
        character.Storage[GuidBuffs] = SerializeBuffs(Characters[character]);
      }
    }

    private static void BuffApplied( IMyCharacter character, Buff buff, IMyPlayer player = null ) {
      switch( buff ) {
        case Buff.Terrazine:
          MyVisualScriptLogicProvider.SetPlayerGeneralDamageModifier( player == null ? SpaceCraftSession.GetPlayerId(character) : player.PlayerID, .5f);
          break;
      }
    }

    public static string SerializeBuffs( List<Buff> buffs ) {
      string str = "";
      foreach( Buff buff in buffs ) {
        if( str.Length > 0 ) str += ",";
        str += buff.ToString();
      }
      return str;
    }

    public static bool HasBuff( IMyCharacter character, Buff buff ) {
      if( !Characters.ContainsKey(character) ) return false;
      return Characters[character].Contains(buff);
    }

    public static void Update() {
      foreach( IMyCharacter character in Characters.Keys.ToList() ) {
        if( character == null || character.Closed ) {
          Remove(character);
          continue;
        }

        foreach( Buff buff in Characters[character] ) {

          Update(character, buff);
        }
      }
    }

    public static void Remove( IMyCharacter character ) {
      if( !Characters.ContainsKey(character) ) return;
      Characters.Remove(character);
      if( character == null ) return;
      if( character.Storage != null && character.Storage.ContainsKey(GuidBuffs) )
        character.Storage.Remove(GuidBuffs);
    }

    private static void Update( IMyCharacter character, Buff buff ) {
      switch( buff ) {
        case Buff.Terrazine:
          MyCharacterOxygenComponent o2 = character.Components.Get<MyCharacterOxygenComponent>();

          if( o2 != null ) {
            o2.SuitOxygenAmount = 100.0f;
            o2.CharacterGasSink.SetInputFromDistributor(OBTypes.Electricity,1f,true,true);
          }

          MyCharacterStatComponent stats = character.Components.Get<MyEntityStatComponent>() as MyCharacterStatComponent;
          if( stats == null ) return;

          MyEntityStat health;

          stats.TryGetStat(MyStringHash.Get("Health"), out health);

          if( health != null ) {
            health.Increase(1f,null);
          }
          break;
      }
    }

  }

}
