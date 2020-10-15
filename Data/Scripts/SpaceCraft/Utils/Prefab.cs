using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using VRage.Game;

namespace SpaceCraft.Utils {

  public class Prefab {
    public string SubtypeId = String.Empty;
    public string Faction = String.Empty;
    public int Cost = 0;
    public bool IsStatic = false;
    public MyPrefabDefinition Definition;

    public static List<Prefab> Prefabs = new List<Prefab>();

    public void Init() {
      if( SubtypeId == String.Empty && Definition == null ) return;

      if( Definition == null )
        Definition = MyDefinitionManager.Static.GetPrefabDefinition(SubtypeId);

      if( Definition == null ) return;

      Prefabs.Add( this );

      if( Definition.CubeGrids == null ) return;

      foreach( MyObjectBuilder_CubeGrid grid in Definition.CubeGrids ) {
        if( grid == null ) continue;
        if( grid.IsStatic ) IsStatic = true;
        Cost += grid.CubeBlocks.Count;
      }
    }

    public static Prefab Get( string subtypeId ) {
      foreach( Prefab p in Prefabs ) if( p.SubtypeId == subtypeId ) return p;
      return null;
    }

    public static Prefab Add( string subtypeId, string faction ) {
      if( null == Prefab.Get(subtypeId) ) {
        Prefab prefab = new Prefab{
          SubtypeId = subtypeId,
          Faction = faction
        };

        prefab.Init();

        return prefab;
      }
      return null;
    }

    public override string ToString() {
      return base.ToString() + " " + SubtypeId;
    }
  }

}
